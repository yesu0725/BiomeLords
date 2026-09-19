using System.Collections.Generic;
using BepInEx.Configuration;
using BiomeLords.Data;
using BiomeLords.Util;

namespace BiomeLords.Config
{
    /// <summary>
    /// BepInEx config wrapper. Every gameplay entry is marked IsAdminOnly so that on
    /// a dedicated server, the server's values are pushed to clients via Jotunn's
    /// SynchronizationManager — clients cannot locally override these.
    ///
    /// The "UI" section is the deliberate exception: those entries are pure local
    /// cosmetics (window placement) written by the player dragging the window, so
    /// they are bound WITHOUT IsAdminOnly and stay client-side.
    /// </summary>
    public static class LordConfig
    {
        public static ConfigEntry<bool>  DebugLogging;
        public static ConfigEntry<bool>  EnableKillTracking;
        public static ConfigEntry<bool>  ShowCropGrowTimes;
        public static ConfigEntry<bool>  GlobalLordDefeats;
        public static ConfigEntry<int>    BlessingChargesPerTrophy;
        public static ConfigEntry<float>  FisherBoonBonusFishChance;
        public static ConfigEntry<float>  FisherBoonBaitSaveChance;
        public static ConfigEntry<float>  HearthMasterMultiplier;
        public static ConfigEntry<float>  RefinersTouchChance;
        public static ConfigEntry<float>  FallerValkyrieWeightCap;
        public static ConfigEntry<int>    FallerValkyrieExtraRows;
        public static ConfigEntry<float>  ValkyrieRallyRadius;
        public static ConfigEntry<float>  ValkyrieRallyRestedSeconds;
        public static ConfigEntry<float>  FimbulHideSnowShedRadius;
        public static ConfigEntry<float>  PetrifyDuration;
        public static ConfigEntry<float>  PetrifyShatterRadius;
        public static ConfigEntry<float>  PetrifyShatterDamage;
        public static ConfigEntry<string> HallRecipe;
        public static ConfigEntry<string> HornRecipe;
        public static ConfigEntry<float>  StorageUiOffsetColumns;
        public static ConfigEntry<float>  StorageUiOffsetRows;

        private static readonly Dictionary<string, ConfigEntry<int>>   _killReq =
            new Dictionary<string, ConfigEntry<int>>();
        private static readonly Dictionary<string, ConfigEntry<float>> _baseHp =
            new Dictionary<string, ConfigEntry<float>>();
        private static readonly Dictionary<string, ConfigEntry<float>> _hpMult =
            new Dictionary<string, ConfigEntry<float>>();
        private static readonly Dictionary<string, ConfigEntry<float>> _dmgMult =
            new Dictionary<string, ConfigEntry<float>>();

        private static ConfigDescription Admin(string description)
        {
            return new ConfigDescription(
                description,
                null,
                new ConfigurationManagerAttributes { IsAdminOnly = true });
        }

