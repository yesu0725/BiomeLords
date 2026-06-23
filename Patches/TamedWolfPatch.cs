using HarmonyLib;
using UnityEngine;
using BiomeLords.Phase1C;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Pack Whisperer blessing (SE_FenringLordSpirit) and Howl of the Pack
    /// Forsaken Power (GP_HowlOfThePack) both reshape damage between tamed
    /// wolves and the world.
    ///
    /// While the local player has the SE/GP active AND is within Range metres
    /// of the wolf in question:
    ///   • Pack Whisperer alone           → tamed wolves take −50 % damage.
    ///   • Howl of the Pack alone          → tamed wolves deal +100 % damage.
    ///   • BOTH active at the same time    → tamed wolves take −90 % damage AND
    ///                                       still deal +100 % damage.
    ///
    /// Implementation lives in Character.Damage prefix so it composes with all
    /// existing patches (e.g. Lord damage boost runs on a different attacker
    /// path so there's no collision).
    /// </summary>
    [HarmonyPatch(typeof(Character), "Damage")]
    public static class Character_Damage_PackWhisperer
    {
        private const float WhispererDmgTakenMult = 0.50f;  // -50% incoming (blessing alone)
        private const float SynergyDmgTakenMult   = 0.15f;  // -85% incoming (blessing + Howl)
        private const float HowlDmgDealtMult      = 2.00f;  // +100% outgoing (Howl of the Pack)
        private const float Range = 30f;

        private static int _blessingHash;
        private static int _howlHash;

        [HarmonyPrefix]
        public static void Prefix(Character __instance, HitData hit)
        {
            if (hit == null || __instance == null) return;
            var p = Player.m_localPlayer;
            if (p == null || p.IsDead()) return;

            if (_blessingHash == 0) _blessingHash = StatusEffectFactory.FenringLordSpiritSE.GetStableHashCode();
            if (_howlHash     == 0) _howlHash     = GuardianPowerFactory.FenringLordGP.GetStableHashCode();

            var seman = p.GetSEMan();
            bool hasBlessing = seman != null && seman.HaveStatusEffect(_blessingHash);
            bool hasHowl     = seman != null && seman.HaveStatusEffect(_howlHash);
            if (!hasBlessing && !hasHowl) return;

            // ---- Tamed wolf is the DEFENDER → damage taken reduction ----
            // Synergy: blessing + Howl together deepen the reduction to -90%.
            if (hasBlessing && IsTamedWolf(__instance) && InRange(p, __instance))
            {
                float takenMult = hasHowl ? SynergyDmgTakenMult : WhispererDmgTakenMult;
                Scale(hit, takenMult);
                return;
            }

            // ---- Tamed wolf is the ATTACKER → damage dealt boost ----
            // Pack Whisperer is now defensive-only; offense buff is Howl of the
            // Pack's exclusive turf so the two powers stay distinct.
            if (!hasHowl) return;
            var attackerWolf = hit.GetAttacker();
            if (!IsTamedWolf(attackerWolf)) return;
            if (!InRange(p, attackerWolf)) return;
            Scale(hit, HowlDmgDealtMult);
        }

        private static bool IsTamedWolf(Character c)
        {
            if (c == null) return false;
            if (!c.gameObject.name.StartsWith("Wolf")) return false;
            return c.IsTamed();
        }

        private static bool InRange(Component a, Component b)
        {
            if (a == null || b == null) return false;
            return (a.transform.position - b.transform.position).sqrMagnitude <= Range * Range;
        }

        private static void Scale(HitData hit, float mult)
        {
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
        }
    }
}
