using UnityEngine;
using Jotunn.Managers;
using Jotunn.Entities;
using Jotunn.Configs;
using BiomeLords.Util;

namespace BiomeLords.Phase1B
{
    /// <summary>
    /// Builds Lord creature prefabs by cloning vanilla ones and tweaking
    /// scale / tint / base health. Runtime scaling (boss-key based) is
    /// applied later at spawn time in SummonService.
    /// </summary>
    public static class CreatureFactory
    {
        public const string NeckLordPrefab              = "NeckLord";
        public const string GreydwarfShamanLordPrefab   = "GreydwarfShamanLord";
        public const string DraugrEliteLordPrefab       = "DraugrEliteLord";
        public const string FenringLordPrefab           = "FenringLord";
        public const string LoxLordPrefab               = "LoxLord";
        public const string SeekerLordPrefab            = "SeekerLord";
        public const string FallerValkyrieLordPrefab    = "FallerValkyrieLord";

        public static void RegisterAll()
        {
            BuildNeckLord();
            BuildGreydwarfShamanLord();
            BuildDraugrEliteLord();
            BuildFenringLord();
            BuildLoxLord();
            BuildSeekerLord();
            BuildFallerValkyrieLord();
        }

        private static void BuildNeckLord()
        {
            var clone = PrefabManager.Instance.CreateClonedPrefab(NeckLordPrefab, "Neck");
            if (clone == null)
            {
                Jotunn.Logger.LogError("[BiomeLords] Failed to clone Neck prefab.");
                return;
            }

            // 1.6x scale — chunky but still recognisable.
            clone.transform.localScale = Vector3.one * 1.6f;

            // Crimson tint on every renderer.
            var tint = new Color(0.9f, 0.25f, 0.25f, 1f);
            foreach (var r in clone.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var m in r.materials)
                {
                    if (m == null) continue;
                    if (m.HasProperty("_Color")) m.color = tint;
                    if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", tint * 0.4f);
                }
            }

            // Base stat buff — runtime scaling layered on top at spawn.
            var humanoid = clone.GetComponent<Humanoid>();
            if (humanoid != null)
            {
                humanoid.m_health = 500f;          // matched to Eikthyr (Meadows boss)
                humanoid.m_name = "$enemy_necklord";
            }

            var character = clone.GetComponent<Character>();
            if (character != null)
            {
                character.m_boss = true;            // shows boss bar
            }

            var monsterAI = clone.GetComponent<MonsterAI>();
            if (monsterAI != null)
            {
                monsterAI.m_avoidFire = false;      // vanilla Neck flees fire; the Lord doesn't
            }

            // Drops: copper + the Lord's trophy. Both are added at spawn so we
            // can adjust if the trophy prefab isn't yet registered at this point.
            var drop = clone.GetComponent<CharacterDrop>();
            if (drop != null)
            {
                drop.m_drops.Clear();
                drop.m_drops.Add(new CharacterDrop.Drop
                {
                    m_prefab          = PrefabManager.Instance.GetPrefab("Copper"),
                    m_amountMin       = 3,
                    m_amountMax       = 5,
                    m_chance          = 1f,
                    m_onePerPlayer    = false,
                    m_levelMultiplier = false,
                });
                drop.m_drops.Add(new CharacterDrop.Drop
                {
                    m_prefab          = PrefabManager.Instance.GetPrefab(Phase1C.TrophyFactory.NeckLordTrophy)
                                       ?? PrefabManager.Instance.GetPrefab("TrophyNeck"), // fallback if trophy not yet registered
                    m_amountMin       = 1,
                    m_amountMax       = 1,
                    m_chance          = 1f,
                    m_onePerPlayer    = true,
                    m_levelMultiplier = false,
                });
            }

            // Attach the per-instance brain (Tide Caller + Frenzy).
            clone.AddComponent<NeckLordBrain>();

            var custom = new CustomCreature(clone, fixReference: true);
            CreatureManager.Instance.AddCreature(custom);

            RegisteredLords.Register(NeckLordPrefab, Phase1B.EventFactory.NeckLordEvent, "neck_lord");

            Jotunn.Logger.LogInfo($"[BiomeLords] Registered creature: {NeckLordPrefab}");
        }

