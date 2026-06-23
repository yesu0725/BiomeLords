using HarmonyLib;
using BiomeLords.Phase1C;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Tide's Grace (Neck Lord): while the local player is <b>Wet</b> and has the
    /// Forsaken Power active, every melee attack deals +50% damage. Applied through
    /// Character.Damage so it composes with the other damage patches.
    /// </summary>
    [HarmonyPatch(typeof(Character), "Damage")]
    public static class Character_Damage_TidesGraceMelee
    {
        private const float WetMeleeMultiplier = 1.5f; // +50%

        private static int _tidesHash;

        [HarmonyPrefix]
        public static void Prefix(Character __instance, HitData hit)
        {
            if (hit == null || __instance == null) return;

            // Attacker must be the local player.
            if (!(hit.GetAttacker() is Player attacker) || attacker != Player.m_localPlayer) return;
            if (!IsMeleeSkill(hit.m_skill)) return;

            var seman = attacker.GetSEMan();
            if (seman == null) return;

            if (_tidesHash == 0) _tidesHash = GuardianPowerFactory.NeckLordGP.GetStableHashCode();
            if (!seman.HaveStatusEffect(_tidesHash)) return;
            if (!seman.HaveStatusEffect(SEMan.s_statusEffectWet)) return;

            // Scale only the physical melee components.
            hit.m_damage.m_blunt  *= WetMeleeMultiplier;
            hit.m_damage.m_slash  *= WetMeleeMultiplier;
            hit.m_damage.m_pierce *= WetMeleeMultiplier;
        }

        private static bool IsMeleeSkill(Skills.SkillType skill)
        {
            switch (skill)
            {
                case Skills.SkillType.Swords:
                case Skills.SkillType.Knives:
                case Skills.SkillType.Clubs:
                case Skills.SkillType.Polearms:
                case Skills.SkillType.Spears:
                case Skills.SkillType.Axes:
                case Skills.SkillType.Unarmed:
                    return true;
                default:
                    return false;
            }
        }
    }
}
