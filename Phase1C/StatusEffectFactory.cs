using System.Collections.Generic;
using UnityEngine;
using Jotunn.Managers;
using Jotunn.Entities;
using BiomeLords.Config;
using BiomeLords.Util;

namespace BiomeLords.Phase1C
{
    /// <summary>
    /// Registers per-Lord passive status effects. Phase 1C-1 ships the Neck
    /// Lord's spirit; the rest follow in 1D.
    ///
    /// Effects use a short TTL (12s) and are reapplied every few seconds by the
    /// RewardSystem while the player is within range of a Lord's Pedestal that
    /// has the matching trophy mounted. When the player walks away, the SE
    /// expires naturally.
    /// </summary>
    public static class StatusEffectFactory
    {
        public const string NeckLordSpiritSE      = "SE_NeckLordSpirit";
        public const string GreydwarfLordSpiritSE = "SE_GreydwarfLordSpirit";
        public const string DraugrLordSpiritSE    = "SE_DraugrLordSpirit";
        public const string FenringLordSpiritSE   = "SE_FenringLordSpirit";
        public const string LoxLordSpiritSE       = "SE_LoxLordSpirit";
        public const string SeekerLordSpiritSE    = "SE_SeekerLordSpirit";
        public const string FallerValkyrieLordSpiritSE = "SE_FallerValkyrieLordSpirit";

        /// <summary>Direct references to our registered SEs.</summary>
        public static readonly Dictionary<string, StatusEffect> ByName =
            new Dictionary<string, StatusEffect>();

        /// <summary>Subset of ByName — only SEs that are "blessings" (mutually
        /// exclusive — applying one removes the others). Populated as each
        /// blessing is registered.</summary>
        public static readonly HashSet<int> BlessingHashes = new HashSet<int>();

        public static void RegisterAll()
        {
            var neckSpirit = ScriptableObject.CreateInstance<SE_Stats>();
            neckSpirit.name      = NeckLordSpiritSE;
            neckSpirit.m_name    = "$se_necklordspirit";
            neckSpirit.m_tooltip = "$se_necklordspirit_tooltip";
            // Duration set per-application via SetTtl when blessing is granted —
            // we leave the default here so the SE survives long enough on its own
            // if the granting code somehow skips updating it.
            // Permanent — blessings persist until the player switches to another.
            neckSpirit.m_ttl     = 0f;

            // HUD messages on activate / expire.
            neckSpirit.m_startMessageType = MessageHud.MessageType.Center;
            neckSpirit.m_startMessage     = "$se_necklordspirit_start";
            neckSpirit.m_stopMessageType  = MessageHud.MessageType.Center;
            neckSpirit.m_stopMessage      = "$se_necklordspirit_stop";

            // Start FX: cyan ring + soft thunder crack (resolved against vanilla
            // prefabs at registration time).
            neckSpirit.m_startEffects = BuildEffects(new[]
            {
                "vfx_lootspawn", "vfx_HitSparks", "fx_himminafl_aoe",
            });
            neckSpirit.m_stopEffects = BuildEffects(new[]
            {
                "vfx_corpse_destruction_small",
            });

            // No innate stat modifiers — Fisher's Boon's effects (bait save +
            // bonus fish) are driven by FishingFloat_Setup_FisherBoon and
            // Fish_Pickup_FisherBoon Harmony patches that check for this SE.

            var custom = new CustomStatusEffect(neckSpirit, fixReference: false);
            ItemManager.Instance.AddStatusEffect(custom);
            ByName[NeckLordSpiritSE] = neckSpirit;
            BlessingHashes.Add(NeckLordSpiritSE.GetStableHashCode());
            Jotunn.Logger.LogInfo($"[BiomeLords] Registered status effect: {NeckLordSpiritSE}");

            BuildGreydwarfLordSpirit();
            BuildDraugrLordSpirit();
            BuildFenringLordSpirit();
            BuildLoxLordSpirit();
            BuildSeekerLordSpirit();
            BuildFallerValkyrieLordSpirit();
        }

