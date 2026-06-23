using HarmonyLib;
using BiomeLords.Config;
using BiomeLords.Data;
using BiomeLords.Util;

namespace BiomeLords.Patches
{
    /// <summary>
    /// When ANY Lord creature wakes (Horn-summoned, cheat-spawned, world-loaded
    /// from a previous session), bake its convergence-resolved attack profile and
    /// config damage mult into the registries. Without this, Lords that bypass
    /// SummonService.ApplyScaling (cheat spawn, save+reload mid-fight) would
    /// silently fall back to vanilla damage and feel un-elite.
    ///
    /// Uses the same resolver and progression source (LordDefeatStore) as
    /// SummonService so every spawn path converges identically.
    /// </summary>
    [HarmonyPatch(typeof(Character), "Awake")]
    public static class Character_Awake_LordAutoRegister
    {
        [HarmonyPostfix]
        public static void Postfix(Character __instance)
        {
            if (__instance == null) return;
            var lordId = RegisteredLords.LordIdFor(__instance);
            if (lordId == null) return;

            // Already registered (Horn-spawned via SummonService) — skip.
            if (LordDamageRegistry.Get(__instance, fallback: -1f) >= 0f) return;

            var def = LordRegistry.ById(lordId);
            if (def == null) return;

            int effectiveTier = System.Math.Max(def.Tier, LordDefeatStore.HighestDefeatedTier());
            DamageProfile profile = LordAttackProfile.Resolve(def.Id, def.Tier, effectiveTier);

            float cfgMult       = LordConfig.DamageMultiplier(def.Id);
            float intrinsicMult = LordIntrinsic.DamageMultiplier(def.Id);
            float dmgMult       = cfgMult * intrinsicMult;

            LordDamageRegistry.Set(__instance, dmgMult);
            LordProfileRegistry.Set(__instance, profile);

            if (LordConfig.DebugLogging != null && LordConfig.DebugLogging.Value)
                Jotunn.Logger.LogInfo(
                    $"[BiomeLords] Auto-registered {def.Id} on Awake: " +
                    $"effectiveTier={effectiveTier} profile@tier{effectiveTier} " +
                    $"dmgMult=×{dmgMult:F2} (cfg×{cfgMult:F2} intrinsic×{intrinsicMult:F2})");
        }
    }
}
