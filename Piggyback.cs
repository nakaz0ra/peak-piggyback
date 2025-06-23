using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Piggyback;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Piggyback : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;

    private static ConfigEntry<bool> s_spectateViewSetting;
    private static ConfigEntry<float> s_holdToCarrySetting;
    private static bool s_isHoldToCarryPatchApplied = false;

    private readonly Harmony m_harmony = new(MyPluginInfo.PLUGIN_GUID);

    private void Awake()
    {
        Logger = base.Logger;

        SetupConfig();

        m_harmony.PatchAll(typeof(CanBeCarriedPatch));
        m_harmony.PatchAll(typeof(SpectateViewPatch));
        if (s_holdToCarrySetting.Value > 0.0f)
        {
            m_harmony.PatchAll(typeof(HoldToCarryPatch));
            s_isHoldToCarryPatchApplied = true;
        }

        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_NAME} loaded");
    }

    private void SetupConfig()
    {
        s_spectateViewSetting = Config.Bind("General", "SpectateView", true,
            new ConfigDescription(
                "If true, the camera will switch to the spectate view while being carried. " +
                "If false, the camera will remain in the first-person view while being carried. " +
                "Note that you'll only be able to spectate the player carrying you."));
        s_holdToCarrySetting = Config.Bind("General", "HoldToCarryTime", 1.5f,
            new ConfigDescription(
                "The time in seconds you need to hold the interact button to start carrying another player. " +
                "If 0, you will start carrying the player immediately upon pressing the interact button. " +
                "If the player is passed out, you will start carrying them immediately even if this is set to a " +
                "value greater than 0.", new AcceptableValueRange<float>(0.0f, 5.0f)));

        s_holdToCarrySetting.SettingChanged += (sender, args) =>
        {
            if (s_holdToCarrySetting.Value <= 0.0f) return;
            if (s_isHoldToCarryPatchApplied)
            {
                Logger.LogInfo($"Hold to Carry time set to {s_holdToCarrySetting.Value} seconds.");
            }
            else
            {
                m_harmony.PatchAll(typeof(HoldToCarryPatch));
                s_isHoldToCarryPatchApplied = true;
                Logger.LogInfo($"Hold to Carry enabled, time set to {s_holdToCarrySetting.Value} seconds.");
            }
        };
    }

    private static bool IsCharacterDoingIllegalCarryActions(Character character)
    {
        return character.data.isSprinting
            || character.data.isJumping
            || character.data.isClimbing
            || character.data.isRopeClimbing
            || character.data.isVineClimbing
            || character.data.isCrouching
            || character.data.isReaching;
    }

    private class CanBeCarriedPatch
    {
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

            // If the local player has a backpack, don't allow carrying
            if (!Player.localPlayer.backpackSlot.IsEmpty()) return;
            // if (!Player.localPlayer.backpackSlot.IsEmpty() && !___character.player.backpackSlot.IsEmpty()) return;

            // If the local player character is climbing or rope/vine climbing, don't allow carrying
            if (Character.localCharacter.data.isClimbing
                || Character.localCharacter.data.isRopeClimbing
                || Character.localCharacter.data.isVineClimbing)
                return;

            // If the character is holding an item or doing an illegal action, don't allow carrying
            if ((bool)(Object)___character.data.currentItem || IsCharacterDoingIllegalCarryActions(___character)) return;

            // If the character is carrying another player, don't allow carrying
            if ((bool)(Object)___character.data.carriedPlayer) return;

            // If the character is already being carried, don't allow carrying
            if ((bool)(Object)___character.data.carrier) return;

            // If the local character is holding an item that can be used on friends, don't allow carrying
            if ((bool)(Object)Character.localCharacter.data.currentItem &&
                Character.localCharacter.data.currentItem.canUseOnFriend) return;

            __result = true;
        }

        [HarmonyPatch(typeof(CharacterInteractible), "IsInteractible")]
        [HarmonyPostfix]
        private static bool IsInteractablePostfix(bool originalResult, CharacterInteractible __instance, Character interactor)
        {
            if (__instance.character.data.fullyPassedOut) return originalResult;

            // Reduce the distance check for carrying (when not passed out)
            float distance = Vector3.Distance(interactor.Center, __instance.character.Center);
            if (distance > 2f) return __instance.IsSecondaryInteractible(interactor);

            return originalResult;
        }

        [HarmonyPatch(typeof(CharacterCarrying), "Update")]
        [HarmonyPrefix]
        private static bool CharacterCarryingUpdatePrefix(Character ___character)
        {
            // Note: When we return true, the original method will run, which will inevitably see that the
            // carried player is not fully passed out and will drop them.
            if (___character.IsLocal && (bool)(Object)___character.data.carrier)
            {
                if (!___character.data.fullyPassedOut && IsCharacterDoingIllegalCarryActions(___character))
                {
                    ___character.data.carrier.photonView.RPC("RPCA_Drop", RpcTarget.All, ___character.photonView);
                    return false;
                }
                return true;
            }

            if (!(bool)(Object)___character.data.carriedPlayer) return true;

            // Drop the carried player if they are holding an item
            if ((bool)(Object)___character.data.carriedPlayer.data.currentItem) return true;
            // Drop the carried player if they are doing illegal carry actions unless the player carrying them is climbing
            if (IsCharacterDoingIllegalCarryActions(___character.data.carriedPlayer) &&
                !(___character.data.isClimbing || ___character.data.isRopeClimbing || ___character.data.isVineClimbing))
                return true;

            if (!___character.data.carriedPlayer.data.dead &&
                !___character.input.selectBackpackWasPressed && !___character.data.fullyPassedOut &&
                !___character.data.dead || !___character.refs.view.IsMine)
                return false;

            return true;
        }

        [HarmonyPatch(typeof(CharacterInput), "Sample")]
        [HarmonyPostfix]
        private static void CharacterInputSamplePostfix(CharacterInput __instance)
        {
            if (Character.localCharacter.data.fullyPassedOut || !(bool)(Object)Character.localCharacter.data.carrier)
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
            if (!Character.localCharacter.data.fullyPassedOut && (bool)(Object)Character.localCharacter.data.carrier)
                return false;
            return originalResult;
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
        static bool HandleSpecSelectionPrefix(ref bool __result)
        {
            if (!Character.localCharacter.data.fullyPassedOut && (bool)(Object)Character.localCharacter.data.carrier)
            {
                if (!(bool)(Object)MainCameraMovement.specCharacter)
                    SetSpecCharacter(Character.localCharacter.data.carrier);
                __result = true;
                return false;
            }
            return true;
        }
    }

    private class HoldToCarryPatch
    {
        private static readonly Func<CharacterInteractible, bool> CarriedByLocalCharacterDelegate =
            (Func<CharacterInteractible, bool>)Delegate.CreateDelegate(
                typeof(Func<CharacterInteractible, bool>),
                null,
                typeof(CharacterInteractible).GetMethod("CarriedByLocalCharacter", BindingFlags.Instance | BindingFlags.NonPublic)!
            );

        [HarmonyPatch(typeof(CharacterInteractible), "Start")]
        [HarmonyPostfix]
        private static void ReplaceCharacterInteractibleComponent(CharacterInteractible __instance)
        {
            var go = __instance.gameObject;
            if (go.GetComponent<CharacterInteractibleConstant>() != null) return;
            var newComp = go.AddComponent<CharacterInteractibleConstant>();
            newComp.character = __instance.character;
            Destroy(__instance);
        }

        [HarmonyPatch(typeof(CharacterInteractible), "Interact")]
        [HarmonyPrefix]
        private static bool CharacterInteractibleInteractPrefix(CharacterInteractible __instance, Character interactor)
        {
            return CarriedByLocalCharacterDelegate(__instance)
                   || __instance.character.data.fullyPassedOut
                   || s_holdToCarrySetting.Value == 0.0f;
        }
    }

    public class CharacterInteractibleConstant : CharacterInteractible, IInteractibleConstant
    {
        private static readonly Func<CharacterInteractible, bool> CanBeCarriedDelegate =
            (Func<CharacterInteractible, bool>)Delegate.CreateDelegate(
                typeof(Func<CharacterInteractible, bool>),
                null,
                typeof(CharacterInteractible).GetMethod("CanBeCarried", BindingFlags.Instance | BindingFlags.NonPublic)!
            );
        private static readonly Action<CharacterCarrying, Character> StartCarryDelegate =
            (Action<CharacterCarrying, Character>)Delegate.CreateDelegate(
                typeof(Action<CharacterCarrying, Character>),
                null,
                typeof(CharacterCarrying).GetMethod("StartCarry", BindingFlags.Instance | BindingFlags.NonPublic)!
            );

        private void Start()
        {
            Logger.LogInfo($"CharacterInteractibleConstant added to {character}");
        }

        public bool IsConstantlyInteractable(Character interactor)
        {
            return CanBeCarriedDelegate(this);
        }

        public float GetInteractTime(Character interactor) => s_holdToCarrySetting.Value;

        public void Interact_CastFinished(Character interactor)
        {
            if (CanBeCarriedDelegate(this))
                StartCarryDelegate(interactor.refs.carriying, character);
        }

        public void CancelCast(Character interactor) {}

        public void ReleaseInteract(Character interactor) {}

        public bool holdOnFinish => false;
    }
}
