using HarmonyLib;
using BiomeLords.Phase1D;
using BiomeLords.Util;

namespace BiomeLords.Patches
{
    /// <summary>
    /// When a Fenring Lord with an active Vampiric Strike buff lands a melee
    /// hit, absorb 80 % of the damage dealt back as healing.  Uses a Damage
    /// postfix so the heal fires after armor reduction has been applied and
    /// fully-blocked hits (0 total damage) are ignored.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
    static class Character_Damage_FenringVamp
    {
        [HarmonyPostfix]
        static void Postfix(HitData hit)
        {
            var attacker = hit.GetAttacker();
            if (attacker == null) return;

            var brain = attacker.GetComponent<FenringLordBrain>();
            if (brain == null || !brain.IsVampActive) return;

            float dmg = hit.GetTotalDamage();
            if (dmg <= 0f) return;

            attacker.Heal(dmg * FenringLordBrain.VampHealFraction);
            FxLibrary.TrySpawn("vfx_HitSparks", attacker.transform.position + UnityEngine.Vector3.up);
        }
    }
}
