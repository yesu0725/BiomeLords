using System.Collections.Generic;

namespace BiomeLords.Util
{
    /// <summary>
    /// Per-Lord hard-coded intrinsic multipliers, applied on TOP of the
    /// tier-based TierTable scaling AND the user-facing LordConfig multipliers.
    ///
    /// Use this when a specific Lord needs a baked-in balance tweak that
    /// should NOT affect any other Lord and should NOT be expressed as a
    /// config default (so user configs don't drift when we re-tune).
    ///
    /// Runtime-editable via the biomelords_intrinsic console command, which
    /// rewrites the dictionary in-place. Defaults are recorded in
    /// Defaults so the command can restore them.
    /// </summary>
    public static class LordIntrinsic
    {
        // Damage. 1.0 = no intrinsic change. 2.0 = doubled.
        // Edits at runtime are persisted only for the current session — these
        // baked numbers are what the next launch starts from.
        // All neutralized to 1.0: Lord attack damage is now set directly from
        // LordAttackProfile (matched to each biome's vanilla boss), so no
        // per-Lord intrinsic amplification is applied. Kept as a live-tuning
        // surface (biomelords_intrinsic) for ad-hoc balance experiments.
        private static readonly Dictionary<string, float> DamageMult =
            new Dictionary<string, float>
            {
                { "neck_lord",      1.0f },
                { "greydwarf_lord", 1.0f },
                { "draugr_lord",    1.0f },
                { "fenring_lord",   1.0f },
                { "lox_lord",       1.0f },
                { "seeker_lord",    1.0f },
                { "faller_valkyrie_lord", 1.0f },
            };

        /// <summary>Snapshot of the original values so the console command can
        /// restore defaults after live tuning.</summary>
        private static readonly Dictionary<string, float> Defaults =
            new Dictionary<string, float>(DamageMult);

        public static float DamageMultiplier(string lordId)
        {
            if (lordId == null) return 1f;
            return DamageMult.TryGetValue(lordId, out var v) ? v : 1f;
        }

        // ---- Runtime tuning surface --------------------------------------

        /// <summary>All lord ids currently recognised (in stable defaults order).</summary>
        public static IEnumerable<string> Ids => Defaults.Keys;

        /// <summary>True if the id has a recorded default.</summary>
        public static bool IsKnown(string lordId) =>
            !string.IsNullOrEmpty(lordId) && Defaults.ContainsKey(lordId);

        /// <summary>Replace the intrinsic for one lord. Returns the old value,
        /// or -1 if the lord id is unknown.</summary>
        public static float Set(string lordId, float value)
        {
            if (!IsKnown(lordId)) return -1f;
            float before = DamageMult.TryGetValue(lordId, out var v) ? v : 1f;
            DamageMult[lordId] = value;
            return before;
        }

        /// <summary>Restore one lord to its baked default. Returns true if the
        /// id was known.</summary>
        public static bool Reset(string lordId)
        {
            if (!Defaults.TryGetValue(lordId, out var def)) return false;
            DamageMult[lordId] = def;
            return true;
        }

        /// <summary>Restore every lord to its baked default. Returns count.</summary>
        public static int ResetAll()
        {
            foreach (var kv in Defaults) DamageMult[kv.Key] = kv.Value;
            return Defaults.Count;
        }

        /// <summary>Get the original/default value (pre-tuning), or 1.0 if unknown.</summary>
        public static float DefaultFor(string lordId) =>
            Defaults.TryGetValue(lordId, out var v) ? v : 1f;
    }
}
