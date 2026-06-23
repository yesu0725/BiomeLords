using HarmonyLib;
using BiomeLords.Phase1C;

namespace BiomeLords.Patches
{
    /// <summary>
    /// While Plague Bearer is active, the local player's outgoing attacks gain
    /// +25 base poison damage. Vanilla SE_Stats.m_percentigeDamageModifiers
    /// (+75% poison) then multiplies that, so each strike inflicts ~44 effective
    /// poison output — meaningful even on weapons that have no native poison.
    /// </summary>
    [HarmonyPatch(typeof(Character), "Damage")]
    public static class Character_Damage_PlagueBearerPoison
    {
        private const float AddedPoison = 25f;
        private static int _gpHash;

        [HarmonyPrefix]
        public static void Prefix(HitData hit)
        {
            if (hit == null) return;
            var attacker = hit.GetAttacker() as Player;
            if (attacker == null || attacker != Player.m_localPlayer) return;

            if (_gpHash == 0) _gpHash = GuardianPowerFactory.DraugrLordGP.GetStableHashCode();
            var seman = attacker.GetSEMan();
            if (seman == null || !seman.HaveStatusEffect(_gpHash)) return;

            hit.m_damage.m_poison += AddedPoison;
        }
    }
}
