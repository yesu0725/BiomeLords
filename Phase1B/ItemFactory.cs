using UnityEngine;
using Jotunn.Managers;
using Jotunn.Entities;
using Jotunn.Configs;

namespace BiomeLords.Phase1B
{
    /// <summary>
    /// Registers the single "Lord's Horn" — one consumable that summons whichever
    /// Lord matches the player's current biome AND kill-count threshold.
    /// Cloned visually from the vanilla drinking horn.
    /// </summary>
    public static class ItemFactory
    {
        public const string LordsHornPrefab = "LordsHorn";

        public static void RegisterAll()
        {
            // Cloned visually from the Anniversary Tankard — a drinking-horn-shaped
            // ceremonial item that fits the "summoning relic" feel better than Wishbone.
            var item = new CustomItem(LordsHornPrefab, "TankardAnniversary", new ItemConfig
            {
                Name        = "$item_lordshorn",
                Description = "$item_lordshorn_desc",
                CraftingStation = CraftingStations.Workbench,
                Requirements = new[]
                {
                    new RequirementConfig { Item = "NeckTail",   Amount = 5,  Recover = false },
                    new RequirementConfig { Item = "TrophyDeer", Amount = 1,  Recover = false },
                    new RequirementConfig { Item = "Bronze",     Amount = 1,  Recover = false },
                },
            });

            // Localised strings — wired via Jotunn's Localization manager so the
            // tokens above resolve in-game.
            LocalizationManager.Instance.AddLocalization(new Jotunn.Configs.LocalizationConfig("English")
            {
                Translations =
                {
                    { "item_lordshorn",      "Lord's Horn" },
                    { "item_lordshorn_desc", "A primal horn that calls forth the Lord of the current biome — if you have proven yourself a worthy hunter." },
                    { "enemy_necklord",      "Neck Lord" },
                    { "biomelords_summon_neck_start", "The marshes ripple… something ancient stirs." },
                    { "biomelords_summon_neck_end",   "The waters fall still. The Neck Lord is no more." },
                    { "biomelords_horn_fail_biome",   "The horn falls silent. No Lord rules this land." },
                    { "biomelords_horn_fail_kills",   "The horn refuses you. You are not yet a proven hunter here." },
                    { "biomelords_horn_fail_day",     "The horn lies silent under the sun. The Lord answers only by night." },

                    // Vanilla item-type label — token Valheim looks up for ItemType.Consumable.
                    // Override needed because the vanilla string table doesn't ship this key.
                    { "item_consumable",              "Consumable" },

                    // Phase 1C — trophies, pedestals, status effects, powers.
                    { "item_trophynecklord",          "Trophy of the Neck Lord" },
                    { "item_trophynecklord_desc",     "The horned skull of the Neck Lord. Mount it on a Lord's Pedestal to draw upon its spirit." },
                    { "piece_lordspedestal",          "Lord's Pedestal" },
                    { "piece_lordspedestal_desc",     "A gilded ceremonial stand that holds the trophy of a fallen Lord. Glows softly when erected. Place several side-by-side to form your own Hall of the Lords." },
                    { "se_necklordspirit",            "Fisher's Boon" },
                    { "se_necklordspirit_tooltip",    "The Neck Lord's gift. Your fishing casts have a chance to spare the bait, and a chance to land a bonus fish on the catch." },
                    { "se_necklordspirit_start",      "The Neck Lord's spirit settles upon your line." },
                    { "se_necklordspirit_stop",       "The Neck Lord's spirit drifts away." },

                    // Greydwarf Shaman Lord
                    { "enemy_greydwarflord",          "Greydwarf Shaman Lord" },
                    { "item_trophygreydwarflord",     "Trophy of the Greydwarf Shaman Lord" },
                    { "item_trophygreydwarflord_desc","The mossy skull of the Greydwarf Shaman Lord. Mount it on a Lord's Pedestal to draw upon its spirit." },
                    { "biomelords_summon_greydwarf_start", "The greenwood stirs… ancient eyes open." },
                    { "biomelords_summon_greydwarf_end",   "The forest grows still. The Shaman Lord falls." },
                    // Draugr Elite Lord
                    { "enemy_draugrlord",                  "Draugr Elite Lord" },
                    { "item_trophydraugrlord",             "Trophy of the Draugr Elite Lord" },
                    { "item_trophydraugrlord_desc",        "The crowned skull of the Draugr Elite Lord. Mount it on a Lord's Pedestal to draw upon its spirit." },
                    { "biomelords_summon_draugr_start",    "A miasma rises from the bog… the dead king walks." },
                    { "biomelords_summon_draugr_end",      "The bog grows quiet. The Draugr Lord returns to dust." },
                    { "biomelords_plague_cloud",           "A choking miasma billows out — do not stand in it." },
                    { "biomelords_undying_surge",          "The Draugr Lord tears the life-force from everything around it!" },
                    { "biomelords_undying_surge_fail",     "The Draugr Lord's hunger finds no prey — its surge fails!" },
                    { "se_draugrwound",                    "Wounded" },
                    { "se_draugrlordspirit",               "Iron Vein" },
                    { "se_draugrlordspirit_tooltip",       "The Draugr Lord's gift. Mining iron from Swamp ores has a chance to yield an extra piece." },
                    { "se_draugrlordspirit_start",         "The Draugr Lord's spirit hardens your strike." },
                    { "se_draugrlordspirit_stop",          "The Draugr Lord's spirit fades." },

                    { "se_greydwarflordspirit",            "Quick Sprout" },
                    { "se_greydwarflordspirit_tooltip",    "Planted crops within 30 m grow about 30 % faster while this blessing endures." },
                    { "se_greydwarflordspirit_start",      "The Greydwarf Shaman whispers to the seeds you've sown." },
                    { "se_greydwarflordspirit_stop",       "The Greydwarf Shaman's whisper fades." },
                    { "biomelords_pedestal_receive",  "Receive blessing" },
                    { "biomelords_pedestal_unknown_trophy", "This is not a Trophy of a Lord." },
                    { "biomelords_blessing_spent",    "The spirit is spent. Hunt the Lord again to draw upon it." },
                    { "biomelords_blessing_cooldown", "The spirit gathers — wait a moment." },
                    { "biomelords_blessing_lastuse",  "The Trophy crumbles to dust. The Lord's spirit is spent — hunt it anew to restore the blessing." },
                    { "biomelords_pedestal_locked",   "The Trophy is bound to the pedestal until its spirit is spent." },
                    { "biomelords_pedestal_lockedhint","Bound — cannot be removed until charges are spent." },
                    { "biomelords_pedestal_destroy_locked","The Pedestal cannot be removed while a Lord's trophy is mounted." },

                    // Forsaken Power: Tide's Grace (Neck Lord)
                    { "gp_tidesgrace",                "Tide's Grace" },
                    { "gp_tidesgrace_tooltip",        "The Neck Lord's mastery of water flows through you.\nWhile swimming, your stamina is restored instead of drained.\nWhile <b>Wet</b>, all of your melee attacks deal <b>+50%</b> damage — and the Wet status can no longer harm you." },
                    { "gp_tidesgrace_start",          "The tide answers your call." },
                    { "gp_tidesgrace_stop",           "The tide recedes from you." },

                    // Forest's Embrace (Greydwarf Shaman Lord)
                    { "gp_forestsembrace",            "Forest's Embrace" },
                    { "gp_forestsembrace_tooltip",    "The trees themselves shelter you.\nStanding by any mature tree counts as shelter.\nSit (use the /sit emote or a chair) beside a tree with no monsters within 30 metres to heal — the older the tree, the stronger the gift (1 HP near Beech up to 6 HP near Charred trees, every 3 seconds). After 60 seconds of seated rest the forest grants you a Rested buff. Older trees grant more comfort, extending the Rested time (Beech +1, Oak +2, Yggdrasil/Charred +3)." },
                    { "gp_forestsembrace_start",      "The forest folds itself around you." },
                    { "gp_forestsembrace_stop",       "The forest releases you." },

                    // Plague Bearer (Draugr Elite Lord)
                    { "gp_plaguebearer",              "Plague Bearer" },
                    { "gp_plaguebearer_tooltip",      "Poison cannot touch you. Your poison strikes burn fiercer." },
                    { "gp_plaguebearer_start",        "The rot is yours to wield." },
                    { "gp_plaguebearer_stop",         "The rot leaves you." },

                    // Howl of the Pack (Fenring Lord) — replaces Frostfang.
                    { "gp_howlofthepack",             "Howl of the Pack" },
                    { "gp_howlofthepack_tooltip",     "The Fenring's call answers.\nOn activation, every tamed creature within 30 m is fully healed and marked with the pack's ember light.\nA ghostly violet Phantom Wolf fights at your side for 60 s.\nAny tamed wolf within 30 m deals <b>+100%</b> damage for 10 minutes.\n\n<b>Synergy with Pack Whisperer:</b> if the blessing is active when you press F, tamed wolves take <b>−85%</b> damage (instead of −50%), the Phantom Wolf endures for 2 minutes and <b>ignores all incoming damage</b>, and your Blood Magic swells the pack — <b>50+</b> summons 2 wolves, <b>100</b> summons 3." },
                    { "gp_howlofthepack_start",       "The pack answers your call." },
                    { "gp_howlofthepack_stop",        "The pack returns to the wild." },
                    { "gp_howlofthepack_phantom",     "A phantom wolf joins your side." },
                    { "gp_howlofthepack_phantom_synergy", "The pack answers in full — a phantom wolf joins your side, and the blessing's bond runs deeper." },
                    { "enemy_phantomwolf",            "Phantom Wolf" },

                    // Fenring Lord enemy + trophy + Pack Whisperer blessing
                    { "enemy_fenringlord",            "Fenring Lord" },
                    { "item_trophyfenringlord",       "Trophy of the Fenring Lord" },
                    { "item_trophyfenringlord_desc",  "The frostbitten skull of the Fenring Lord. Mount it on a Lord's Pedestal to draw upon its spirit." },
                    { "biomelords_summon_fenring_start","The mountain howls… the pack stirs." },
                    { "biomelords_summon_fenring_end",  "The wind grows still. The Fenring Lord falls." },
                    { "se_fenringlordspirit",         "Pack Whisperer" },
                    { "se_fenringlordspirit_tooltip", "The Fenring Lord's gift. Tamed wolves within 30 m take <b>−50%</b> damage and breed <b>twice</b> as fast." },
                    { "se_fenringlordspirit_start",   "The pack senses one of its own." },
                    { "se_fenringlordspirit_stop",    "The pack's bond fades." },

                    // Lox Lord enemy + trophy + Harvester's Bounty blessing
                    { "enemy_loxlord",                "Lox Lord" },
                    { "item_trophyloxlord",           "Trophy of the Lox Lord" },
                    { "item_trophyloxlord_desc",      "The matted skull of the Lox Lord. Mount it on a Lord's Pedestal to draw upon its spirit." },
                    { "biomelords_summon_lox_start",  "The grasslands tremble… a great beast bellows." },
                    { "biomelords_summon_lox_end",    "The thunder of hooves ends. The Lox Lord falls." },
                    { "biomelords_lox_bellow",        "A bone-shaking bellow knocks you back!" },
                    { "biomelords_lox_bulwark",       "The Lox Lord digs in, hide hardening like stone!" },
                    { "se_loxlordspirit",             "Hearth Master" },
                    { "se_loxlordspirit_tooltip",     "The Lox Lord's gift. Food buffs you eat last <b>+100%</b> longer." },
                    { "se_loxlordspirit_start",       "The Lox Lord blesses your hearth." },
                    { "se_loxlordspirit_stop",        "The hearth's warmth fades." },

                    // Seeker Lord enemy + trophy + Refiner's Touch blessing
                    { "enemy_seekerlord",             "Seeker Lord" },
                    { "item_trophyseekerlord",        "Trophy of the Seeker Lord" },
                    { "item_trophyseekerlord_desc",   "The chitinous skull of the Seeker Lord, still humming with hive resonance. Mount it on a Lord's Pedestal to draw upon its spirit." },
                    { "biomelords_summon_seeker_start","The mist hums… the hive marks you." },
                    { "biomelords_summon_seeker_end", "The hum dies away. The Seeker Lord is undone." },
                    { "se_seekerlordspirit",          "Refiner's Touch" },
                    { "se_seekerlordspirit_tooltip",  "The Seeker Lord's gift. Smelters, Blast Furnaces, Spinning Wheels and Eitr Refineries near you have a chance to yield a bonus output." },
                    { "se_seekerlordspirit_start",    "The hive bends raw matter to your will." },
                    { "se_seekerlordspirit_stop",     "The hive's gift fades." },

                    // Fallen Valkyrie Lord enemy + trophy + Valkyrie's Ascent blessing
                    { "enemy_fallervalkyrielord",         "Fallen Valkyrie Lord" },
                    { "item_trophyfallervalkyrielord",    "Trophy of the Fallen Valkyrie Lord" },
                    { "item_trophyfallervalkyrielord_desc","The radiant, ash-streaked skull of the Fallen Valkyrie Lord. Mount it on a Lord's Pedestal to draw upon its spirit." },
                    { "biomelords_summon_fallervalkyrie_start","The ashen sky tears open… a fallen Valkyrie descends." },
                    { "biomelords_summon_fallervalkyrie_end", "The light fades from the sky. The Fallen Valkyrie Lord is slain." },
                    { "se_fallervalkyrielordspirit",         "Featherweight" },
                    { "se_fallervalkyrielordspirit_tooltip", "The Fallen Valkyrie Lord's gift. Her wings bear your burdens: carry up to <b>1000</b> weight with <b>no encumbrance penalty</b> — walk, run and recover stamina freely until you reach the cap. Also grants <b>+2 inventory rows</b>. Switch blessings and anything in those extra rows spills into a crate at your feet." },
                    { "se_fallervalkyrielordspirit_start",   "Your burdens turn weightless." },
                    { "se_fallervalkyrielordspirit_stop",    "The weight of the world returns." },
                    { "biomelords_featherweight_crate",      "Featherweight fades — your extra packs spill into a crate." },
                    { "biomelords_featherweight_dropped",    "Featherweight fades — your extra packs tumble to the ground." },

                    // Bull Rush (Lox Lord)
                    { "gp_bullrush",                  "Bull Rush" },
                    { "gp_bullrush_tooltip",          "Unstoppable. Cannot be staggered, attacks cost −50% stamina, adrenaline surges <b>+100%</b> with every strike." },
                    { "gp_bullrush_start",            "Nothing shall move you." },
                    { "gp_bullrush_stop",             "The strength of the beast leaves you." },

                    // Hive Sight (Seeker Lord) — replaces Hive Sense.
                    { "gp_hivesight",                 "Hive Sight" },
                    { "gp_hivesight_tooltip",         "The hive shows you its prey.\nFor 10 minutes, every hostile creature within 80 m is marked on your minimap and pulses with a faint sign — visible even through stone and mist." },
                    { "gp_hivesight_start",           "The hive opens its many eyes." },
                    { "gp_hivesight_stop",            "The hive closes its eyes." },

                    // Valkyrie's Rally — group support burst (Fallen Valkyrie Lord).
                    { "gp_valkyrieascension",         "Valkyrie's Rally" },
                    { "gp_valkyrieascension_tooltip", "The fallen Valkyrie answers your call to arms.\nEvery player within <b>20 m</b> — you and your kin — is restored in an instant:\n• <b>Health, Stamina and Eitr</b> filled to the brim\n• <b>Adrenaline</b> maxed, if a trinket is worn\n• A <b>max-level shield</b> bubble, as from the Staff of Protection\n• A <b>20-minute Rested</b> buff" },
                    { "gp_valkyrieascension_start",   "You raise the Valkyrie's banner." },
                    { "gp_valkyrieascension_stop",    "The banner lowers." },
                    { "gp_valkyrieascension_rallied", "The Valkyrie's grace restores you." },

                    { "se_forestsit",                 "Resting under a tree" },
                    { "se_forestsit_tooltip",         "Seated beneath the boughs. Stay seated long enough and the forest will grant you the Rested buff." },
                    { "biomelords_power_awarded",     "The Lord's spirit chooses you — its power is yours." },
                },
            });

            // Strip Tankard-specific wielding behaviour (it's normally a held/equipped
            // drinking item with its own equip + consume status effects and animation
            // state) so the Horn can't be wielded — it's a pure use-from-inventory item.
            var drop = item.ItemPrefab.GetComponent<ItemDrop>();
            if (drop != null && drop.m_itemData != null && drop.m_itemData.m_shared != null)
            {
                var s = drop.m_itemData.m_shared;
                s.m_equipStatusEffect    = null;
                s.m_setStatusEffect      = null;
                s.m_attackStatusEffect   = null;
                s.m_consumeStatusEffect  = null;
                s.m_setName              = "";
                s.m_setSize              = 0;
                s.m_animationState       = ItemDrop.ItemData.AnimationState.Unarmed;

                // Tooltip type label: Tankard shows as a wieldable utility item.
                // "Consumable" fits the Horn (one-shot use → destroyed), can't be
                // equipped/wielded, and reads correctly to the player.
                s.m_itemType        = ItemDrop.ItemData.ItemType.Consumable;
                s.m_maxStackSize    = 1;
                s.m_attachOverride  = ItemDrop.ItemData.ItemType.None; // clear equip-slot wielding
                s.m_useDurability   = false;
                s.m_questItem       = false;
                Jotunn.Logger.LogInfo(
                    $"[BiomeLords] LordsHorn shared: itemType={s.m_itemType}, attachOverride={s.m_attachOverride}");
            }

            // Retint visuals so it doesn't look like a vanilla Anniversary Tankard.
            // Deep teal/cyan = water/Neck theme; far enough from the Tankard's wood/gold tones.
            var bodyTint     = new Color(0.15f, 0.55f, 0.85f, 1f);
            var emissionTint = new Color(0.05f, 0.70f, 1.00f, 1f);
            RetintPrefab(item.ItemPrefab, bodyTint, emissionTint);

            // Render a fresh icon from the now-tinted prefab.
            try
            {
                var req = new RenderManager.RenderRequest(item.ItemPrefab)
                {
                    Width = 64, Height = 64, UseCache = true,
                    // Ties the on-disk icon cache key to our mod's version (not just
                    // the prefab name + game version) so a stale icon from before the
                    // Wishbone→TankardAnniversary clone swap can't be reused forever.
                    TargetPlugin = Plugin.Instance.Info.Metadata,
                };
                var sprite = RenderManager.Instance.Render(req);
                if (sprite != null && drop != null)
                {
                    drop.m_itemData.m_shared.m_icons = new[] { sprite };
                }
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogWarning($"[BiomeLords] Icon render failed: {ex.Message}");
            }

            ItemManager.Instance.AddItem(item);
            Jotunn.Logger.LogInfo($"[BiomeLords] Registered item: {LordsHornPrefab}");
        }

        /// <summary>
        /// Replaces every renderer's materials with new instances (so we don't
        /// mutate the vanilla Wishbone's shared assets), tints them, recolors
        /// any ParticleSystem and Light, so the held + dropped + glow visuals
        /// all read as distinctly different.
        /// </summary>
        private static void RetintPrefab(GameObject prefab, Color body, Color emission)
        {
            if (prefab == null) return;

            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                var srcs = r.sharedMaterials;
                if (srcs == null || srcs.Length == 0) continue;
                var copies = new Material[srcs.Length];
                for (int i = 0; i < srcs.Length; i++)
                {
                    if (srcs[i] == null) continue;
                    var m = new Material(srcs[i]);
                    if (m.HasProperty("_Color"))         m.color = body;
                    if (m.HasProperty("_MainColor"))     m.SetColor("_MainColor", body);
                    if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
                    copies[i] = m;
                }
                r.sharedMaterials = copies;
            }

            foreach (var ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.startColor = new ParticleSystem.MinMaxGradient(emission);
            }

            foreach (var light in prefab.GetComponentsInChildren<Light>(true))
            {
                light.color = emission;
            }
        }
    }
}
