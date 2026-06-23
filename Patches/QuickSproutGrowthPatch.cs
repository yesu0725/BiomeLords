using HarmonyLib;
using UnityEngine;
using BiomeLords.Phase1C;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Quick Sprout blessing — when the local player has the Greydwarf Lord
    /// blessing active, planted crops within 30 m grow ~30 % faster.
    /// </summary>
    [HarmonyPatch(typeof(Plant), "GetGrowTime")]
    public static class Plant_GetGrowTime_QuickSprout
    {
        private const float NearbyRadiusSqr = 20f * 20f;
        private const float GrowFactor      = 0.77f; // ~30 % faster (1 / 1.3)

        private static int _spiritHash;

        [HarmonyPostfix]
        public static void Postfix(Plant __instance, ref float __result)
        {
            if (__instance == null) return;
            var p = Player.m_localPlayer;
            if (p == null || p.IsDead()) return;

            if (_spiritHash == 0)
                _spiritHash = StatusEffectFactory.GreydwarfLordSpiritSE.GetStableHashCode();

            var seman = p.GetSEMan();
            if (seman == null || !seman.HaveStatusEffect(_spiritHash)) return;
            if ((p.transform.position - __instance.transform.position).sqrMagnitude > NearbyRadiusSqr) return;

            __result *= GrowFactor;
        }
    }
}
