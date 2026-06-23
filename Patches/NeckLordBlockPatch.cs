using HarmonyLib;
using BiomeLords.Phase1B;
using BiomeLords.Util;
using UnityEngine;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Cancels incoming blockable damage while the Neck Lord's IsBlocking window
    /// is active (brain-driven, only available below 50% HP).
    /// </summary>
    [HarmonyPatch(typeof(Character), "Damage")]
    public static class NeckLordBlockPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Character __instance, HitData hit)
        {
            if (hit == null || !hit.m_blockable) return true;

            var brain = __instance.GetComponent<NeckLordBrain>();
            if (brain == null || !brain.IsBlocking) return true;

            var pos = hit.m_point != Vector3.zero ? hit.m_point : __instance.transform.position + Vector3.up;
            FxLibrary.TrySpawn("fx_GoblinShieldHit", pos);

            return false; // cancel damage
        }
    }
}
