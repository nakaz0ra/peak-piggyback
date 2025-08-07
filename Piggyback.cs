using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;
using Zorro.Core.Serizalization;
using Object = UnityEngine.Object;

namespace Piggyback;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Piggyback : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;

    private readonly Harmony m_harmony = new(MyPluginInfo.PLUGIN_GUID);

    private static ConfigEntry<bool> s_enablePiggybackSetting;
    private static ConfigEntry<bool> s_allowPiggybackByOthersSetting;
    private static ConfigEntry<bool> s_spectateViewSetting;
    private static ConfigEntry<float> s_holdToCarrySetting;
    private static ConfigEntry<bool> s_swapBackpackSetting;
    private static ConfigEntry<string> s_gamepadDropKeyBindingSetting;

    private static readonly Dictionary<string, string> GamepadKeysToControlPath = new(StringComparer.OrdinalIgnoreCase)
    {
        { "DpadUp", "dpad/up" },
        { "DpadDown", "dpad/down" },
        { "DpadLeft", "dpad/left" },
        { "DpadRight", "dpad/right" },
        { "North", "buttonSouth" },
        { "East", "buttonEast" },
        { "South", "buttonSouth" },
        { "West", "buttonWest" },
        { "LeftStickButton", "leftStickPress" },
        { "RightStickButton", "rightStickPress" },
        { "LeftShoulder", "leftShoulder" },
        { "RightShoulder", "rightShoulder" },
        { "LeftTrigger", "leftTrigger" },
        { "RightTrigger", "rightTrigger" },
        { "Start", "start" },
        { "Select", "select" }
    };

    private static List<InputAction> s_gamepadDropActions = [];

    private static readonly Action<CharacterCarrying, Character> DropFromCarryDelegate =
        (Action<CharacterCarrying, Character>)Delegate.CreateDelegate(
            typeof(Action<CharacterCarrying, Character>),
            null,
            typeof(CharacterCarrying).GetMethod("Drop", BindingFlags.Instance | BindingFlags.NonPublic)!
        );
    private static readonly Func<CharacterInteractible, bool> CarriedByLocalCharacterDelegate =
        (Func<CharacterInteractible, bool>)Delegate.CreateDelegate(
            typeof(Func<CharacterInteractible, bool>),
            null,
            typeof(CharacterInteractible).GetMethod("CarriedByLocalCharacter", BindingFlags.Instance | BindingFlags.NonPublic)!
        );
    private static readonly Func<CharacterInteractible, bool> CanBeCarriedDelegate =
        (Func<CharacterInteractible, bool>)Delegate.CreateDelegate(
            typeof(Func<CharacterInteractible, bool>),
            null,
            typeof(CharacterInteractible).GetMethod("CanBeCarried", BindingFlags.Instance | BindingFlags.NonPublic)!
        );

    private void Awake()
    {
        Logger = base.Logger;

        SetupConfig();

        if (s_enablePiggybackSetting.Value)
        {
            m_harmony.PatchAll(typeof(CanBeCarriedPatch));
            m_harmony.PatchAll(typeof(SpectateViewPatch));
            m_harmony.PatchAll(typeof(HoldToCarryPatch));
        }
        m_harmony.PatchAll(typeof(BackpackSwapOnCarryPatch));

        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_NAME} loaded");
    }

    private void SetupConfig()
    {
        s_enablePiggybackSetting = Config.Bind("General", "EnablePiggyback", true,
            "If false, you won't be able to piggyback others.\n" +
            "The AllowPiggybackByOthers, SwapBackpack, and GamepadDropKeybind settings will still take effect, " +
            "but you won't be able to carry players that are not passed out.");
        s_allowPiggybackByOthersSetting = Config.Bind("General", "AllowPiggybackByOthers", true,
            "If false, you'll be immediately dropped whenever a player attempts to carry you " +
            "while you're not passed out. Use this if you want to fully prevent players from picking you up.");
        s_spectateViewSetting = Config.Bind("General", "SpectateView", true,
            "If true, the camera will switch to the spectate view while being carried.\n" +
            "If false, the camera will remain in the first-person view while being carried.\n" +
            "Note that you'll only be able to spectate the player carrying you.");
        s_holdToCarrySetting = Config.Bind("General", "HoldToCarryTime", 1.5f,
            new ConfigDescription(
                "The time in seconds you need to hold the interact button to start carrying another player.\n" +
                "If 0, you will start carrying the player immediately upon pressing the interact button.\n" +
                "If the player is passed out, you will start carrying them immediately even if this is set to a " +
                "value greater than 0.", new AcceptableValueRange<float>(0.0f, 5.0f)
            ));
        s_swapBackpackSetting = Config.Bind("General", "SwapBackpack", true,
            "If true, if you have a backpack and start carrying another player who does not " +
            "have a backpack, the player you're carrying will automatically equip your backpack.\n" +
            "The backpack will be returned to you when you drop the player.\n" +
            "If false or if the player you want to carry already has a backpack, you must manually drop your " +
            "backpack before you can carry that player.");
        s_gamepadDropKeyBindingSetting = Config.Bind("Controls", "GamepadDropKeybind",
            "LeftShoulder+RightShoulder",
            "The key binding to drop the player you're carrying. " +
            "This only applies to Gamepad, the binding on keyboard should be the Number 4 key by default.\n" +
            "You can combine multiple keys by separating them with a plus (+) sign. This would require you to press " +
            "all the given keys at the same time to drop the player.\n" +
            "Acceptable Gamepad Keys:\nNone, " + string.Join(", ", GamepadKeysToControlPath.Keys));

        s_gamepadDropKeyBindingSetting.SettingChanged += (_, _) => SetupGamepadDropAction();
        SetupGamepadDropAction();
    }

    private static void SetupGamepadDropAction()
    {
        var value = s_gamepadDropKeyBindingSetting.Value.Trim();
        if (s_gamepadDropActions.Count > 0)
        {
            foreach (var action in s_gamepadDropActions)
            {
                action.Disable();
                action.Dispose();
            }
            s_gamepadDropActions.Clear();
        }
        if (value.ToLower() == "none")
        {
            Logger.LogInfo("Gamepad drop key binding is set to 'None'. Disabling custom drop key binding.");
            return;
        }
        var bindings = new HashSet<string>();
        bool isInvalid = false;
        foreach (var key in value.Split('+'))
        {
            if (GamepadKeysToControlPath.TryGetValue(key.Trim(), out var controlPath))
            {
                bindings.Add($"<Gamepad>/{controlPath}");
            }
            else
            {
                Logger.LogWarning($"Unknown gamepad key: {key.Trim()}");
                isInvalid = true;
            }
        }
        if (isInvalid)
        {
            var defaultBinding = (string)s_gamepadDropKeyBindingSetting.DefaultValue;
            Logger.LogError($"Invalid gamepad key binding detected. Falling back to default binding: {defaultBinding}");
            bindings.Clear();
            foreach (var key in defaultBinding.Split('+'))
            {
                if (GamepadKeysToControlPath.TryGetValue(key.Trim(), out var controlPath))
                    bindings.Add($"<Gamepad>/{controlPath}");
                else
                    Logger.LogError($"Unknown default gamepad key: {key.Trim()}");
            }
        }
        foreach (var binding in bindings)
        {
            var action = new InputAction($"DropCarry_{binding}", InputActionType.Button, binding);
            s_gamepadDropActions.Add(action);
            action.Enable();
        }
        Logger.LogInfo("Custom drop key binding set to: " + string.Join(" + ", bindings));
    }

    private void Update()
    {
        if (!(bool)(Object)Character.localCharacter) return;
        if ((bool)(Object)Character.localCharacter.data.carriedPlayer && s_gamepadDropActions.Count > 0)
        {
            if ((s_gamepadDropActions.Count == 1 && s_gamepadDropActions[0].WasPressedThisFrame())
                || s_gamepadDropActions.All(action => action.IsPressed()))
                DropPlayerFromCarry(Character.localCharacter.data.carriedPlayer);
        }
        if (!s_allowPiggybackByOthersSetting.Value)
        {
            if (!Character.localCharacter.data.fullyPassedOut && Character.localCharacter.data.isCarried)
            {
                DropPlayerFromCarry(Character.localCharacter);
                return;
            }
        }
        if (s_enablePiggybackSetting.Value)
        {
            if (!Character.localCharacter.data.fullyPassedOut && Character.localCharacter.data.isCarried
                && IsCharacterDoingIllegalCarryActions(Character.localCharacter))
                DropPlayerFromCarry(Character.localCharacter);
        }
    }

    private static void DropPlayerFromCarry(Character character)
    {
        if (!(bool)(Object)character.data.carrier) return;
        DropFromCarryDelegate(character.data.carrier.refs.carriying, character);
    }

    private static bool IsCharacterDoingIllegalCarryActions(Character character)
    {
        return character.data.isSprinting
            || character.data.isJumping
            || character.data.isClimbingAnything
            || character.data.isCrouching
            || character.data.isReaching;
    }

    private class CanBeCarriedPatch
    {
        private static readonly Func<GUIManager, IInteractible> GetCurrentInteractable =
            (Func<GUIManager, IInteractible>)Delegate.CreateDelegate(
                typeof(Func<GUIManager, IInteractible>),
                AccessTools.PropertyGetter(typeof(GUIManager), "currentInteractable")
            );

        private static readonly Dictionary<CharacterInteractible, Coroutine> s_refreshCoroutines = new();

        private static IEnumerator RefreshPromptCoroutine(CharacterInteractible interactible)
        {
            while (true)
            {
                if (GetCurrentInteractable(GUIManager.instance) == interactible)
                    GUIManager.instance.RefreshInteractablePrompt();
                yield return null;
            }
        }

        [HarmonyPatch(typeof(CharacterInteractible), "CanBeCarried")]
        [HarmonyPostfix]
        private static void CanBeCarriedPostfix(ref bool __result, Character ___character)
        {
            // If the original method already returned true, skip
            if (__result) return;

            // If this is the local character (the player's character itself), don't allow carrying
            if (___character.IsLocal) return;

            // If the character is dead, don't allow carrying
            if (___character.data.dead) return;

            if (s_swapBackpackSetting.Value)
            {
                // When the SwapBackpack setting is enabled, don't allow carrying if both the local player
                // and the target carry character have a backpack.
                if (Player.localPlayer.backpackSlot.hasBackpack && ___character.player.backpackSlot.hasBackpack) return;
            }
            // If the SwapBackpack setting is disabled, don't allow carrying only if the local player has a backpack
            else if (Player.localPlayer.backpackSlot.hasBackpack) return;

            // If the local player character is climbing anything, don't allow carrying
            if (Character.localCharacter.data.isClimbingAnything) return;

            // If the character is holding an item or doing an illegal action, don't allow carrying
            if ((bool)(Object)___character.data.currentItem || IsCharacterDoingIllegalCarryActions(___character)) return;

            // If the character is carrying another player, don't allow carrying
            if ((bool)(Object)___character.data.carriedPlayer) return;

            // If the character is already being carried, don't allow carrying
            if ((bool)(Object)___character.data.carrier) return;

            // If the local character is holding an item that can be used on friends, don't allow carrying
            if ((bool)(Object)Character.localCharacter.data.currentItem &&
                Character.localCharacter.data.currentItem.canUseOnFriend) return;

            // If the character is cannibalizable (can be eaten), don't allow carrying
            if (___character.refs.customization.isCannibalizable) return;

            __result = true;
        }

        [HarmonyPatch(typeof(CharacterInteractible), "IsInteractible")]
        [HarmonyPostfix]
        private static bool IsInteractablePostfix(bool originalResult, CharacterInteractible __instance, Character interactor)
        {
            if (interactor.data.isCarried || !originalResult) return false;
            if (__instance.character.refs.customization.isCannibalizable) return true;
            if (CarriedByLocalCharacterDelegate(__instance)) return true;
            if (!CanBeCarriedDelegate(__instance)) return true; // If we got here, this is the secondary interactible
            // Is carry interaction
            if (__instance.character.data.fullyPassedOut) return true;
            // Reduce the distance check for carrying (when not passed out)
            return Vector3.Distance(interactor.Center, __instance.character.Center) <= 2f;
        }

        [HarmonyPatch(typeof(CharacterCarrying), "Update")]
        [HarmonyPrefix]
        private static bool CharacterCarryingUpdatePrefix(Character ___character)
        {
            // Note: When true is returned, the original method will run, which will inevitably see that the
            // carried player is not fully passed out and will drop them.
            if (!___character.refs.view.IsMine) return true;
            if (!(bool)(Object)___character.data.carriedPlayer) return true;

            // Drop the carried player if they are holding an item
            if ((bool)(Object)___character.data.carriedPlayer.data.currentItem) return true;
            // Drop the carried player if they are doing illegal carry actions
            if (IsCharacterDoingIllegalCarryActions(___character.data.carriedPlayer))
                return true;

            if (!___character.data.carriedPlayer.data.dead &&
                !___character.data.fullyPassedOut &&
                !___character.data.dead)
                return false;

            return true;
        }

        [HarmonyPatch(typeof(CharacterCarrying), "CarrierGone")]
        [HarmonyPostfix]
        private static void CharacterCarryingCarrierGonePostfix(Character ___character)
        {
            ___character.data.isCarried = false;
        }

        [HarmonyPatch(typeof(CharacterInput), "Sample")]
        [HarmonyPostfix]
        private static void CharacterInputSamplePostfix(CharacterInput __instance)
        {
            if (Character.localCharacter.data.fullyPassedOut || !Character.localCharacter.data.isCarried)
                return;
            __instance.movementInput = Vector2.zero;
            __instance.interactWasPressed = false;
            __instance.interactIsPressed = false;
            __instance.emoteIsPressed = false;
            __instance.sprintWasPressed = false;
            __instance.sprintIsPressed = false;
            __instance.sprintToggleIsPressed = false;
            __instance.sprintToggleWasPressed = false;
            __instance.dropWasPressed = false;
            __instance.dropIsPressed = false;
            __instance.usePrimaryWasPressed = false;
            __instance.usePrimaryIsPressed = false;
            __instance.useSecondaryWasPressed = false;
            __instance.useSecondaryIsPressed = false;
            __instance.spectateLeftWasPressed = false;
            __instance.spectateRightWasPressed = false;
            __instance.selectBackpackWasPressed = false;
            __instance.scrollButtonLeftWasPressed = false;
            __instance.scrollButtonRightWasPressed = false;
            __instance.selectSlotForwardWasPressed = false;
            __instance.selectSlotBackwardWasPressed = false;
        }

        [HarmonyPatch(typeof(CharacterInput), "SelectSlotWasPressed")]
        [HarmonyPostfix]
        private static bool CharacterInputSelectSlotWasPressedPostfix(bool originalResult, CharacterInput __instance)
        {
            if (!Character.localCharacter.data.fullyPassedOut && Character.localCharacter.data.isCarried)
                return false;
            return originalResult;
        }

        [HarmonyPatch(typeof(CharacterInteractible), "HoverEnter")]
        [HarmonyPostfix]
        private static void HoverEnterPostfix(CharacterInteractible __instance)
        {
            if (s_refreshCoroutines.ContainsKey(__instance)) return;
            var coroutine = __instance.StartCoroutine(RefreshPromptCoroutine(__instance));
            s_refreshCoroutines[__instance] = coroutine;
        }

        [HarmonyPatch(typeof(CharacterInteractible), "HoverExit")]
        [HarmonyPostfix]
        private static void HoverExitPostfix(CharacterInteractible __instance)
        {
            if (s_refreshCoroutines.TryGetValue(__instance, out var coroutine))
            {
                __instance.StopCoroutine(coroutine);
                s_refreshCoroutines.Remove(__instance);
            }
        }
    }

    private class SpectateViewPatch
    {
        private static readonly Func<MainCamera, CameraOverride> GetCamOverride = ExpressionUtils.CreateFieldGetter<MainCamera, CameraOverride>("camOverride");
        private static readonly Action<MainCameraMovement> SpectateDelegate =
            (Action<MainCameraMovement>)Delegate.CreateDelegate(
                typeof(Action<MainCameraMovement>),
                null,
                typeof(MainCameraMovement).GetMethod("Spectate", BindingFlags.Instance | BindingFlags.NonPublic)!
            );
        private static readonly Action<Character> SetSpecCharacter =
            (Action<Character>)Delegate.CreateDelegate(
                typeof(Action<Character>),
                AccessTools.PropertySetter(typeof(MainCameraMovement), "specCharacter")
            );

        private static float? m_defaultSpectateZoomMax = null;
        private static float m_customSpectateZoomMax = 3f;

        [HarmonyPatch(typeof(MainCameraMovement), "LateUpdate")]
        [HarmonyPostfix]
        static void MainCameraMovementLateUpdatePostfix(
            MainCameraMovement __instance,
            MainCamera ___cam,
            bool ___isGodCam,
            ref bool ___isSpectating
        ) {
            if (!s_spectateViewSetting.Value) return;
            if (___isGodCam || ___isSpectating) return;
            if (!(bool)(Object)Character.localCharacter) return;
            if (!Character.localCharacter.data.isCarried) return;
            if ((bool)(Object)GetCamOverride(___cam)) return;
            SpectateDelegate(__instance);
            ___isSpectating = true;
        }

        [HarmonyPatch(typeof(MainCameraMovement), "HandleSpecSelection")]
        [HarmonyPrefix]
        static bool HandleSpecSelectionPrefix(ref bool __result, MainCameraMovement __instance)
        {
            if (!Character.localCharacter.data.fullyPassedOut && (bool)(Object)Character.localCharacter.data.carrier)
            {
                if (!(bool)(Object)MainCameraMovement.specCharacter)
                    SetSpecCharacter(Character.localCharacter.data.carrier);
                m_defaultSpectateZoomMax ??= __instance.spectateZoomMax;
                __instance.spectateZoomMax = m_customSpectateZoomMax;
                __result = true;
                return false;
            }
            if (m_defaultSpectateZoomMax.HasValue) __instance.spectateZoomMax = m_defaultSpectateZoomMax.Value;
            return true;
        }
    }

    private class HoldToCarryPatch
    {
        private static readonly Action<CharacterCarrying, Character> StartCarryDelegate =
            (Action<CharacterCarrying, Character>)Delegate.CreateDelegate(
                typeof(Action<CharacterCarrying, Character>),
                null,
                typeof(CharacterCarrying).GetMethod("StartCarry", BindingFlags.Instance | BindingFlags.NonPublic)!
            );

        private static bool s_isAttemptingToCarry = false;

        [HarmonyPatch(typeof(CharacterInteractible), "Interact")]
        [HarmonyPrefix]
        private static bool CharacterInteractibleInteractPrefix(CharacterInteractible __instance, Character interactor)
        {
            s_isAttemptingToCarry = false;
            if (CarriedByLocalCharacterDelegate(__instance)) return true;
            if (__instance.character.refs.customization.isCannibalizable) return true;
            if (__instance.character.data.fullyPassedOut || s_holdToCarrySetting.Value == 0.0f) return true;
            s_isAttemptingToCarry = true;
            return false;
        }

        [HarmonyPatch(typeof(CharacterInteractible), "IsConstantlyInteractable")]
        [HarmonyPostfix]
        private static bool CharacterInteractibleIsConstantlyInteractablePostfix(bool originalResult, CharacterInteractible __instance)
        {
            return originalResult || CanBeCarriedDelegate(__instance);
        }

        [HarmonyPatch(typeof(CharacterInteractible), "GetInteractTime")]
        [HarmonyPostfix]
        private static float CharacterInteractibleGetInteractTimePostfix(float originalResult, CharacterInteractible __instance)
        {
            return __instance.character.refs.customization.isCannibalizable ? originalResult : s_holdToCarrySetting.Value;
        }

        [HarmonyPatch(typeof(CharacterInteractible), "Interact_CastFinished")]
        [HarmonyPrefix]
        private static bool CharacterInteractibleCastFinishedPrefix(CharacterInteractible __instance, Character interactor)
        {
            if (!interactor.IsLocal) return false;
            // If the player is not attempting to carry, meaning it is trying to cannibalize (eat) the character,
            // return true to allow the original method to run.
            if (!s_isAttemptingToCarry) return true;
            if (CanBeCarriedDelegate(__instance))
                StartCarryDelegate(interactor.refs.carriying, __instance.character);
            s_isAttemptingToCarry = false;
            return false;
        }
    }

    private class BackpackSwapOnCarryPatch
    {
        private static bool s_wasBackpackSwapped = false;

        [HarmonyPatch(typeof(CharacterCarrying), "StartCarry")]
        [HarmonyPrefix]
        private static bool StartCarryPrefix(ref Character ___character, Character target)
        {
            if (!s_swapBackpackSetting.Value) return true;
            s_wasBackpackSwapped = false;
            BackpackSlot backpackSlot = ___character.player.backpackSlot;
            if (!backpackSlot.hasBackpack || target.player.backpackSlot.hasBackpack) return true;
            target.player.backpackSlot = backpackSlot;
            ___character.player.backpackSlot = new BackpackSlot(3);
            var characterManagedArray = IBinarySerializable.ToManagedArray(new InventorySyncData(___character.player.itemSlots, ___character.player.backpackSlot, ___character.player.tempFullSlot));
            ___character.player.photonView.RPC("SyncInventoryRPC", RpcTarget.All, characterManagedArray, true);
            var targetManagedArray = IBinarySerializable.ToManagedArray(new InventorySyncData(target.player.itemSlots, target.player.backpackSlot, target.player.tempFullSlot));
            target.player.photonView.RPC("SyncInventoryRPC", RpcTarget.All, targetManagedArray, true);
            s_wasBackpackSwapped = true;
            return true;
        }

        [HarmonyPatch(typeof(CharacterCarrying), "Drop")]
        [HarmonyPostfix]
        private static void DropPostfix(ref Character ___character, Character target)
        {
            if (!s_swapBackpackSetting.Value || !s_wasBackpackSwapped) return;
            BackpackSlot backpackSlot = target.player.backpackSlot;
            if (!backpackSlot.hasBackpack || ___character.player.backpackSlot.hasBackpack) return;
            ___character.player.backpackSlot = backpackSlot;
            target.player.backpackSlot = new BackpackSlot(3);
            var targetManagedArray = IBinarySerializable.ToManagedArray(new InventorySyncData(target.player.itemSlots, target.player.backpackSlot, target.player.tempFullSlot));
            target.player.photonView.RPC("SyncInventoryRPC", RpcTarget.All, targetManagedArray, true);
            var characterManagedArray = IBinarySerializable.ToManagedArray(new InventorySyncData(___character.player.itemSlots, ___character.player.backpackSlot, ___character.player.tempFullSlot));
            ___character.player.photonView.RPC("SyncInventoryRPC", RpcTarget.All, characterManagedArray, true);
        }
    }
}
