using System.Collections.Generic;
using UnityEngine;
using BiomeLords.Util;

namespace BiomeLords.Phase1B
{
    /// <summary>
    /// Per-Lord summon and death FX configuration. Each Lord registers a
    /// themed FX set so we don't hard-code water-only effects in the summon
    /// service. Falls back to the Neck Lord's set if a Lord isn't configured.
    /// </summary>
    public static class LordFx
    {
        public class FxSet
        {
            public string[] Burst     = System.Array.Empty<string>();   // single-shot prefabs
            public string[] BurstTimed = System.Array.Empty<string>();  // spawned with 4s auto-destroy
            public string   Roar      = "fx_Fader_Roar";
            public int      SparkRing = 6;     // # of vfx_HitSparks around the centre
            public float    SparkR    = 1.5f;  // ring radius
        }

        private static readonly Dictionary<string, FxSet> Summon = new Dictionary<string, FxSet>();
        private static readonly Dictionary<string, FxSet> Death  = new Dictionary<string, FxSet>();

        static LordFx()
        {
            // Neck Lord — water theme.
            Summon["neck_lord"] = new FxSet
            {
                Burst = new[] { "fx_summon_start", "vfx_prespawn", "vfx_MeadSplash",
                                "fx_himminafl_aoe", "fx_redlightning_burst", "fx_chainlightning_hit" },
                BurstTimed = new[] { "vfx_water_surface" },
                SparkRing = 6,
            };
            Death["neck_lord"] = new FxSet
            {
                Burst = new[] { "vfx_eikthyr_death", "vfx_BonemassDeath", "vfx_MeadSplash",
                                "vfx_corpse_destruction_small", "fx_himminafl_aoe", "fx_redlightning_burst" },
                BurstTimed = new[] { "vfx_water_surface" },
                SparkRing = 12,
            };

            // Greydwarf Shaman Lord — forest/root theme.
            Summon["greydwarf_lord"] = new FxSet
            {
                Burst = new[] { "fx_summon_start", "vfx_prespawn", "fx_gdking_rootspawn",
                                "vfx_gdking_stomp", "fx_himminafl_aoe" },
                SparkRing = 8,
            };
            Death["greydwarf_lord"] = new FxSet
            {
                Burst = new[] { "vfx_greydwarf_elite_death", "fx_gdking_rootspawn",
                                "vfx_gdking_stomp", "vfx_corpse_destruction_small",
                                "fx_himminafl_aoe", "fx_redlightning_burst" },
                SparkRing = 12,
            };

            // Draugr Elite Lord — poison / bone theme (no fog).
            Summon["draugr_lord"] = new FxSet
            {
                Burst = new[] { "fx_summon_start", "vfx_prespawn", "vfx_DraugrSpawn",
                                "vfx_blob_attack", "fx_himminafl_aoe" },
                SparkRing = 8,
            };
            Death["draugr_lord"] = new FxSet
            {
                Burst = new[] { "vfx_draugr_death", "vfx_BonemassDeath", "vfx_blob_attack",
                                "vfx_corpse_destruction_small", "fx_himminafl_aoe", "fx_redlightning_burst" },
                SparkRing = 12,
            };

            // Fenring Lord — frost / pack theme. Uses widely-shipped vanilla FX
            // names — the rare frost ones may not exist in all versions so they
            // simply no-op via FxLibrary.TrySpawn when absent.
            Summon["fenring_lord"] = new FxSet
            {
                Burst = new[] { "fx_summon_start", "vfx_prespawn", "fx_himminafl_aoe",
                                "fx_chainlightning_hit", "vfx_FreezeGland_explosion" },
                BurstTimed = new[] { "vfx_frostbolt_explode" },
                SparkRing  = 8,
            };
            Death["fenring_lord"] = new FxSet
            {
                Burst = new[] { "vfx_fenring_death", "vfx_corpse_destruction_small",
                                "fx_himminafl_aoe", "vfx_FreezeGland_explosion",
                                "fx_redlightning_burst" },
                BurstTimed = new[] { "vfx_frostbolt_explode" },
                SparkRing  = 12,
            };

            // Lox Lord — Plains dust / stomp theme.
            Summon["lox_lord"] = new FxSet
            {
                Burst = new[] { "fx_summon_start", "vfx_prespawn", "vfx_gdking_stomp",
                                "fx_himminafl_aoe", "fx_crit" },
                SparkRing  = 8,
            };
            Death["lox_lord"] = new FxSet
            {
                Burst = new[] { "vfx_lox_death", "vfx_corpse_destruction_small",
                                "vfx_gdking_stomp", "fx_himminafl_aoe",
                                "fx_redlightning_burst" },
                SparkRing  = 12,
            };

            // Seeker Lord — Mistlands hive theme (violet sparks).
            Summon["seeker_lord"] = new FxSet
            {
                Burst = new[] { "fx_summon_start", "vfx_prespawn", "fx_himminafl_aoe",
                                "vfx_seeker_attack", "fx_chainlightning_hit" },
                SparkRing  = 10,
            };
            Death["seeker_lord"] = new FxSet
            {
                Burst = new[] { "vfx_seeker_death", "vfx_corpse_destruction_small",
                                "fx_himminafl_aoe", "vfx_seeker_attack",
                                "fx_redlightning_burst" },
                SparkRing  = 14,
            };

            // Fallen Valkyrie Lord — Ashlands radiant / flight theme.
            Summon["faller_valkyrie_lord"] = new FxSet
            {
                Burst = new[] { "fx_summon_start", "vfx_prespawn", "fx_Fader_Fissure_Prespawn",
                                "fx_himminafl_aoe", "fx_fallenvalkyrie_screech", "fx_crit" },
                BurstTimed = new[] { "vfx_meteor_explosion" },
                SparkRing  = 10,
            };
            Death["faller_valkyrie_lord"] = new FxSet
            {
                Burst = new[] { "fx_fallenvalkyrie_death", "fx_Fader_CorpseExplosion",
                                "vfx_corpse_destruction_small", "fx_himminafl_aoe",
                                "fx_redlightning_burst" },
                BurstTimed = new[] { "vfx_meteor_explosion" },
                SparkRing  = 16,
            };
        }

        public static void PlaySummon(string lordId, Vector3 pos) => Play(Summon, lordId, pos);
        public static void PlayDeath (string lordId, Vector3 pos) => Play(Death,  lordId, pos);

        private static void Play(Dictionary<string, FxSet> map, string lordId, Vector3 pos)
        {
            if (!map.TryGetValue(lordId, out var fx) && !map.TryGetValue("neck_lord", out fx)) return;

            foreach (var n in fx.Burst)      FxLibrary.TrySpawn(n, pos);
            foreach (var n in fx.BurstTimed) FxLibrary.TrySpawnTimed(n, pos, 4f);

            for (int i = 0; i < fx.SparkRing; i++)
            {
                float ang = i * (360f / fx.SparkRing);
                var off = Quaternion.Euler(0f, ang, 0f) * Vector3.forward * fx.SparkR;
                FxLibrary.TrySpawn("vfx_HitSparks", pos + off + Vector3.up * 0.5f);
            }
            if (!string.IsNullOrEmpty(fx.Roar)) FxLibrary.TrySpawn(fx.Roar, pos);
        }
    }
}
