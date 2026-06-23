using HarmonyLib;
using BiomeLords.Phase1D;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Rotten Cleave "Wounded" application — gated strictly on landed damage.
    ///
    /// The cleave is a real equipped-weapon swing (Humanoid.StartAttack). The
    /// brain opens a short cleave window (DraugrLordBrain.IsCleaving) around the
    /// swing. We capture the player's HP just before Character.Damage resolves and
    /// only apply Wounded if the hit actually reduced it — so a dodge (Damage
    /// never fires) AND a fully-blocked/parried hit (no HP lost) both avoid the
    /// bleed; only damage that truly lands wounds.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
    public static class Character_Damage_DraugrWound
    {
        [HarmonyPrefix]
        public static void Prefix(Character __instance, HitData hit, out float __state)
        {
            // Sentinel: <0 means "not a tracked cleave hit" — postfix skips it.
            __state = -1f;
            if (hit == null) return;
            if (!(__instance is Player player) || player.IsDead()) return;

            var attacker = hit.GetAttacker();
            if (attacker == null) return;
            if (!DraugrLordBrain.IsCleaving(attacker.GetInstanceID())) return;

            // Pre-damage health, captured before the original method applies the hit.
            __state = __instance.GetHealth();
        }

        [HarmonyPostfix]
        public static void Postfix(Character __instance, float __state)
        {
            if (__state < 0f) return;                       // not a cleave hit
            if (!(__instance is Player player)) return;
            if (__instance.GetHealth() >= __state) return;  // blocked/parried — no HP lost

            DraugrLordBrain.ApplyWound(player);
        }
    }
}
