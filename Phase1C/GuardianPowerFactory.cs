using System.Collections.Generic;
using UnityEngine;
using Jotunn.Managers;
using Jotunn.Entities;

namespace BiomeLords.Phase1C
{
    /// <summary>
    /// Per-Lord Forsaken Powers. Built as SE_Stats with m_cooldown set, then
    /// registered into ObjectDB via Jotunn so Player.SetGuardianPower(name)
    /// can find them. Auto-granted to the killer at Lord-defeat time.
    /// </summary>
    public static class GuardianPowerFactory
    {
        public const string NeckLordGP      = "GP_TidesGrace";
        public const string GreydwarfLordGP = "GP_ForestsEmbrace";
        public const string DraugrLordGP    = "GP_PlagueBearer";
        public const string FenringLordGP   = "GP_HowlOfThePack";
        public const string LoxLordGP       = "GP_BullRush";
        public const string SeekerLordGP    = "GP_HiveSense";
        public const string FallerValkyrieLordGP = "GP_ValkyrieAscension";

        public const float ActiveDuration = 600f;   // 10 min — claim is hard-gated by Lord-kill
        public const float Cooldown       = 1200f;  // 20 min — matches vanilla GPs
        public const float InstantWindow  = 4f;     // brief marker for one-shot burst powers

        /// <summary>SE name → registered SE instance.</summary>
        public static readonly Dictionary<string, StatusEffect> ByName =
            new Dictionary<string, StatusEffect>();

        public static void RegisterAll()
        {
            Register(NeckLordGP,      "gp_tidesgrace",      ConfigureTidesGrace);
            Register(GreydwarfLordGP, "gp_forestsembrace",  ConfigureForestsEmbrace);
            Register(DraugrLordGP,    "gp_plaguebearer",    ConfigurePlagueBearer);
            Register(FenringLordGP,   "gp_howlofthepack",   ConfigureHowlOfThePack);
            Register(LoxLordGP,       "gp_bullrush",        ConfigureBullRush);
            Register(SeekerLordGP,    "gp_hivesight",       ConfigureHiveSight);
            // Valkyrie's Rally fires once on activation (ValkyrieRallyService),
            // so it only needs a brief marker window — not the full 10 min.
            Register(FallerValkyrieLordGP, "gp_valkyrieascension", ConfigureValkyrieAscension, InstantWindow);
        }

        private static void Register(string seName, string locKey, System.Action<SE_Stats> configure,
                                     float ttl = ActiveDuration)
        {
            var se = ScriptableObject.CreateInstance<SE_Stats>();
            se.name      = seName;
            se.m_name    = "$" + locKey;
            se.m_tooltip = "$" + locKey + "_tooltip";
            se.m_ttl     = ttl;
            se.m_cooldown = Cooldown;

            se.m_startMessageType = MessageHud.MessageType.Center;
            se.m_startMessage     = "$" + locKey + "_start";
            se.m_stopMessageType  = MessageHud.MessageType.Center;
            se.m_stopMessage      = "$" + locKey + "_stop";

            // Cinematic start — flash + ring + roar. No green effects:
            // "fx_summon_start" (green summon burst) and "vfx_prespawn" (teal-green
            // spawn swirl) are both excluded.
            se.m_startEffects = BuildEffects(new[]
            {
                "vfx_lootspawn",
                "fx_himminafl_aoe",
                "vfx_HitSparks",
                "fx_Fader_Roar",
            });
            se.m_stopEffects  = BuildEffects(new[] { "vfx_corpse_destruction_small", "vfx_HitSparks" });

            configure(se);

            var custom = new CustomStatusEffect(se, fixReference: false);
            ItemManager.Instance.AddStatusEffect(custom);
            ByName[seName] = se;
            Jotunn.Logger.LogInfo($"[BiomeLords] Registered guardian power: {seName}");
        }

        // ---- Per-Lord effect configuration ---------------------------------

