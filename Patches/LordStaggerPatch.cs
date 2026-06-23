using HarmonyLib;
using BiomeLords.Util;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Lords ignore stagger — they should never be interrupted by a player flinch.
    /// Patches Character.Stagger to early-return when the target is a registered Lord.
    /// </summary>
    [HarmonyPatch(typeof(Character), "Stagger")]
    public static class Character_Stagger_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Character __instance)
        {
            // Returning false skips vanilla Stagger entirely.
            return !RegisteredLords.IsLord(__instance);
        }
    }
}
