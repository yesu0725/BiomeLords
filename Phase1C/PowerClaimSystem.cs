using System.Collections.Generic;
using UnityEngine;
using BiomeLords.Config;
using BiomeLords.Util;

namespace BiomeLords.Phase1C
{
    /// <summary>
    /// Forsaken Power award flow. The Lord's Power is granted automatically at
    /// the moment a Lord dies — the dramatic altar-style FX plays at the
    /// killing player, the vanilla Guardian Power slot is set, and the unique-key
    /// is recorded for posterity.
    ///
    /// Pedestals no longer handle power management (just trophies + blessings).
    /// </summary>
    public static class PowerClaimSystem
    {
        /// <summary>Unique-key recorded on a character when they kill a Lord.</summary>
        public static string DefeatKey(string lordId) => "biomelords_defeated_" + lordId;

        public static bool HasDefeated(Player p, string lordId)
        {
            return p != null && !string.IsNullOrEmpty(lordId) && p.HaveUniqueKey(DefeatKey(lordId));
        }

        /// <summary>Lord id → registered Guardian Power name.</summary>
        private static readonly Dictionary<string, string> LordToGP = new Dictionary<string, string>
        {
            { "neck_lord",       GuardianPowerFactory.NeckLordGP },
            { "greydwarf_lord",  GuardianPowerFactory.GreydwarfLordGP },
            { "draugr_lord",     GuardianPowerFactory.DraugrLordGP },
            { "fenring_lord",    GuardianPowerFactory.FenringLordGP },
            { "lox_lord",        GuardianPowerFactory.LoxLordGP },
            { "seeker_lord",     GuardianPowerFactory.SeekerLordGP },
            { "faller_valkyrie_lord", GuardianPowerFactory.FallerValkyrieLordGP },
        };

        /// <summary>
        /// Called when a Lord dies. Auto-equips the Lord's Forsaken Power on
        /// the killer (replacing whatever GP they had), plays the altar FX,
        /// and sends a confirmation message.
        ///
        /// Note: this overwrites any previously held GP (vanilla or Lord). If
        /// the player defeats another Lord later, that Lord's GP will replace
        /// this one — the most-recently-slain Lord's spirit accompanies them.
        /// </summary>
        public static void GrantOnDefeat(Player killer, string lordId)
        {
            if (killer == null || string.IsNullOrEmpty(lordId)) return;
            if (!LordToGP.TryGetValue(lordId, out var gpName)) return;

            killer.SetGuardianPower(gpName);
            if (killer.GetGuardianPowerName() != gpName)
            {
                Jotunn.Logger.LogWarning($"[BiomeLords] SetGuardianPower({gpName}) didn't stick at Lord-defeat time.");
                return;
            }

            PlayAwardFx(killer);
            killer.Message(MessageHud.MessageType.Center, "$biomelords_power_awarded");

            if (LordConfig.DebugLogging.Value)
                Jotunn.Logger.LogInfo($"[BiomeLords] Auto-granted GP {gpName} to {killer.GetPlayerName()}.");
        }

        /// <summary>Dramatic vanilla-altar style FX stack on the player.</summary>
        private static void PlayAwardFx(Player player)
        {
            var pos = player.transform.position;

            // No green VFX: "fx_summon_start" (green summon burst) and
            // "vfx_prespawn" (teal-green spawn swirl) are both omitted.
            FxLibrary.TrySpawn("fx_himminafl_aoe",      pos + Vector3.up * 1.5f);
            FxLibrary.TrySpawn("fx_redlightning_burst", pos + Vector3.up * 2f);
            FxLibrary.TrySpawn("vfx_MeadSplash",        pos);
            FxLibrary.TrySpawnTimed("vfx_water_surface", pos, 4f);
            for (int i = 0; i < 12; i++)
            {
                float ang = i * 30f;
                var off = Quaternion.Euler(0f, ang, 0f) * Vector3.forward * 1.8f;
                FxLibrary.TrySpawn("vfx_HitSparks", pos + off + Vector3.up * 0.5f);
            }
            FxLibrary.TrySpawn("fx_Fader_Roar", pos);
            FxLibrary.TrySpawn("vfx_lootspawn", pos + Vector3.up * 1.2f);
        }
    }
}
