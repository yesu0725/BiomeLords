namespace BiomeLords.Util
{
    /// <summary>
    /// Boss-tier HP reference table. Vanilla boss HP per tier — the progression
    /// curve. Used as the HP target for scaled vanilla bosses and as the ratio
    /// basis for Lord HP.
    ///
    /// Tier mapping:
    ///   1 = Meadows (Eikthyr)
    ///   2 = Black Forest (Elder)
    ///   3 = Swamp (Bonemass)
    ///   4 = Mountain (Moder)
    ///   5 = Plains (Yagluth)
    ///   6 = Mistlands (Queen)
    ///   7 = Ashlands (Fader)
    ///
    /// Damage scaling no longer lives here — Lords converge via LordAttackProfile
    /// and vanilla bosses scale to LordAttackProfile.TierMagnitude.
    /// </summary>
    public static class TierTable
    {
        public const int MinTier = 1;
        public const int MaxTier = 7;

        private static readonly float[] HpByTier =
        {
            0f,      // tier 0 unused
              500f,  // 1 Eikthyr
             2500f,  // 2 The Elder
             5000f,  // 3 Bonemass
             7500f,  // 4 Moder
            10000f,  // 5 Yagluth
            12500f,  // 6 The Queen
            25000f,  // 7 Fader
        };

        public static int Clamp(int tier) => tier < MinTier ? MinTier : (tier > MaxTier ? MaxTier : tier);

        public static float HpFor(int tier) => HpByTier[Clamp(tier)];
    }
}
