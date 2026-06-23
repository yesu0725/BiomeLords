using System.Collections.Generic;
using UnityEngine;

namespace BiomeLords.Phase1B
{
    /// <summary>
    /// Custom RandomEvents (vanilla "The forest is moving" system) for each
    /// Lord summon. Triggered manually by SummonService via SetRandomEventByName.
    /// </summary>
    public static class EventFactory
    {
        public const string NeckLordEvent       = "biomelords_neck";
        public const string GreydwarfLordEvent  = "biomelords_greydwarf";
        public const string DraugrLordEvent     = "biomelords_draugr";
        public const string FenringLordEvent    = "biomelords_fenring";
        public const string LoxLordEvent        = "biomelords_lox";
        public const string SeekerLordEvent     = "biomelords_seeker";
        public const string FallerValkyrieLordEvent = "biomelords_fallervalkyrie";

        private static readonly Dictionary<string, string> EventByLord = new Dictionary<string, string>
        {
            { "neck_lord",      NeckLordEvent      },
            { "greydwarf_lord", GreydwarfLordEvent },
            { "draugr_lord",    DraugrLordEvent    },
            { "fenring_lord",   FenringLordEvent   },
            { "lox_lord",       LoxLordEvent       },
            { "seeker_lord",    SeekerLordEvent    },
            { "faller_valkyrie_lord", FallerValkyrieLordEvent },
        };

        public static string EventNameFor(string lordId) =>
            EventByLord.TryGetValue(lordId, out var n) ? n : null;

        public static void RegisterAll()
        {
            var sys = RandEventSystem.instance;
            if (sys == null)
            {
                Jotunn.Logger.LogWarning("[BiomeLords] RandEventSystem not ready; events skipped.");
                return;
            }

            RegisterEvent(sys, BuildNeckEvent());
            RegisterEvent(sys, BuildGreydwarfEvent());
            RegisterEvent(sys, BuildDraugrEvent());
            RegisterEvent(sys, BuildFenringEvent());
            RegisterEvent(sys, BuildLoxEvent());
            RegisterEvent(sys, BuildSeekerEvent());
            RegisterEvent(sys, BuildFallerValkyrieEvent());
        }

        private static RandomEvent BuildFallerValkyrieEvent() => new RandomEvent
        {
            m_name             = FallerValkyrieLordEvent,
            m_enabled          = true,
            m_random           = false,
            m_duration         = 600f,
            m_nearBaseOnly     = false,
            m_pauseIfNoPlayerInArea = true,
            m_biome            = Heightmap.Biome.AshLands,
            m_startMessage     = "$biomelords_summon_fallervalkyrie_start",
            m_endMessage       = "$biomelords_summon_fallervalkyrie_end",
            m_forceEnvironment = "AshRain",
            m_forceMusic       = "boss_eikthyr",
        };

        private static RandomEvent BuildSeekerEvent() => new RandomEvent
        {
            m_name             = SeekerLordEvent,
            m_enabled          = true,
            m_random           = false,
            m_duration         = 600f,
            m_nearBaseOnly     = false,
            m_pauseIfNoPlayerInArea = true,
            m_biome            = Heightmap.Biome.Mistlands,
            m_startMessage     = "$biomelords_summon_seeker_start",
            m_endMessage       = "$biomelords_summon_seeker_end",
            m_forceEnvironment = "Misty",
            m_forceMusic       = "boss_eikthyr",
        };

        private static RandomEvent BuildLoxEvent() => new RandomEvent
        {
            m_name             = LoxLordEvent,
            m_enabled          = true,
            m_random           = false,
            m_duration         = 600f,
            m_nearBaseOnly     = false,
            m_pauseIfNoPlayerInArea = true,
            m_biome            = Heightmap.Biome.Plains,
            m_startMessage     = "$biomelords_summon_lox_start",
            m_endMessage       = "$biomelords_summon_lox_end",
            m_forceEnvironment = "Misty",     // dusty/heavy feel; vanilla "Sand" not on all builds
            m_forceMusic       = "boss_eikthyr",
        };

        private static RandomEvent BuildFenringEvent() => new RandomEvent
        {
            m_name             = FenringLordEvent,
            m_enabled          = true,
            m_random           = false,
            m_duration         = 600f,
            m_nearBaseOnly     = false,
            m_pauseIfNoPlayerInArea = true,
            m_biome            = Heightmap.Biome.Mountain,
            m_startMessage     = "$biomelords_summon_fenring_start",
            m_endMessage       = "$biomelords_summon_fenring_end",
            m_forceEnvironment = "SnowStorm",
            m_forceMusic       = "boss_eikthyr",
        };

        private static void RegisterEvent(RandEventSystem sys, RandomEvent evt)
        {
            if (sys.m_events.Exists(e => e.m_name == evt.m_name)) return;
            sys.m_events.Add(evt);
            Jotunn.Logger.LogInfo($"[BiomeLords] Registered event: {evt.m_name}");
        }

        private static RandomEvent BuildNeckEvent() => new RandomEvent
        {
            m_name             = NeckLordEvent,
            m_enabled          = true,
            m_random           = false,
            m_duration         = 600f,
            m_nearBaseOnly     = false,
            m_pauseIfNoPlayerInArea = true,
            m_biome            = Heightmap.Biome.Meadows | Heightmap.Biome.BlackForest,
            m_startMessage     = "$biomelords_summon_neck_start",
            m_endMessage       = "$biomelords_summon_neck_end",
            m_forceEnvironment = "Rain",
            m_forceMusic       = "boss_eikthyr",
        };

        private static RandomEvent BuildGreydwarfEvent() => new RandomEvent
        {
            m_name             = GreydwarfLordEvent,
            m_enabled          = true,
            m_random           = false,
            m_duration         = 600f,
            m_nearBaseOnly     = false,
            m_pauseIfNoPlayerInArea = true,
            m_biome            = Heightmap.Biome.BlackForest | Heightmap.Biome.Meadows,
            m_startMessage     = "$biomelords_summon_greydwarf_start",
            m_endMessage       = "$biomelords_summon_greydwarf_end",
            m_forceEnvironment = "Misty",
            m_forceMusic       = "boss_eikthyr",
        };

        private static RandomEvent BuildDraugrEvent() => new RandomEvent
        {
            m_name             = DraugrLordEvent,
            m_enabled          = true,
            m_random           = false,
            m_duration         = 600f,
            m_nearBaseOnly     = false,
            m_pauseIfNoPlayerInArea = true,
            m_biome            = Heightmap.Biome.Swamp,
            m_startMessage     = "$biomelords_summon_draugr_start",
            m_endMessage       = "$biomelords_summon_draugr_end",
            m_forceEnvironment = "SwampRain",
            m_forceMusic       = "boss_eikthyr",
        };
    }
}
