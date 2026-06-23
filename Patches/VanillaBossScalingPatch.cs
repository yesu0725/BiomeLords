using System.Collections.Generic;
using HarmonyLib;
using BiomeLords.Util;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Scales vanilla Forsaken bosses whenever they spawn, using the same
    /// Lord-defeat tier progression as the Biome Lords themselves.
    ///
    /// effectiveTier = max(boss.nativeTier, LordDefeatStore.HighestDefeatedTier())
    ///
    /// HP is set directly to TierTable.HpFor(effectiveTier).
    /// Damage uses a relative multiplier (effectiveMult / nativeMult) so we don't
    /// double-scale damage that is already at boss level in the vanilla prefab.
    ///
    /// No changes are made when no Lords have been killed (effectiveTier == nativeTier).
    /// </summary>
    [HarmonyPatch(typeof(Character), "Start")]
    public static class VanillaBossScalingPatch
    {
        // Vanilla Forsaken prefab name → native tier.
        // Names use the GameObject prefab name (gameObject.name with "(Clone)" stripped).
        private static readonly Dictionary<string, int> NativeTierByPrefab =
            new Dictionary<string, int>
            {
                { "Eikthyr",      1 },
                { "gd_king",      2 },
                { "Bonemass",     3 },
                { "Dragon",       4 },
                { "GoblinKing",   5 },
                { "SeekerQueen",  6 },
                { "FallenValkyrie", 7 },
            };

        [HarmonyPostfix]
        public static void Postfix(Character __instance)
        {
            if (__instance == null || !__instance.m_boss) return;

            string prefabName = StripClone(__instance.gameObject.name);
            if (!NativeTierByPrefab.TryGetValue(prefabName, out int nativeTier)) return;

            int highestLordTier = LordDefeatStore.HighestDefeatedTier();
            int effectiveTier   = System.Math.Max(nativeTier, highestLordTier);
            if (effectiveTier == nativeTier) return; // no Lords killed yet — leave vanilla untouched

            // HP: override to tier-table target (table was calibrated from vanilla boss HP).
            float newHp = TierTable.HpFor(effectiveTier);
            var humanoid = __instance.GetComponent<Humanoid>();
            if (humanoid != null)
            {
                humanoid.SetMaxHealth(newHp);
                humanoid.SetHealth(newHp);
            }

            // Damage: scale the boss's own attacks UP to the effective tier's boss
            // magnitude — the same target the Lords converge to — applied as a
            // uniform multiplier so the boss keeps its own damage and elemental
            // types and only the values increase.
            // e.g. Eikthyr (tier-1 magnitude 20) scaled to tier 3 (Bonemass magnitude 130):
            //      mult = 130 / 20 = ×6.5  (20 pierce → 130 pierce, 15 lightning → 97.5)
            float nativeMag = LordAttackProfile.TierMagnitude(nativeTier);
            float effMag    = LordAttackProfile.TierMagnitude(effectiveTier);
            float dmgMult   = nativeMag > 0f ? effMag / nativeMag : 1f;
            LordDamageRegistry.Set(__instance, dmgMult);

            Jotunn.Logger.LogInfo(
                $"[BiomeLords] Vanilla boss scaled: {prefabName} nativeTier={nativeTier} " +
                $"effectiveTier={effectiveTier} HP={newHp:F0} dmgMult=×{dmgMult:F2} " +
                $"(magnitude {nativeMag:F0}→{effMag:F0})");
        }

        private static string StripClone(string n)
        {
            if (string.IsNullOrEmpty(n)) return n;
            const string suffix = "(Clone)";
            return n.EndsWith(suffix) ? n.Substring(0, n.Length - suffix.Length) : n;
        }
    }
}
