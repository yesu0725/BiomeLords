using HarmonyLib;
using BiomeLords.Phase1C;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Tide's Grace (Neck Lord): the Wet status's debuffs are ineffective against
    /// the local player while the Forsaken Power is active. The player still counts
    /// as Wet (so the +50% melee bonus applies — see Character_Damage_TidesGraceMelee),
    /// but Wet's contributions to damage modifiers and stamina regen are skipped.
    ///
    /// Both Modify* methods are declared on SE_Stats (SE_Wet inherits them), so we
    /// patch SE_Stats and gate on the instance actually being the Wet effect.
    /// </summary>
    public static class NeckWetImmunityPatch
    {
        private static int _tidesHash;

        /// <summary>True when this status effect is the Wet effect riding on the
        /// local player and Tide's Grace is active.</summary>
        private static bool ShouldSuppress(SE_Stats se)
        {
            if (!(se is SE_Wet)) return false;
            var p = Player.m_localPlayer;
            if (p == null || se.m_character != p) return false;

            if (_tidesHash == 0) _tidesHash = GuardianPowerFactory.NeckLordGP.GetStableHashCode();
            var seman = p.GetSEMan();
            return seman != null && seman.HaveStatusEffect(_tidesHash);
        }

        // Skip Wet's frost/lightning vulnerability (and any other m_mods).
        [HarmonyPatch(typeof(SE_Stats), "ModifyDamageMods")]
        public static class SE_Stats_ModifyDamageMods_NeckWet
        {
            [HarmonyPrefix]
            public static bool Prefix(SE_Stats __instance) => !ShouldSuppress(__instance);
        }

        // Skip Wet's stamina-regen penalty.
        [HarmonyPatch(typeof(SE_Stats), "ModifyStaminaRegen")]
        public static class SE_Stats_ModifyStaminaRegen_NeckWet
        {
            [HarmonyPrefix]
            public static bool Prefix(SE_Stats __instance) => !ShouldSuppress(__instance);
        }
    }
}
