using HarmonyLib;
using BiomeLords.Phase1D;
using BiomeLords.Util;
using UnityEngine;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Mitigates (does not cancel) incoming damage while either of the Lox
    /// Lord's defensive windows is active — the recurring Bone Bulwark
    /// (IsShielded) or the one-time Unyielding Bulwark (IsLastStand, the
    /// stronger of the two since the Lord is rooted and fully committed).
    /// Unlike NeckLordBlockPatch's full block, this is a magical ward that
    /// takes a percentage off every hit — blockable or not — rather than
    /// negating it outright.
    /// </summary>
    [HarmonyPatch(typeof(Character), "Damage")]
    public static class LoxLordShieldPatch
    {
        private const float ShieldReduction     = 0.65f; // Bone Bulwark
        private const float LastStandReduction  = 0.80f; // Unyielding Bulwark

        [HarmonyPrefix]
        public static bool Prefix(Character __instance, HitData hit)
        {
            if (hit == null) return true;

            var brain = __instance.GetComponent<LoxLordBrain>();
            if (brain == null) return true;

            float reduction = brain.IsLastStand ? LastStandReduction
                             : brain.IsShielded  ? ShieldReduction
                             : 0f;
            if (reduction <= 0f) return true;

            float k = 1f - reduction;
            hit.m_damage.m_damage    *= k;
            hit.m_damage.m_blunt     *= k;
            hit.m_damage.m_slash     *= k;
            hit.m_damage.m_pierce    *= k;
            hit.m_damage.m_chop      *= k;
            hit.m_damage.m_pickaxe   *= k;
            hit.m_damage.m_fire      *= k;
            hit.m_damage.m_frost     *= k;
            hit.m_damage.m_lightning *= k;
            hit.m_damage.m_poison    *= k;
            hit.m_damage.m_spirit    *= k;

            var pos = hit.m_point != Vector3.zero ? hit.m_point : __instance.transform.position + Vector3.up;
            FxLibrary.TrySpawn("fx_GoblinShieldHit", pos);

            return true; // let the mitigated hit through
        }
    }
}
