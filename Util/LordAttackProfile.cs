using System.Collections.Generic;

namespace BiomeLords.Util
{
    /// <summary>One attack's damage split, mirroring HitData.DamageTypes
    /// (combat components only — chop/pickaxe are left untouched on the hit).</summary>
    public struct DamageProfile
    {
        public float Blunt, Slash, Pierce, Fire, Frost, Lightning, Poison, Spirit;
    }

    /// <summary>
    /// Per-Lord melee damage profile, matched to each Lord's own cloned vanilla
    /// creature instead of its biome's vanilla boss. "Single blended profile" —
    /// every swing a Lord makes deals this exact split, replacing the cloned
    /// creature's native weapon damage at hit time (see Character_Damage_LordBoost).
    ///
    /// Neck Lord and Greydwarf Lord run their vanilla creature's strongest
    /// signature attack at +20% (the creature's own 0★ damage × 1.2). Draugr
    /// Lord through Fallen Valkyrie Lord run that same signature attack
    /// unmodified (×1.0) — a deliberate split agreed per-Lord rather than a
    /// flat rule across all seven. No vanilla boss values are used anywhere
    /// in this table.
    /// </summary>
    public static class LordAttackProfile
    {
        private static readonly Dictionary<string, DamageProfile> Profiles =
            new Dictionary<string, DamageProfile>
            {
                // Neck Lord       <- Neck            : bite 5 slash (0★) x1.2 = 6
                { "neck_lord",      new DamageProfile { Slash = 6f } },
                // Greydwarf Lord  <- Greydwarf_Shaman : scratch 14 slash (0★) x1.2 = 16.8 -> 17
                { "greydwarf_lord", new DamageProfile { Slash = 17f } },
                // Draugr Lord     <- Draugr_Elite     : sword slash 58 slash (0★), unmodified
                { "draugr_lord",    new DamageProfile { Slash = 58f } },
                // Fenring Lord    <- Fenring          : claw scratch 85 slash (0★), unmodified
                { "fenring_lord",   new DamageProfile { Slash = 85f } },
                // Lox Lord        <- Lox              : bite 130 slash (0★), unmodified
                { "lox_lord",       new DamageProfile { Slash = 130f } },
                // Seeker Lord     <- Seeker           : claw thrust 120 pierce (0★), unmodified
                { "seeker_lord",    new DamageProfile { Pierce = 120f } },
                // Fallen Valkyrie Lord <- FallenValkyrie : claw strike 160 pierce (0★), unmodified
                { "faller_valkyrie_lord", new DamageProfile { Pierce = 160f } },
            };

        public static bool TryGet(string lordId, out DamageProfile profile)
        {
            if (!string.IsNullOrEmpty(lordId) && Profiles.TryGetValue(lordId, out profile))
                return true;
            profile = default;
            return false;
        }

        /// <summary>
        /// Convergence target magnitude per tier (index = tier 1..7), replacing
        /// the old vanilla-boss table. Each value is that tier's own Lord
        /// profile total (see Profiles above). Magnitude is stored under Blunt
        /// purely as a carrier field — Resolve() below sums Blunt+Slash+Pierce
        /// to get the target magnitude and applies it to the converging Lord's
        /// OWN physical type, so which field holds the number here doesn't matter.
        /// </summary>
        private static readonly DamageProfile[] ByTier =
        {
            default,                       // 0 unused
            new DamageProfile { Blunt = 6f   },  // 1 Neck Lord
            new DamageProfile { Blunt = 17f  },  // 2 Greydwarf Lord
            new DamageProfile { Blunt = 58f  },  // 3 Draugr Lord
            new DamageProfile { Blunt = 85f  },  // 4 Fenring Lord
            new DamageProfile { Blunt = 130f },  // 5 Lox Lord
            new DamageProfile { Blunt = 120f },  // 6 Seeker Lord
            new DamageProfile { Blunt = 160f },  // 7 Fallen Valkyrie Lord
        };

        public static bool TryGetByTier(int tier, out DamageProfile profile)
        {
            if (tier >= 1 && tier <= 7) { profile = ByTier[tier]; return true; }
            profile = default;
            return false;
        }

        /// <summary>
        /// Total combat damage of a tier's boss signature attack — the magnitude
        /// the Lords converge to at that tier. Used to scale vanilla bosses to the
        /// same per-tier magnitude while preserving their own damage types.
        /// </summary>
        public static float TierMagnitude(int tier)
        {
            if (!TryGetByTier(tier, out var p)) return 0f;
            return p.Blunt + p.Slash + p.Pierce
                 + p.Fire + p.Frost + p.Lightning + p.Poison + p.Spirit;
        }

        /// <summary>
        /// Resolve the attack profile a Lord should use for the given progression.
        ///
        /// At the native tier the Lord keeps its own signature attack. Once the
        /// effective tier is higher, it converges toward that tier's target
        /// magnitude (ByTier) while keeping its own identity: the Lord's
        /// physical TYPE is preserved, but its value follows the higher tier's
        /// magnitude. Any elemental components (only ever present via a runtime
        /// addition like Greydwarf's Poison Nova, not from this static table)
        /// still stack on top.
        ///
        /// Example: Neck Lord (6 slash, tier 1) converging to tier 3 (58
        /// magnitude) becomes 58 slash.
        /// </summary>
        public static DamageProfile Resolve(string lordId, int nativeTier, int effectiveTier)
        {
            TryGet(lordId, out var own);

            if (effectiveTier <= nativeTier || !TryGetByTier(effectiveTier, out var tierTarget))
                return own;

            // Tier elemental (always 0 from this table) added on top of the Lord's own elemental.
            var result = new DamageProfile
            {
                Fire      = own.Fire      + tierTarget.Fire,
                Frost     = own.Frost     + tierTarget.Frost,
                Lightning = own.Lightning + tierTarget.Lightning,
                Poison    = own.Poison    + tierTarget.Poison,
                Spirit    = own.Spirit    + tierTarget.Spirit,
            };

            // Lord's physical TYPE kept; value follows the tier's target magnitude,
            // falling back to the Lord's own value when the tier has none.
            float tierPhys = tierTarget.Blunt + tierTarget.Slash + tierTarget.Pierce;
            if (own.Pierce >= own.Blunt && own.Pierce >= own.Slash && own.Pierce > 0f)
                result.Pierce = tierPhys > 0f ? tierPhys : own.Pierce;
            else if (own.Blunt >= own.Slash && own.Blunt > 0f)
                result.Blunt = tierPhys > 0f ? tierPhys : own.Blunt;
            else if (own.Slash > 0f)
                result.Slash = tierPhys > 0f ? tierPhys : own.Slash;
            // else: Lord has no physical channel (pure caster) — leave physical at 0.

            return result;
        }
    }
}