        /// <summary>
        /// Fallen Valkyrie Lord blessing — Featherweight. A pure hauling blessing
        /// (marker SE — no SE_Stats fields are set). While active it:
        ///   • Raises your effective carry cap to LordConfig.FallerValkyrieWeightCap
        ///     (default 1000) with NO over-encumbrance penalty below it — you walk,
        ///     run and regen stamina normally and show no encumbered animation. The
        ///     normal encumbered state only triggers at the cap. Driven by
        ///     FeatherweightEncumbrancePatch (Player.GetMaxCarryWeight).
        ///   • Grants LordConfig.FallerValkyrieExtraRows extra inventory rows; see
        ///     FeatherweightInventory.
        /// Both behaviours need Harmony patches / runtime logic — they are not
        /// SE_Stats fields — so this SE carries no stat modifiers itself.
        /// </summary>
        private static void BuildFallerValkyrieLordSpirit()
        {
            var spirit = ScriptableObject.CreateInstance<SE_Stats>();
            spirit.name     = FallerValkyrieLordSpiritSE;
            spirit.m_name    = "$se_fallervalkyrielordspirit";
            spirit.m_tooltip = "$se_fallervalkyrielordspirit_tooltip";
            spirit.m_ttl     = 0f;

            spirit.m_startMessageType = MessageHud.MessageType.Center;
            spirit.m_startMessage     = "$se_fallervalkyrielordspirit_start";
            spirit.m_stopMessageType  = MessageHud.MessageType.Center;
            spirit.m_stopMessage      = "$se_fallervalkyrielordspirit_stop";

            spirit.m_startEffects = BuildEffects(new[] {
                "vfx_lootspawn", "vfx_HitSparks", "fx_himminafl_aoe"
            });
            spirit.m_stopEffects  = BuildEffects(new[] { "vfx_corpse_destruction_small" });

            // No SE_Stats modifiers — the raised carry cap (FeatherweightEncumbrancePatch)
            // and the extra inventory rows (FeatherweightInventory) are both driven
            // by runtime logic gated on this SE being active.

            var custom = new CustomStatusEffect(spirit, fixReference: false);
            ItemManager.Instance.AddStatusEffect(custom);
            ByName[FallerValkyrieLordSpiritSE] = spirit;
            BlessingHashes.Add(FallerValkyrieLordSpiritSE.GetStableHashCode());
            Jotunn.Logger.LogInfo($"[BiomeLords] Registered status effect: {FallerValkyrieLordSpiritSE}");
        }

        /// <summary>
        /// Seeker Lord blessing — Refiner's Touch. Marker SE; the reward
        /// (chance for bonus output when a Smelter/Blast Furnace/Spinning Wheel
        /// completes a product) is driven by RefinersTouchPatch.
        /// </summary>
        private static void BuildSeekerLordSpirit()
        {
            var spirit = ScriptableObject.CreateInstance<SE_Stats>();
            spirit.name     = SeekerLordSpiritSE;
            spirit.m_name    = "$se_seekerlordspirit";
            spirit.m_tooltip = "$se_seekerlordspirit_tooltip";
            spirit.m_ttl     = 0f;

            spirit.m_startMessageType = MessageHud.MessageType.Center;
            spirit.m_startMessage     = "$se_seekerlordspirit_start";
            spirit.m_stopMessageType  = MessageHud.MessageType.Center;
            spirit.m_stopMessage      = "$se_seekerlordspirit_stop";

            spirit.m_startEffects = BuildEffects(new[] {
                "vfx_lootspawn", "vfx_HitSparks", "vfx_seeker_attack"
            });
            spirit.m_stopEffects  = BuildEffects(new[] { "vfx_corpse_destruction_small" });

            var custom = new CustomStatusEffect(spirit, fixReference: false);
            ItemManager.Instance.AddStatusEffect(custom);
            ByName[SeekerLordSpiritSE] = spirit;
            BlessingHashes.Add(SeekerLordSpiritSE.GetStableHashCode());
            Jotunn.Logger.LogInfo($"[BiomeLords] Registered status effect: {SeekerLordSpiritSE}");
        }

