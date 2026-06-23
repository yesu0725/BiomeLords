using HarmonyLib;
using BiomeLords.Util;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Lord attack damage. Lords with a matched attack profile (LordAttackProfile)
    /// have their hit's combat damage REPLACED with the vanilla-boss signature
    /// attack split for their biome — so each swing deals exactly the boss's
    /// damage and elemental composition, independent of the cloned creature's
    /// native weapon. Any remaining per-instance multiplier (admin
    /// DamageMultiplier config; 1.0 by default) is applied on top.
    ///
    /// Attackers without a profile (e.g. vanilla bosses registered for scaling)
    /// fall back to the legacy multiply-native-damage path.
    /// </summary>
    [HarmonyPatch(typeof(Character), "Damage")]
    public static class Character_Damage_LordBoost
    {
        [HarmonyPrefix]
        public static void Prefix(HitData hit)
        {
            if (hit == null) return;
            var attacker = hit.GetAttacker();
            if (attacker == null) return;
            // Apply to Lords and to vanilla bosses that have been registered for scaling.
            if (!RegisteredLords.IsLord(attacker) && !LordDamageRegistry.Has(attacker)) return;

            // Per-instance multiplier (admin config). Defaults to 1.0 so the
            // profile values land exactly as written.
            float mult    = LordDamageRegistry.Get(attacker, fallback: 1.0f);
            string lordId = RegisteredLords.LordIdFor(attacker);

            // Greydwarf Lord: vanilla ranged poison orb arrives with m_poison > 0 and
            // m_blunt == 0. Redirect it to pure poison at the vanilla Greydwarf
            // Shaman's own poison spit magnitude so it keeps its thematic identity
            // instead of being converted to blunt. After Poison Nova the melee
            // profile has both blunt AND poison, so the blunt==0 guard correctly
            // limits this path to the ranged orb only.
            if (lordId == "greydwarf_lord"
                && hit.m_damage.m_poison > 0f
                && hit.m_damage.m_blunt  == 0f)
            {
                float poisonMag = 36f; // vanilla Greydwarf Shaman poison spit (30) x1.2
                hit.m_damage.m_damage    = 0f;
                hit.m_damage.m_blunt     = 0f;
                hit.m_damage.m_slash     = 0f;
                hit.m_damage.m_pierce    = 0f;
                hit.m_damage.m_fire      = 0f;
                hit.m_damage.m_frost     = 0f;
                hit.m_damage.m_lightning = 0f;
                hit.m_damage.m_poison    = poisonMag * mult;
                hit.m_damage.m_spirit    = 0f;
                hit.m_pushForce    *= 1.5f;
                hit.m_backstabBonus = 1f;
                return;
            }

            // Boss-matched profile path: overwrite combat damage outright.
            // chop/pickaxe are left as the native weapon's values so Lords can
            // still damage structures. The profile is the convergence-resolved
            // one baked at spawn/Awake (LordProfileRegistry); if that's somehow
            // missing we fall back to the Lord's own native-tier profile.
            bool haveProfile = LordProfileRegistry.TryGet(attacker, out var p)
                               || (lordId != null && LordAttackProfile.TryGet(lordId, out p));
            if (haveProfile)
            {
                hit.m_damage.m_damage    = 0f; // generic "true" damage unused by bosses
                hit.m_damage.m_blunt     = p.Blunt     * mult;
                hit.m_damage.m_slash     = p.Slash     * mult;
                hit.m_damage.m_pierce    = p.Pierce    * mult;
                hit.m_damage.m_fire      = p.Fire      * mult;
                hit.m_damage.m_frost     = p.Frost     * mult;
                hit.m_damage.m_lightning = p.Lightning * mult;
                hit.m_damage.m_poison    = p.Poison    * mult;
                hit.m_damage.m_spirit    = p.Spirit    * mult;

                hit.m_pushForce    *= 1.5f;
                hit.m_backstabBonus = 1f; // ignore backstab cheese

                if (BiomeLords.Config.LordConfig.DebugLogging != null
                    && BiomeLords.Config.LordConfig.DebugLogging.Value)
                {
                    Jotunn.Logger.LogInfo(
                        $"[BiomeLords] {lordId} hit (profile ×{mult:F2}): " +
                        $"blunt={hit.m_damage.m_blunt:F0} slash={hit.m_damage.m_slash:F0} " +
                        $"pierce={hit.m_damage.m_pierce:F0} fire={hit.m_damage.m_fire:F0} " +
                        $"frost={hit.m_damage.m_frost:F0} lightning={hit.m_damage.m_lightning:F0} " +
                        $"poison={hit.m_damage.m_poison:F0} spirit={hit.m_damage.m_spirit:F0}");
                }
                return;
            }

            // Legacy multiply path (registered vanilla bosses without a profile).
            hit.m_damage.m_damage   *= mult;
            hit.m_damage.m_blunt    *= mult;
            hit.m_damage.m_slash    *= mult;
            hit.m_damage.m_pierce   *= mult;
            hit.m_damage.m_chop     *= mult;
            hit.m_damage.m_pickaxe  *= mult;
            hit.m_damage.m_fire     *= mult;
            hit.m_damage.m_frost    *= mult;
            hit.m_damage.m_lightning*= mult;
            hit.m_damage.m_poison   *= mult;
            hit.m_damage.m_spirit   *= mult;

            hit.m_pushForce    *= 1.5f;
            hit.m_backstabBonus = 1f; // ignore backstab cheese
        }
    }
}
