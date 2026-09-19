using HarmonyLib;
using BiomeLords.Phase1D;
using BiomeLords.Util;
using UnityEngine;

namespace BiomeLords.Patches
{
    /// <summary>
    /// The Gammeltroll Lord's stone shell. While the Lord is petrified every
    /// incoming damage component except <b>pickaxe</b> is cut to
    /// <see cref="GammeltrollLordBrain.ShellDamageFactor"/>, and the hit can neither
    /// push nor stagger a statue. Pickaxe damage passes untouched — vanilla
    /// TrollFrost is already Weak to it, so picks chip the shell at ×1.5 while
    /// swords bounce off. Same shape as LoxLordShieldPatch: mitigate, never cancel.
    /// </summary>
    [HarmonyPatch(typeof(Character), "Damage")]
    public static class GammeltrollShellPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Character __instance, HitData hit)
        {
            if (hit == null) return true;

            var brain = __instance.GetComponent<GammeltrollLordBrain>();
            if (brain == null || !brain.IsPetrified) return true;

            float k = GammeltrollLordBrain.ShellDamageFactor;
            hit.m_damage.m_damage    *= k;
            hit.m_damage.m_blunt     *= k;
            hit.m_damage.m_slash     *= k;
            hit.m_damage.m_pierce    *= k;
            hit.m_damage.m_chop      *= k;
            hit.m_damage.m_fire      *= k;
            hit.m_damage.m_frost     *= k;
            hit.m_damage.m_lightning *= k;
            hit.m_damage.m_poison    *= k;
            hit.m_damage.m_spirit    *= k;
            // m_pickaxe deliberately untouched.

            hit.m_pushForce         = 0f;
            hit.m_staggerMultiplier = 0f;

            var pos = hit.m_point != Vector3.zero ? hit.m_point : __instance.transform.position + Vector3.up * 2f;
            FxLibrary.TrySpawn(hit.m_damage.m_pickaxe > 0f ? "vfx_RockHit" : "fx_GoblinShieldHit", pos);

            return true; // let the mitigated hit through
        }
    }
}