        private static void ConfigureTidesGrace(SE_Stats se)
        {
            // Marker only — no innate stat modifiers. While active:
            //   • PowerEffectsService restores stamina while the player swims.
            //   • Character_Damage_TidesGraceMelee grants +50% melee while Wet.
            //   • NeckWetImmunityPatch nullifies the Wet status's debuffs.
        }

        private static void ConfigureForestsEmbrace(SE_Stats se)
        {
            // Marker only — no innate stats. ForestEmbraceService ticks while
            // this SE is on the player and: heals based on the strongest tree
            // within 8 m, and (after 60 s safe-sit) elevates comfort via the
            // ForestEmbraceComfortPatch so vanilla grants a longer Rested.
        }

        private static void ConfigurePlagueBearer(SE_Stats se)
        {
            // Full poison immunity + +75% poison damage output.
            se.m_mods = new List<HitData.DamageModPair>
            {
                new HitData.DamageModPair { m_type = HitData.DamageType.Poison, m_modifier = HitData.DamageModifier.Immune },
            };
            se.m_percentigeDamageModifiers = new HitData.DamageTypes { m_poison = 0.75f };
            se.m_healthRegenMultiplier     = 1.5f;
        }

        private static void ConfigureHowlOfThePack(SE_Stats se)
        {
            // Marker only. While active, PowerEffectsService:
            //   • amplifies nearby tamed-wolf damage via TamedWolfPatch's check
            //   • spawns a phantom wolf companion at activation (auto-despawn)
            // In synergy with Pack Whisperer the phantom wolves are invulnerable
            // (PhantomWolfInvulnPatch) and Blood Magic scales the pack size
            // (1 → 2 at skill 50+ → 3 at skill 100).
            // No innate stat mods on the player — the gift is the pack.
        }

        private static void ConfigureBullRush(SE_Stats se)
        {
            // Trimmed — Clubs skill and physical resists were duplicates of
            // vanilla buffs/Forsaken Powers, so this version is just the
            // "unstoppable" identity: stagger-immune + free attacks + adrenaline.
            se.m_staggerModifier            = -1.0f;   // cannot be staggered
            se.m_adrenalineModifier         = 1.0f;     // +100% adrenaline surge
            se.m_attackStaminaUseModifier   = -0.50f;   // -50% attack stamina cost
        }

        private static void ConfigureHiveSight(SE_Stats se)
        {
            // Marker only. While active, HiveSightService:
            //   • Adds a red minimap pin for every hostile creature within
            //     80 m (refreshed every second, pruned on death / distance).
            //   • Pulses a glow above each hostile so they're visible-ish
            //     through walls when the FX is in-engine view.
        }

        private static void ConfigureValkyrieAscension(SE_Stats se)
        {
            // Marker only — Valkyrie's Rally. No innate stat mods. On activation,
            // ValkyrieRallyService fires a one-shot support burst that fully
            // restores HP / Stamina / Eitr (and max Adrenaline if a trinket is
            // equipped), wraps a max-level StaffShield bubble, and grants a 20-min
            // Rested buff to every player within range of the caster.
        }

        // ---- FX & icon helpers ---------------------------------------------

        private static EffectList BuildEffects(string[] prefabNames)
        {
            var list = new List<EffectList.EffectData>();
            foreach (var n in prefabNames)
            {
                var p = PrefabManager.Instance.GetPrefab(n);
                if (p == null) continue;
                list.Add(new EffectList.EffectData
                {
                    m_prefab         = p,
                    m_enabled        = true,
                    m_attach         = false,
                    m_follow         = false,
                    m_scale          = false,
                    m_randomRotation = true,
                });
            }
            return new EffectList { m_effectPrefabs = list.ToArray() };
        }

        public static void EnsureIcon(StatusEffect se, string trophyPrefabName)
        {
            if (se == null || se.m_icon != null) return;
            var trophy = PrefabManager.Instance.GetPrefab(trophyPrefabName)?.GetComponent<ItemDrop>();
            var icons  = trophy?.m_itemData?.m_shared?.m_icons;
            if (icons != null && icons.Length > 0 && icons[0] != null)
            {
                se.m_icon = icons[0];
                return;
            }
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