        /// <summary>
        /// Lox Lord blessing — Harvester's Bounty. Marker SE; the reward
        /// (chance for bonus Barley/Flax on Pickable harvest) is driven by
        /// HarvestersBountyPatch.
        /// </summary>
        private static void BuildLoxLordSpirit()
        {
            var spirit = ScriptableObject.CreateInstance<SE_Stats>();
            spirit.name     = LoxLordSpiritSE;
            spirit.m_name    = "$se_loxlordspirit";
            spirit.m_tooltip = "$se_loxlordspirit_tooltip";
            spirit.m_ttl     = 0f;

            spirit.m_startMessageType = MessageHud.MessageType.Center;
            spirit.m_startMessage     = "$se_loxlordspirit_start";
            spirit.m_stopMessageType  = MessageHud.MessageType.Center;
            spirit.m_stopMessage      = "$se_loxlordspirit_stop";

            spirit.m_startEffects = BuildEffects(new[] {
                "vfx_lootspawn", "vfx_HitSparks", "vfx_gdking_stomp"
            });
            spirit.m_stopEffects  = BuildEffects(new[] { "vfx_corpse_destruction_small" });

            var custom = new CustomStatusEffect(spirit, fixReference: false);
            ItemManager.Instance.AddStatusEffect(custom);
            ByName[LoxLordSpiritSE] = spirit;
            BlessingHashes.Add(LoxLordSpiritSE.GetStableHashCode());
            Jotunn.Logger.LogInfo($"[BiomeLords] Registered status effect: {LoxLordSpiritSE}");
        }

        /// <summary>
        /// Fenring Lord blessing — Pack Whisperer. Marker SE only; the actual
        /// reward (tamed wolves take −50% damage and deal +25% damage while
        /// the bearer is nearby) is driven by TamedWolfPatch.
        /// </summary>
        private static void BuildFenringLordSpirit()
        {
            var spirit = ScriptableObject.CreateInstance<SE_Stats>();
            spirit.name     = FenringLordSpiritSE;
            spirit.m_name    = "$se_fenringlordspirit";
            spirit.m_tooltip = "$se_fenringlordspirit_tooltip";
            spirit.m_ttl     = 0f; // permanent

            spirit.m_startMessageType = MessageHud.MessageType.Center;
            spirit.m_startMessage     = "$se_fenringlordspirit_start";
            spirit.m_stopMessageType  = MessageHud.MessageType.Center;
            spirit.m_stopMessage      = "$se_fenringlordspirit_stop";

            spirit.m_startEffects = BuildEffects(new[] {
                "vfx_lootspawn", "vfx_HitSparks", "fx_himminafl_aoe"
            });
            spirit.m_stopEffects  = BuildEffects(new[] { "vfx_corpse_destruction_small" });

            var custom = new CustomStatusEffect(spirit, fixReference: false);
            ItemManager.Instance.AddStatusEffect(custom);
            ByName[FenringLordSpiritSE] = spirit;
            BlessingHashes.Add(FenringLordSpiritSE.GetStableHashCode());
            Jotunn.Logger.LogInfo($"[BiomeLords] Registered status effect: {FenringLordSpiritSE}");
        }

        private static void BuildDraugrLordSpirit()
        {
            var spirit = ScriptableObject.CreateInstance<SE_Stats>();
            spirit.name     = DraugrLordSpiritSE;
            spirit.m_name    = "$se_draugrlordspirit";
            spirit.m_tooltip = "$se_draugrlordspirit_tooltip";
            spirit.m_ttl     = 0f; // permanent

            spirit.m_startMessageType = MessageHud.MessageType.Center;
            spirit.m_startMessage     = "$se_draugrlordspirit_start";
            spirit.m_stopMessageType  = MessageHud.MessageType.Center;
            spirit.m_stopMessage      = "$se_draugrlordspirit_stop";

            spirit.m_startEffects = BuildEffects(new[] {
                "vfx_lootspawn", "vfx_HitSparks", "vfx_swamp_mist"
            });
            spirit.m_stopEffects  = BuildEffects(new[] { "vfx_corpse_destruction_small" });

            var custom = new CustomStatusEffect(spirit, fixReference: false);
            ItemManager.Instance.AddStatusEffect(custom);
            ByName[DraugrLordSpiritSE] = spirit;
            BlessingHashes.Add(DraugrLordSpiritSE.GetStableHashCode());
            Jotunn.Logger.LogInfo($"[BiomeLords] Registered status effect: {DraugrLordSpiritSE}");
        }

