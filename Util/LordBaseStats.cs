using System.Collections.Generic;
using BiomeLords.Config;

namespace BiomeLords.Util
{
    /// <summary>
    /// Per-Lord base HP at the Lord's own (native) tier — its identity value,
    /// matched to the biome's vanilla boss. Progression scaling (effectiveTier)
    /// is layered on top in SummonService.ApplyScaling using the TierTable curve
    /// as a ratio, exactly like the vanilla-boss scaling.
    ///
    /// All 8 Lords are boss-backed, so this equals TierTable.HpFor(lordTier).
    ///
    /// Admins can override the baseline per Lord via LordStats.BaseHealth
    /// (LordConfig.BaseHealth); the table below only supplies the defaults.
    /// </summary>
    public static class LordBaseStats
    {
        private static readonly Dictionary<string, float> BaseHp =
            new Dictionary<string, float>
            {
                { "neck_lord",       500f },  // Eikthyr
                { "greydwarf_lord", 2500f },  // The Elder
                { "draugr_lord",    5000f },  // Bonemass
                { "fenring_lord",   7500f },  // Moder
                { "lox_lord",      10000f },  // Yagluth
                { "seeker_lord",   12500f },  // The Queen
                { "faller_valkyrie_lord", 25000f },  // Fallen Valkyrie
                { "gammeltroll_lord",     47000f },  // Kall Fimbulbringer, all phases
            };

        /// <summary>Design default base HP for a Lord, falling back to the tier curve if unmapped.
        /// Used as the config default — read HpFor() for the live value.</summary>
        public static float DefaultHpFor(string lordId, int tier)
        {
            if (!string.IsNullOrEmpty(lordId) && BaseHp.TryGetValue(lordId, out var hp))
                return hp;
            return TierTable.HpFor(tier);
        }

        /// <summary>Base HP for a Lord: the admin-configured LordStats.BaseHealth
        /// value when set (> 0), otherwise the design default.</summary>
        public static float HpFor(string lordId, int tier)
        {
            float cfg = LordConfig.BaseHealth(lordId);
            return cfg > 0f ? cfg : DefaultHpFor(lordId, tier);
        }
    }
}
