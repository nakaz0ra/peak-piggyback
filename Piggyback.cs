using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Piggyback;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Piggyback : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    private readonly Harmony harmony = new(MyPluginInfo.PLUGIN_GUID);

    private void Awake()
    {
        Logger = base.Logger;
        harmony.PatchAll(typeof(CanBeCarriedPatch));
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_NAME} loaded");
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

            // If the local player has a backpack, don't allow carrying
            if (!Player.localPlayer.backpackSlot.IsEmpty()) return;

            // If the local character is holding an item that can be used on friends, don't allow carrying
            if ((bool)(Object)Character.localCharacter.data.currentItem &&
                Character.localCharacter.data.currentItem.canUseOnFriend) return;

            if (!___character.data.dead && !(bool)(Object)___character.data.carrier)
            {
                __result = true;
            }
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
}