        /// <summary>
        /// Greydwarf Shaman Lord blessing — a marker SE with no inherent stat
        /// modifiers. The actual reward (slow healing near Oak trees) is
        /// applied by OakHealingService while this SE is present.
        /// </summary>
        private static void BuildGreydwarfLordSpirit()
        {
            var spirit = ScriptableObject.CreateInstance<SE_Stats>();
            spirit.name     = GreydwarfLordSpiritSE;
            spirit.m_name   = "$se_greydwarflordspirit";
            spirit.m_tooltip = "$se_greydwarflordspirit_tooltip";
            spirit.m_ttl    = 0f;

            spirit.m_startMessageType = MessageHud.MessageType.Center;
            spirit.m_startMessage     = "$se_greydwarflordspirit_start";
            spirit.m_stopMessageType  = MessageHud.MessageType.Center;
            spirit.m_stopMessage      = "$se_greydwarflordspirit_stop";

            spirit.m_startEffects = BuildEffects(new[] {
                "vfx_lootspawn", "vfx_HitSparks", "fx_gdking_rootspawn"
            });
            spirit.m_stopEffects  = BuildEffects(new[] { "vfx_corpse_destruction_small" });

            var custom = new CustomStatusEffect(spirit, fixReference: false);
            ItemManager.Instance.AddStatusEffect(custom);
            ByName[GreydwarfLordSpiritSE] = spirit;
            BlessingHashes.Add(GreydwarfLordSpiritSE.GetStableHashCode());
            Jotunn.Logger.LogInfo($"[BiomeLords] Registered status effect: {GreydwarfLordSpiritSE}");
        }

        /// <summary>Builds a minimal EffectList from vanilla prefab names that exist.</summary>
        private static EffectList BuildEffects(string[] prefabNames)
        {
            var list = new List<EffectList.EffectData>();
            foreach (var n in prefabNames)
            {
                var p = PrefabManager.Instance.GetPrefab(n);
                if (p == null) continue;
                list.Add(new EffectList.EffectData
                {
                    m_prefab        = p,
                    m_enabled       = true,
                    m_attach        = false,
                    m_follow        = false,
                    m_scale         = false,
                    m_randomRotation = true,
                });
            }
            return new EffectList { m_effectPrefabs = list.ToArray() };
        }

        /// <summary>Always refresh the SE icon from the trophy's current icon
        /// (the trophy icon may have been re-rendered after SE registration).</summary>
        public static void RefreshIcon(StatusEffect se, string trophyPrefabName)
        {
            if (se == null) return;
            var trophy = Jotunn.Managers.PrefabManager.Instance.GetPrefab(trophyPrefabName)?.GetComponent<ItemDrop>();
            var icons  = trophy?.m_itemData?.m_shared?.m_icons;
            if (icons != null && icons.Length > 0 && icons[0] != null)
                se.m_icon = icons[0];
        }

        /// <summary>
        /// Icons must be assigned before the SE first renders, but Trophy icons
        /// are rendered AFTER StatusEffectFactory runs in OnVanillaPrefabsAvailable.
        /// So we lazily steal an icon on first use: first try the Lord's own
        /// trophy icon, then fall back to the first non-null vanilla SE icon.
        /// </summary>
        public static void EnsureIcon(StatusEffect se, string trophyPrefabName)
        {
            if (se == null || se.m_icon != null) return;

            // 1. Lord's own trophy icon — visually thematic.
            var trophy = Jotunn.Managers.PrefabManager.Instance.GetPrefab(trophyPrefabName)?.GetComponent<ItemDrop>();
            var icons  = trophy?.m_itemData?.m_shared?.m_icons;
            if (icons != null && icons.Length > 0 && icons[0] != null)
            {
                se.m_icon = icons[0];
                return;
            }

            // 2. Fallback: borrow the first vanilla SE icon we can find.
            var db = ObjectDB.instance;
            if (db == null || db.m_StatusEffects == null) return;
            foreach (var s in db.m_StatusEffects)
            {
                if (s == null || s.m_icon == null) continue;
                se.m_icon = s.m_icon;
                return;
            }
        }
    }
}