        private static void BuildGreydwarfShamanLord()
        {
            var clone = PrefabManager.Instance.CreateClonedPrefab(GreydwarfShamanLordPrefab, "Greydwarf_Shaman");
            if (clone == null)
            {
                Jotunn.Logger.LogError("[BiomeLords] Failed to clone Greydwarf_Shaman prefab.");
                return;
            }

            // 1.7x scale — chunkier than Neck Lord; reads as Black Forest tier.
            clone.transform.localScale = Vector3.one * 1.7f;

            // Skip material edits (vanilla shader floods the whole mesh with
            // any emission). Distinct identity comes from a child Light + a
            // looping particle aura — both rendered independently of the body
            // shader so they can't bleed across the entire model.
            AttachLordAura(clone, lightColor: new Color(0.35f, 1.00f, 0.30f));

            var humanoid = clone.GetComponent<Humanoid>();
            if (humanoid != null)
            {
                humanoid.m_health = 2500f;         // matched to The Elder (Black Forest boss)
                humanoid.m_name   = "$enemy_greydwarflord";
            }
            var character = clone.GetComponent<Character>();
            if (character != null) character.m_boss = true;

            var drop = clone.GetComponent<CharacterDrop>();
            if (drop != null)
            {
                drop.m_drops.Clear();
                drop.m_drops.Add(new CharacterDrop.Drop
                {
                    m_prefab    = PrefabManager.Instance.GetPrefab(Phase1C.TrophyFactory.GreydwarfLordTrophy)
                               ?? PrefabManager.Instance.GetPrefab("TrophyGreydwarfShaman"),
                    m_amountMin = 1, m_amountMax = 1, m_chance = 1f, m_onePerPlayer = true,
                });
                drop.m_drops.Add(new CharacterDrop.Drop
                {
                    m_prefab    = PrefabManager.Instance.GetPrefab("Bronze"),
                    m_amountMin = 3, m_amountMax = 5, m_chance = 1f,
                });
                drop.m_drops.Add(new CharacterDrop.Drop
                {
                    m_prefab    = PrefabManager.Instance.GetPrefab("SurtlingCore"),
                    m_amountMin = 1, m_amountMax = 2, m_chance = 1f,
                });
                drop.m_drops.Add(new CharacterDrop.Drop
                {
                    m_prefab    = PrefabManager.Instance.GetPrefab("Coal"),
                    m_amountMin = 5, m_amountMax = 10, m_chance = 1f,
                });
            }

            // The vanilla shaman heal item must be stripped — its AoE spawns through
            // Character.Damage() and LordDamageBoostPatch converts the tiny heal-aoe
            // damage into full lord blunt damage, hitting both allies and the lord itself.
            // BEFORE stripping, capture its real heal-cast animation + effect prefabs so
            // the brain (TryHealingResonance) can replay the genuine cast in code.
            if (humanoid != null)
            {
                GameObject healItem = null;
                foreach (var item in humanoid.m_defaultItems)
                    if (item != null && item.name.IndexOf("heal", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    { healItem = item; break; }

                if (healItem != null)
                {
                    var idrop = healItem.GetComponent<ItemDrop>();
                    if (idrop != null && idrop.m_itemData != null && idrop.m_itemData.m_shared != null)
                    {
                        var shared = idrop.m_itemData.m_shared;
                        string anim = shared.m_attack != null ? shared.m_attack.m_attackAnimation : null;
                        var fx = new System.Collections.Generic.List<GameObject>();
                        CollectEffects(shared.m_hitEffect,     fx);
                        CollectEffects(shared.m_startEffect,   fx);
                        CollectEffects(shared.m_triggerEffect, fx);
                        // The heal burst/Aoe prefab carries the main green heal VFX; the
                        // brain strips its Aoe at spawn so it stays purely cosmetic.
                        if (shared.m_attack != null && shared.m_attack.m_attackProjectile != null)
                            fx.Add(shared.m_attack.m_attackProjectile);
                        Phase1D.GreydwarfLordBrain.ConfigureHeal(anim, fx.ToArray());
                        Jotunn.Logger.LogInfo(
                            $"[BiomeLords] Captured shaman heal cast: anim='{anim}', {fx.Count} effect(s).");
                    }
                }

                var filtered = new System.Collections.Generic.List<GameObject>();
                foreach (var item in humanoid.m_defaultItems)
                {
                    if (item == null) continue;
                    if (item.name.IndexOf("heal", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    filtered.Add(item);
                }
                humanoid.m_defaultItems = filtered.ToArray();
            }

            clone.AddComponent<Phase1D.GreydwarfLordBrain>();

            var custom = new CustomCreature(clone, fixReference: true);
            CreatureManager.Instance.AddCreature(custom);

            RegisteredLords.Register(GreydwarfShamanLordPrefab,
                                     Phase1B.EventFactory.GreydwarfLordEvent,
                                     "greydwarf_lord");

            Jotunn.Logger.LogInfo($"[BiomeLords] Registered creature: {GreydwarfShamanLordPrefab}");
        }

        private static void BuildDraugrEliteLord()
        {
            var clone = PrefabManager.Instance.CreateClonedPrefab(DraugrEliteLordPrefab, "Draugr_Elite");
            if (clone == null)
            {
                Jotunn.Logger.LogError("[BiomeLords] Failed to clone Draugr_Elite prefab.");
                return;
            }

            // 1.3x — still distinctly bigger than a vanilla Draugr Elite,
            // but tighter silhouette than Neck/Greydwarf Lords at 1.6–1.7x.
            clone.transform.localScale = Vector3.one * 1.3f;

            // No body material edits — distinct identity via aura.
            AttachLordAura(clone, lightColor: new Color(0.45f, 0.95f, 0.20f)); // sickly green

            var humanoid = clone.GetComponent<Humanoid>();
            if (humanoid != null)
            {
                humanoid.m_health = 5000f;         // matched to Bonemass (Swamp boss)
                humanoid.m_name   = "$enemy_draugrlord";
            }
            var character = clone.GetComponent<Character>();
            if (character != null) character.m_boss = true;

            var drop = clone.GetComponent<CharacterDrop>();
            if (drop != null)
            {
                drop.m_drops.Clear();
                drop.m_drops.Add(new CharacterDrop.Drop
                {
                    m_prefab    = PrefabManager.Instance.GetPrefab(Phase1C.TrophyFactory.DraugrLordTrophy)
                               ?? PrefabManager.Instance.GetPrefab("TrophyDraugrElite"),
                    m_amountMin = 1, m_amountMax = 1, m_chance = 1f, m_onePerPlayer = true,
                });
                drop.m_drops.Add(new CharacterDrop.Drop { m_prefab = PrefabManager.Instance.GetPrefab("Iron"),          m_amountMin = 2, m_amountMax = 4,  m_chance = 1f });
                drop.m_drops.Add(new CharacterDrop.Drop { m_prefab = PrefabManager.Instance.GetPrefab("Coal"),          m_amountMin = 5, m_amountMax = 10, m_chance = 1f });
                drop.m_drops.Add(new CharacterDrop.Drop { m_prefab = PrefabManager.Instance.GetPrefab("BoneFragments"), m_amountMin = 5, m_amountMax = 10, m_chance = 1f });
                drop.m_drops.Add(new CharacterDrop.Drop { m_prefab = PrefabManager.Instance.GetPrefab("Entrails"),      m_amountMin = 3, m_amountMax = 5,  m_chance = 1f });
            }

            clone.AddComponent<Phase1D.DraugrLordBrain>();

            var custom = new CustomCreature(clone, fixReference: true);
            CreatureManager.Instance.AddCreature(custom);

            RegisteredLords.Register(DraugrEliteLordPrefab,
                                     Phase1B.EventFactory.DraugrLordEvent,
                                     "draugr_lord");

            Jotunn.Logger.LogInfo($"[BiomeLords] Registered creature: {DraugrEliteLordPrefab}");
        }

        private static void BuildFenringLord()
        {
            var clone = PrefabManager.Instance.CreateClonedPrefab(FenringLordPrefab, "Fenring");
            if (clone == null)
            {
                Jotunn.Logger.LogError("[BiomeLords] Failed to clone Fenring prefab.");
                return;
            }

            // 1.4x — Fenring's silhouette is already tall; bigger and it
            // clips Mountain cliffs.
            clone.transform.localScale = Vector3.one * 1.4f;

            // Pale-blue Mountain aura. Death Throes will swap to crimson.
            AttachLordAura(clone, lightColor: new Color(0.60f, 0.85f, 1.00f));

            var humanoid = clone.GetComponent<Humanoid>();
            if (humanoid != null)
            {
                humanoid.m_health = 7500f;         // matched to Moder (Mountain boss)
                humanoid.m_name   = "$enemy_fenringlord";
            }
            var character = clone.GetComponent<Character>();
            if (character != null) character.m_boss = true;

            var drop = clone.GetComponent<CharacterDrop>();
            if (drop != null)
            {
                drop.m_drops.Clear();
                drop.m_drops.Add(new CharacterDrop.Drop
                {
                    m_prefab    = PrefabManager.Instance.GetPrefab(Phase1C.TrophyFactory.FenringLordTrophy)
                               ?? PrefabManager.Instance.GetPrefab("TrophyFenring"),
                    m_amountMin = 1, m_amountMax = 1, m_chance = 1f, m_onePerPlayer = true,
                });
                drop.m_drops.Add(new CharacterDrop.Drop { m_prefab = PrefabManager.Instance.GetPrefab("WolfFang"),    m_amountMin = 3, m_amountMax = 5, m_chance = 1f });
                drop.m_drops.Add(new CharacterDrop.Drop { m_prefab = PrefabManager.Instance.GetPrefab("WolfPelt"),    m_amountMin = 2, m_amountMax = 4, m_chance = 1f });
                drop.m_drops.Add(new CharacterDrop.Drop { m_prefab = PrefabManager.Instance.GetPrefab("FreezeGland"), m_amountMin = 1, m_amountMax = 2, m_chance = 1f });
                drop.m_drops.Add(new CharacterDrop.Drop { m_prefab = PrefabManager.Instance.GetPrefab("Silver"),      m_amountMin = 1, m_amountMax = 2, m_chance = 1f });
            }

            clone.AddComponent<Phase1D.FenringLordBrain>();

            var custom = new CustomCreature(clone, fixReference: true);
            CreatureManager.Instance.AddCreature(custom);

            RegisteredLords.Register(FenringLordPrefab,
                                     Phase1B.EventFactory.FenringLordEvent,
                                     "fenring_lord");

            Jotunn.Logger.LogInfo($"[BiomeLords] Registered creature: {FenringLordPrefab}");
        }

        private static void BuildLoxLord()
        {
            var clone = PrefabManager.Instance.CreateClonedPrefab(LoxLordPrefab, "Lox");
            if (clone == null)
            {
                Jotunn.Logger.LogError("[BiomeLords] Failed to clone Lox prefab.");
                return;
            }

            // 1.25x — vanilla Lox is already enormous; bigger and the AI
            // navmesh starts clipping on Plains slopes.
            clone.transform.localScale = Vector3.one * 1.25f;

            // Dusty-yellow Plains aura. Rage swaps to crimson.
            AttachLordAura(clone, lightColor: new Color(1.00f, 0.85f, 0.45f));

            var humanoid = clone.GetComponent<Humanoid>();
            if (humanoid != null)
            {
                humanoid.m_health = 10000f;        // matched to Yagluth (Plains boss)
                humanoid.m_name   = "$enemy_loxlord";
            }
            var character = clone.GetComponent<Character>();
            if (character != null) character.m_boss = true;

            // The Lox Lord is a boss, not livestock — strip the vanilla Lox's
            // Tameable so it can never be fed/tamed. Removed on the prefab clone
            // itself (DestroyImmediate, since the clone isn't instantiated yet).
            // Procreation must go too: it dereferences the (now absent) Tameable
            // every tick and would otherwise NRE in Procreation.Procreate().
            var tameable = clone.GetComponent<Tameable>();
            if (tameable != null) Object.DestroyImmediate(tameable);
            var procreation = clone.GetComponent<Procreation>();
            if (procreation != null) Object.DestroyImmediate(procreation);

            var drop = clone.GetComponent<CharacterDrop>();
            if (drop != null)
            {
                drop.m_drops.Clear();
                drop.m_drops.Add(new CharacterDrop.Drop
                {
                    m_prefab    = PrefabManager.Instance.GetPrefab(Phase1C.TrophyFactory.LoxLordTrophy)
                               ?? PrefabManager.Instance.GetPrefab("TrophyLox"),
                    m_amountMin = 1, m_amountMax = 1, m_chance = 1f, m_onePerPlayer = true,
                });
                drop.m_drops.Add(new CharacterDrop.Drop { m_prefab = PrefabManager.Instance.GetPrefab("LoxMeat"),        m_amountMin = 3, m_amountMax = 5, m_chance = 1f });
                drop.m_drops.Add(new CharacterDrop.Drop { m_prefab = PrefabManager.Instance.GetPrefab("LoxPelt"),        m_amountMin = 2, m_amountMax = 4, m_chance = 1f });
                drop.m_drops.Add(new CharacterDrop.Drop { m_prefab = PrefabManager.Instance.GetPrefab("Barley"),         m_amountMin = 8, m_amountMax = 15, m_chance = 1f });
                drop.m_drops.Add(new CharacterDrop.Drop { m_prefab = PrefabManager.Instance.GetPrefab("BlackMetalScrap"),m_amountMin = 1, m_amountMax = 3, m_chance = 1f });
            }

            clone.AddComponent<Phase1D.LoxLordBrain>();

            var custom = new CustomCreature(clone, fixReference: true);
            CreatureManager.Instance.AddCreature(custom);

            RegisteredLords.Register(LoxLordPrefab,
                                     Phase1B.EventFactory.LoxLordEvent,
                                     "lox_lord");

            Jotunn.Logger.LogInfo($"[BiomeLords] Registered creature: {LoxLordPrefab}");
        }

        private static void BuildSeekerLord()
        {
            var clone = PrefabManager.Instance.CreateClonedPrefab(SeekerLordPrefab, "Seeker");
            if (clone == null)
            {
                Jotunn.Logger.LogError("[BiomeLords] Failed to clone Seeker prefab.");
                return;
            }

            // 1.4x — vanilla Seeker is already large; this reads as "queen-class."
            clone.transform.localScale = Vector3.one * 1.4f;

            // Cyan/violet hive aura. Hive Frenzy swaps to crimson.
            AttachLordAura(clone, lightColor: new Color(0.55f, 0.30f, 1.00f));

            var humanoid = clone.GetComponent<Humanoid>();
            if (humanoid != null)
            {
                humanoid.m_health = 12500f;        // matched to The Queen (Mistlands boss)
                humanoid.m_name   = "$enemy_seekerlord";
            }
            var character = clone.GetComponent<Character>();
            if (character != null) character.m_boss = true;

            var drop = clone.GetComponent<CharacterDrop>();
            if (drop != null)
            {
                drop.m_drops.Clear();
                drop.m_drops.Add(new CharacterDrop.Drop
                {
                    m_prefab    = PrefabManager.Instance.GetPrefab(Phase1C.TrophyFactory.SeekerLordTrophy)
                               ?? PrefabManager.Instance.GetPrefab("TrophySeeker"),
                    m_amountMin = 1, m_amountMax = 1, m_chance = 1f, m_onePerPlayer = true,
                });
                drop.m_drops.Add(new CharacterDrop.Drop { m_prefab = PrefabManager.Instance.GetPrefab("Carapace"),       m_amountMin = 3, m_amountMax = 5, m_chance = 1f });
                drop.m_drops.Add(new CharacterDrop.Drop { m_prefab = PrefabManager.Instance.GetPrefab("Sap"),            m_amountMin = 2, m_amountMax = 4, m_chance = 1f });
                drop.m_drops.Add(new CharacterDrop.Drop { m_prefab = PrefabManager.Instance.GetPrefab("Eitr"),           m_amountMin = 1, m_amountMax = 3, m_chance = 1f });
                drop.m_drops.Add(new CharacterDrop.Drop { m_prefab = PrefabManager.Instance.GetPrefab("Mandible"),       m_amountMin = 1, m_amountMax = 2, m_chance = 1f });
            }

            clone.AddComponent<Phase1D.SeekerLordBrain>();

            var custom = new CustomCreature(clone, fixReference: true);
            CreatureManager.Instance.AddCreature(custom);

            RegisteredLords.Register(SeekerLordPrefab,
                                     Phase1B.EventFactory.SeekerLordEvent,
                                     "seeker_lord");

            Jotunn.Logger.LogInfo($"[BiomeLords] Registered creature: {SeekerLordPrefab}");
        }

        private static void BuildFallerValkyrieLord()
        {
            var clone = PrefabManager.Instance.CreateClonedPrefab(FallerValkyrieLordPrefab, "FallenValkyrie");
            if (clone == null)
            {
                Jotunn.Logger.LogError("[BiomeLords] Failed to clone FallenValkyrie prefab.");
                return;
            }

            // 1.3x — the Fallen Valkyrie is already large and flies; a smaller
            // bump than the ground-bound Lords keeps it readable in the air.
            clone.transform.localScale = Vector3.one * 1.3f;

            // Radiant white-gold aura. Valkyrie's Wrath pumps it brighter.
            AttachLordAura(clone, lightColor: new Color(1.00f, 0.95f, 0.75f));

            var humanoid = clone.GetComponent<Humanoid>();
            if (humanoid != null)
            {
                humanoid.m_health = 25000f;        // matched to the Fallen Valkyrie (Ashlands boss)
                humanoid.m_name   = "$enemy_fallervalkyrielord";
            }
            var character = clone.GetComponent<Character>();
            if (character != null) character.m_boss = true;

            var drop = clone.GetComponent<CharacterDrop>();
            if (drop != null)
            {
                drop.m_drops.Clear();
                drop.m_drops.Add(new CharacterDrop.Drop
                {
                    m_prefab    = PrefabManager.Instance.GetPrefab(Phase1C.TrophyFactory.FallerValkyrieLordTrophy)
                               ?? PrefabManager.Instance.GetPrefab("TrophyFallenValkyrie"),
                    m_amountMin = 1, m_amountMax = 1, m_chance = 1f, m_onePerPlayer = true,
                });
                drop.m_drops.Add(new CharacterDrop.Drop { m_prefab = PrefabManager.Instance.GetPrefab("FlametalNew"),     m_amountMin = 2, m_amountMax = 4, m_chance = 1f });
                drop.m_drops.Add(new CharacterDrop.Drop { m_prefab = PrefabManager.Instance.GetPrefab("Coal"),            m_amountMin = 10,m_amountMax = 20,m_chance = 1f });
                drop.m_drops.Add(new CharacterDrop.Drop { m_prefab = PrefabManager.Instance.GetPrefab("CharredBone"),     m_amountMin = 3, m_amountMax = 6, m_chance = 1f });
                drop.m_drops.Add(new CharacterDrop.Drop { m_prefab = PrefabManager.Instance.GetPrefab("SulfurStone"),     m_amountMin = 4, m_amountMax = 8, m_chance = 1f });
            }

            clone.AddComponent<Phase1D.FallerValkyrieLordBrain>();

            var custom = new CustomCreature(clone, fixReference: true);
            CreatureManager.Instance.AddCreature(custom);

            RegisteredLords.Register(FallerValkyrieLordPrefab,
                                     Phase1B.EventFactory.FallerValkyrieLordEvent,
                                     "faller_valkyrie_lord");

            Jotunn.Logger.LogInfo($"[BiomeLords] Registered creature: {FallerValkyrieLordPrefab}");
        }

        /// <summary>Collect non-null effect prefabs from an EffectList into dst.</summary>
        private static void CollectEffects(EffectList list, System.Collections.Generic.List<GameObject> dst)
        {
            if (list == null || list.m_effectPrefabs == null) return;
            foreach (var e in list.m_effectPrefabs)
                if (e != null && e.m_prefab != null) dst.Add(e.m_prefab);
        }

        /// <summary>Attach a child Light + a vanilla loop-particle (best-effort)
        /// to a Lord prefab. Renders independently of the body shader, so the
        /// creature texture stays clean.</summary>
        private static void AttachLordAura(GameObject lord, Color lightColor)
        {
            var auraGO = new GameObject("BiomeLords_Aura");
            auraGO.transform.SetParent(lord.transform, false);
            auraGO.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            var light = auraGO.AddComponent<Light>();
            light.type      = LightType.Point;
            light.color     = lightColor;
            light.intensity = 2.5f;
            light.range     = 5.0f;

            // Try to parent a vanilla looping particle if any of these exist.
            foreach (var n in new[] { "fx_firepit", "fx_torch_basic", "vfx_firewisp" })
            {
                var src = PrefabManager.Instance.GetPrefab(n);
                if (src == null) continue;
                var inst = Object.Instantiate(src, auraGO.transform);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localScale    = Vector3.one * 0.5f;
                inst.name = "BiomeLords_AuraFx";
                Jotunn.Logger.LogInfo($"[BiomeLords] Lord aura particle: {n}");
                break;
            }
        }
    }
}
