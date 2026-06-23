using System.Collections.Generic;

namespace BiomeLords.Util
{
    /// <summary>
    /// Per-Lord base HP at the Lord's own (native) tier — its identity value,
    /// matched to the biome's vanilla boss. Progression scaling (effectiveTier)
    /// is layered on top in SummonService.ApplyScaling using the TierTable curve
    /// as a ratio, exactly like the vanilla-boss scaling.
    ///
    /// All 7 Lords are boss-backed, so this equals TierTable.HpFor(lordTier).
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
            };

        /// <summary>Base HP for a Lord, falling back to the tier curve if unmapped.</summary>
        public static float HpFor(string lordId, int tier)
        {
            if (!string.IsNullOrEmpty(lordId) && BaseHp.TryGetValue(lordId, out var hp))
                return hp;
            return TierTable.HpFor(tier);
        }
    }
}
