using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Object = UnityEngine.Object;

namespace Piggyback;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Piggyback : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;

    private static ConfigEntry<float> s_holdToCarrySetting;
    private static bool s_isHoldToCarryPatchApplied = false;

    private readonly Harmony m_harmony = new(MyPluginInfo.PLUGIN_GUID);

    private void Awake()
    {
        Logger = base.Logger;

        SetupConfig();

        m_harmony.PatchAll(typeof(CanBeCarriedPatch));
        if (s_holdToCarrySetting.Value > 0.0f)
        {
            m_harmony.PatchAll(typeof(HoldToCarryPatch));
            s_isHoldToCarryPatchApplied = true;
        }

        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_NAME} loaded");
    }

    private void SetupConfig()
    {
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

    class CanBeCarriedPatch
    {
        [HarmonyPatch(typeof(CharacterInteractible), "CanBeCarried")]
        [HarmonyPostfix]
        static void CanBeCarriedPostfix(ref bool __result, ref Character ___character)
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

            // If the character is carrying another player, don't allow carrying
            if ((bool)(Object)___character.data.carriedPlayer) return;

            // If the character is already being carried, don't allow carrying
            if ((bool)(Object)___character.data.carrier) return;

            // If the local character is holding an item that can be used on friends, don't allow carrying
            if ((bool)(Object)Character.localCharacter.data.currentItem &&
                Character.localCharacter.data.currentItem.canUseOnFriend) return;

            __result = true;
        }

        [HarmonyPatch(typeof(CharacterCarrying), "Update")]
        [HarmonyPrefix]
        static bool CharacterCarryingUpdatePrefix(ref Character ___character)
        {
            if (!(bool)(Object)___character.data.carriedPlayer) return true;

            if (!___character.data.carriedPlayer.data.dead &&
                !___character.input.selectBackpackWasPressed && !___character.data.fullyPassedOut &&
                !___character.data.dead || !___character.refs.view.IsMine)
                return false;

            return true;
        }
    }

    class HoldToCarryPatch
    {
        private static readonly Func<CharacterInteractible, bool> CarriedByLocalCharacterDelegate =
            (Func<CharacterInteractible, bool>)Delegate.CreateDelegate(
                typeof(Func<CharacterInteractible, bool>),
                null,
                typeof(CharacterInteractible).GetMethod("CarriedByLocalCharacter", BindingFlags.Instance | BindingFlags.NonPublic)!
            );

        [HarmonyPatch(typeof(CharacterInteractible), "Start")]
        [HarmonyPostfix]
        static void ReplaceCharacterInteractibleComponent(CharacterInteractible __instance)
        {
            var go = __instance.gameObject;
            if (go.GetComponent<CharacterInteractibleConstant>() != null) return;
            var newComp = go.AddComponent<CharacterInteractibleConstant>();
            newComp.character = __instance.character;
            Destroy(__instance);
        }

        [HarmonyPatch(typeof(CharacterInteractible), "Interact")]
        [HarmonyPrefix]
        static bool CharacterInteractibleInteractPrefix(CharacterInteractible __instance, Character interactor)
        {
            return CarriedByLocalCharacterDelegate(__instance)
                   || __instance.character.data.fullyPassedOut
                   || s_holdToCarrySetting.Value == 0.0f;
        }
    }

    class CharacterInteractibleConstant : CharacterInteractible, IInteractibleConstant
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
