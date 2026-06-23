using HarmonyLib;
using UnityEngine;
using BiomeLords.Phase1D;
using BiomeLords.Util;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Phantom wolves summoned by Howl of the Pack in synergy with Pack Whisperer
    /// ignore all incoming damage. If the defender carries a PhantomWolf marked
    /// Invulnerable, the hit is swallowed before it can touch the wolf's HP.
    /// A small spark plays so the deflection still reads visually.
    /// </summary>
    [HarmonyPatch(typeof(Character), "Damage")]
    public static class PhantomWolfInvulnPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Character __instance, HitData hit)
        {
            if (__instance == null) return true;
            var phantom = __instance.GetComponent<PhantomWolf>();
            if (phantom == null || !phantom.Invulnerable) return true;

            // Swallow the hit entirely — no HP loss, no stagger, no knockback.
            var pos = (hit != null && hit.m_point != Vector3.zero)
                ? hit.m_point
                : __instance.transform.position + Vector3.up;
            FxLibrary.TrySpawn("vfx_HitSparks", pos);
            return false;
        }
    }
}