        public static void Bind(ConfigFile cfg)
        {
            DebugLogging = cfg.Bind("General", "DebugLogging", false,
                Admin("Verbose logging for kill tracking and summon checks. Spammy — leave off in normal play."));

            EnableKillTracking = cfg.Bind("General", "EnableKillTracking", true,
                Admin("Master switch for kill tracking. Disable to stop counting (existing counts are preserved)."));

            ShowCropGrowTimes = cfg.Bind("General", "ShowCropGrowTimes", false,
                Admin("DEBUG: When true, planted crops show remaining grow time in their hover text."));

            GlobalLordDefeats = cfg.Bind("General", "GlobalLordDefeats", false,
                Admin("When false (default), each player's Lord defeat progression is tracked " +
                      "individually — only your own Lord kills raise your scaling tier. " +
                      "When true, any Lord kill on the server advances scaling for every player."));

            BlessingChargesPerTrophy = cfg.Bind("Blessings", "ChargesPerTrophy", 5,
                Admin("How many blessings each mounted trophy can grant before crumbling to dust. " +
                      "Hunting the Lord again restocks via a fresh trophy."));

            FisherBoonBonusFishChance = cfg.Bind("Blessings", "FisherBoonBonusFishChance", 0.25f,
                Admin("Fisher's Boon: chance (0-1) for a bonus fish on pickup. 0.25 = 25%."));
            FisherBoonBaitSaveChance = cfg.Bind("Blessings", "FisherBoonBaitSaveChance", 0.50f,
                Admin("Fisher's Boon (Bait Saver): chance (0-1) that a cast doesn't consume bait. 0.50 = 50%."));

            HearthMasterMultiplier = cfg.Bind("Blessings", "HearthMasterMultiplier", 2.0f,
                Admin("Hearth Master (Lox Lord): multiplier on the duration of food buffs you " +
                      "eat. 2.0 = 100% longer. Stacks with vanilla cooking-quality bonuses."));

            RefinersTouchChance = cfg.Bind("Blessings", "RefinersTouchChance", 0.5f,
                Admin("Refiner's Touch (Seeker Lord): chance (0-1) that a Smelter, Blast Furnace, " +
                      "Spinning Wheel or Eitr Refinery yields a bonus output item when one finishes " +
                      "near you. 0.5 = 50%."));

            FallerValkyrieWeightCap = cfg.Bind("Blessings", "FallerValkyrieWeightCap", 1000f,
                Admin("Featherweight (Fallen Valkyrie Lord): raised carry-weight cap, in units. " +
                      "Below this you suffer NO over-encumbrance penalty — you walk, run, and " +
                      "regenerate stamina normally with no encumbered animation. Only when your " +
                      "load reaches this cap (1000) does the normal encumbered state kick in. " +
                      "The inventory weight readout still shows your base capacity, not this cap."));
            FallerValkyrieExtraRows = cfg.Bind("Blessings", "FallerValkyrieExtraRows", 2,
                Admin("Featherweight: extra inventory rows granted while the blessing is active " +
                      "(8 slots per row). When you switch to a different blessing, items left in " +
                      "these extra rows are moved into a CargoCrate dropped at your feet."));

            FimbulHideSnowShedRadius = cfg.Bind("Blessings", "FimbulHideSnowShedRadius", 30f,
                Admin("Fimbul Hide (Gammeltroll Lord): radius, in metres, within which your building " +
                      "pieces shed their snow buildup while the blessing is active (Deep North only). " +
                      "0 disables the shedding; the deep-snow walking immunity is unaffected."));

            PetrifyDuration = cfg.Bind("ForsakenPowers", "PetrifyDuration", 6f,
                Admin("Petrify (Gammeltroll Lord): seconds the stone skin lasts before it shatters. " +
                      "While it lasts you take -75% damage of every type, cannot be staggered, and move at -60%."));
            PetrifyShatterRadius = cfg.Bind("ForsakenPowers", "PetrifyShatterRadius", 8f,
                Admin("Petrify: radius, in metres, of the shatter burst when the stone skin breaks."));
            PetrifyShatterDamage = cfg.Bind("ForsakenPowers", "PetrifyShatterDamage", 80f,
                Admin("Petrify: frost damage dealt to every hostile inside the shatter radius."));

            ValkyrieRallyRadius = cfg.Bind("ForsakenPowers", "ValkyrieRallyRadius", 20f,
                Admin("Valkyrie's Rally (Fallen Valkyrie Lord): radius, in metres, around the caster " +
                      "within which all players are fully restored. 20 = 20 m."));
            ValkyrieRallyRestedSeconds = cfg.Bind("ForsakenPowers", "ValkyrieRallyRestedSeconds", 1200f,
                Admin("Valkyrie's Rally: duration of the Rested buff granted to affected players, in " +
                      "seconds. 1200 = 20 minutes."));

            HallRecipe = cfg.Bind("Hall", "Recipe",
                "Stone:40,FineWood:20,Flint:10,SurtlingCore:3",
                Admin("Comma-separated 'ItemPrefab:Amount' pairs for the Hall of the Lords. " +
                      "Examples: 'Stone:40,FineWood:20,Flint:10,SurtlingCore:3' (default). " +
                      "Use vanilla item prefab names. Amounts ≤ 0 skip that requirement."));

            HornRecipe = cfg.Bind("LordsHorn", "Recipe",
                "NeckTail:5,TrophyDeer:1,Bronze:1",
                Admin("Comma-separated 'ItemPrefab:Amount' pairs for crafting the Lord's Horn at " +
                      "the Workbench. Example: 'NeckTail:5,TrophyDeer:1,Bronze:1' (default). " +
                      "Use vanilla item prefab names. Amounts ≤ 0 skip that requirement. " +
                      "Requires a restart to take effect."));

            // Local (non-admin, not server-sync'd): where the chest/storage window sits.
            // Measured in inventory cells rather than pixels so the placement is identical
            // at any UI scale or resolution. Both are relative to the window's vanilla
            // position, and both are rewritten whenever the player drags the window.
            StorageUiOffsetColumns = cfg.Bind("UI", "StorageUiOffsetColumns", 0f,
                "Horizontal position of the chest/storage window, in inventory cell widths " +
                "away from its vanilla spot. Positive = right, negative = left. 0 = vanilla. " +
                "Drag the storage window in-game to set this automatically.");

            StorageUiOffsetRows = cfg.Bind("UI", "StorageUiOffsetRows", 2f,
                "Vertical position of the chest/storage window, in inventory row heights " +
                "below its vanilla spot. Positive = down, negative = up. The default of 2 " +
                "drops it two rows clear of the player inventory so the Featherweight extra " +
                "rows have room. 0 = vanilla (flush under the player inventory). " +
                "Drag the storage window in-game to set this automatically.");

            foreach (var lord in LordRegistry.All)
            {
                _killReq[lord.Id] = cfg.Bind("KillRequirements", lord.Id, lord.DefaultKillRequirement,
                    Admin($"Kills required before the Lord's Horn can summon the {lord.DisplayName}. " +
                          $"Targets: {string.Join(", ", lord.KillTargets)}"));

                float defaultHp = LordBaseStats.DefaultHpFor(lord.Id, lord.Tier);
                _baseHp[lord.Id] = cfg.Bind("LordStats.BaseHealth", lord.Id, defaultHp,
                    Admin($"Base HP of the {lord.DisplayName} at its native tier ({lord.Tier}), " +
                          $"before progression scaling and HealthMultiplier. Default {defaultHp:0} = " +
                          $"the {lord.Biome} vanilla boss's HP. Values <= 0 fall back to the default."));

                _hpMult[lord.Id] = cfg.Bind("LordStats.HealthMultiplier", lord.Id, 1.0f,
                    Admin($"Multiplier applied on top of the {lord.DisplayName}'s BaseHealth " +
                          $"(after progression scaling). 1.0 = default. 2.0 = twice as tough. 0.5 = half as tough."));

                _dmgMult[lord.Id] = cfg.Bind("LordStats.DamageMultiplier", lord.Id, 1.0f,
                    Admin($"Multiplier applied to the {lord.DisplayName}'s tier-baseline damage. " +
                          $"1.0 = default. 2.0 = twice as hard-hitting. 0.5 = half damage."));
            }
        }

        public static int   KillRequirement(string lordId)    => _killReq.TryGetValue(lordId,  out var e) ? e.Value : 0;
        /// <summary>Configured base HP for a Lord, or 0 when unset / non-positive (caller falls back).</summary>
        public static float BaseHealth(string lordId)         => _baseHp.TryGetValue(lordId,   out var e) && e.Value > 0f ? e.Value : 0f;
        public static float HealthMultiplier(string lordId)   => _hpMult.TryGetValue(lordId,   out var e) ? e.Value : 1f;
        public static float DamageMultiplier(string lordId)   => _dmgMult.TryGetValue(lordId,  out var e) ? e.Value : 1f;
    }
}
