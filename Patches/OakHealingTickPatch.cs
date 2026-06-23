using HarmonyLib;
using BiomeLords.Phase1D;

namespace BiomeLords.Patches
{
    /// <summary>Drives all Player.Update ticks (Forest's Embrace + marker-FP effects).</summary>
    [HarmonyPatch(typeof(Player), "Update")]
    public static class Player_Update_BiomeLordsTicks
    {
        [HarmonyPostfix]
        public static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;
            ForestEmbraceService.Tick();
            PowerEffectsService.Tick();
        }
    }
}
