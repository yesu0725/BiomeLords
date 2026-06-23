using HarmonyLib;
using BiomeLords.Phase1D;

namespace BiomeLords.Patches
{
    /// <summary>
    /// While Forest's Embrace is active and the player has been sitting near
    /// a qualifying tree for 60 s with no monsters within 30 m, bump
    /// Player.GetComfortLevel by the tree's comfort tier so vanilla
    /// automatically grants a longer Rested buff.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.GetComfortLevel))]
    public static class Player_GetComfortLevel_ForestsEmbrace
    {
        [HarmonyPostfix]
        public static void Postfix(Player __instance, ref int __result)
        {
            if (__instance != Player.m_localPlayer) return;
            int bonus = ForestEmbraceService.CurrentTreeComfort;
            if (bonus <= 0) return;
            __result += bonus;
        }
    }

    /// <summary>
    /// Vanilla Rested requires shelter (a roof overhead). Trees in the wild
    /// rarely qualify — under their canopy is open sky. While Forest's Embrace
    /// has granted tree-comfort, force InShelter to return true so the
    /// natural canopy "counts" and Rested actually applies.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.InShelter))]
    public static class Player_InShelter_ForestsEmbrace
    {
        [HarmonyPostfix]
        public static void Postfix(Player __instance, ref bool __result)
        {
            if (__instance != Player.m_localPlayer) return;
            if (__result) return; // already sheltered
            // Shelter kicks in the moment you're near any tree under Forest's Embrace,
            // independent of the 60s sit-for-Rested gate.
            if (ForestEmbraceService.IsNearQualifyingTree) __result = true;
        }
    }
}
