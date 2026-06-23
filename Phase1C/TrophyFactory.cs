using UnityEngine;
using Jotunn.Managers;
using Jotunn.Entities;
using Jotunn.Configs;
using BiomeLords.Util;

namespace BiomeLords.Phase1C
{
    /// <summary>
    /// Per-Lord trophy items. Each is a retinted clone of the base creature's
    /// trophy so we don't need any new art. Mounted on a Lord's Pedestal to
    /// unlock the Lord's passive aura.
    /// </summary>
    public static class TrophyFactory
    {
        public const string NeckLordTrophy        = "TrophyNeckLord";
        public const string GreydwarfLordTrophy   = "TrophyGreydwarfShamanLord";
        public const string DraugrLordTrophy      = "TrophyDraugrEliteLord";
        public const string FenringLordTrophy     = "TrophyFenringLord";
        public const string LoxLordTrophy         = "TrophyLoxLord";
        public const string SeekerLordTrophy      = "TrophySeekerLord";
        public const string FallerValkyrieLordTrophy = "TrophyFallerValkyrieLord";

        public static void RegisterAll()
        {
            BuildNeckLordTrophy();
            BuildGreydwarfShamanLordTrophy();
            BuildDraugrEliteLordTrophy();
            BuildFenringLordTrophy();
            BuildLoxLordTrophy();
            BuildSeekerLordTrophy();
            BuildFallerValkyrieLordTrophy();
        }

        private static void BuildFallerValkyrieLordTrophy()
        {
            var trophy = new CustomItem(FallerValkyrieLordTrophy, "TrophyFallenValkyrie", new ItemConfig
            {
                Name        = "$item_trophyfallervalkyrielord",
                Description = "$item_trophyfallervalkyrielord_desc",
            });

            var drop = trophy.ItemPrefab.GetComponent<ItemDrop>();
            if (drop != null && drop.m_itemData?.m_shared != null)
            {
                var s = drop.m_itemData.m_shared;
                s.m_itemType     = ItemDrop.ItemData.ItemType.Trophy;
                s.m_maxStackSize = 1;
                s.m_questItem    = false;
            }

            // Radiant white-gold tint.
            TintAndRenderIcon(trophy.ItemPrefab, drop, new Color(1.00f, 0.95f, 0.75f));
            ApplyTrophyAura(trophy.ItemPrefab,
                            lightColor: new Color(1.00f, 0.95f, 0.75f),
                            scale: 1.0f);

            ItemManager.Instance.AddItem(trophy);
            Jotunn.Logger.LogInfo($"[BiomeLords] Registered trophy: {FallerValkyrieLordTrophy}");
        }

        private static void BuildSeekerLordTrophy()
        {
            var trophy = new CustomItem(SeekerLordTrophy, "TrophySeeker", new ItemConfig
            {
                Name        = "$item_trophyseekerlord",
                Description = "$item_trophyseekerlord_desc",
            });

            var drop = trophy.ItemPrefab.GetComponent<ItemDrop>();
            if (drop != null && drop.m_itemData?.m_shared != null)
            {
                var s = drop.m_itemData.m_shared;
                s.m_itemType     = ItemDrop.ItemData.ItemType.Trophy;
                s.m_maxStackSize = 1;
                s.m_questItem    = false;
            }

            // Violet hive tint.
            TintAndRenderIcon(trophy.ItemPrefab, drop, new Color(0.55f, 0.30f, 1.00f));
            ApplyTrophyAura(trophy.ItemPrefab,
                            lightColor: new Color(0.55f, 0.30f, 1.00f),
                            scale: 1.0f);

            ItemManager.Instance.AddItem(trophy);
            Jotunn.Logger.LogInfo($"[BiomeLords] Registered trophy: {SeekerLordTrophy}");
        }

        private static void BuildLoxLordTrophy()
        {
            var trophy = new CustomItem(LoxLordTrophy, "TrophyLox", new ItemConfig
            {
                Name        = "$item_trophyloxlord",
                Description = "$item_trophyloxlord_desc",
            });

            var drop = trophy.ItemPrefab.GetComponent<ItemDrop>();
            if (drop != null && drop.m_itemData?.m_shared != null)
            {
                var s = drop.m_itemData.m_shared;
                s.m_itemType     = ItemDrop.ItemData.ItemType.Trophy;
                s.m_maxStackSize = 1;
                s.m_questItem    = false;
            }

            TintAndRenderIcon(trophy.ItemPrefab, drop, new Color(1.00f, 0.85f, 0.45f));
            ApplyTrophyAura(trophy.ItemPrefab,
                            lightColor: new Color(1.00f, 0.85f, 0.45f),
                            scale: 1.0f);

            ItemManager.Instance.AddItem(trophy);
            Jotunn.Logger.LogInfo($"[BiomeLords] Registered trophy: {LoxLordTrophy}");
        }

        private static void BuildFenringLordTrophy()
        {
            var trophy = new CustomItem(FenringLordTrophy, "TrophyFenring", new ItemConfig
            {
                Name        = "$item_trophyfenringlord",
                Description = "$item_trophyfenringlord_desc",
            });

            var drop = trophy.ItemPrefab.GetComponent<ItemDrop>();
            if (drop != null && drop.m_itemData?.m_shared != null)
            {
                var s = drop.m_itemData.m_shared;
                s.m_itemType     = ItemDrop.ItemData.ItemType.Trophy;
                s.m_maxStackSize = 1;
                s.m_questItem    = false;
            }

            // Pale-blue Mountain tint + standard 3/4 render.
            TintAndRenderIcon(trophy.ItemPrefab, drop, new Color(0.65f, 0.85f, 1.00f));

            ApplyTrophyAura(trophy.ItemPrefab,
                            lightColor: new Color(0.65f, 0.85f, 1.00f),
                            scale: 1.0f);

            ItemManager.Instance.AddItem(trophy);
            Jotunn.Logger.LogInfo($"[BiomeLords] Registered trophy: {FenringLordTrophy}");
        }

        private static void BuildNeckLordTrophy()
        {
            var trophy = new CustomItem(NeckLordTrophy, "TrophyNeck", new ItemConfig
            {
                Name        = "$item_trophynecklord",
                Description = "$item_trophynecklord_desc",
                // Trophies have no recipe — they're drops only.
            });

            // Mark as a Trophy item type so it sits in the trophy row.
            var drop = trophy.ItemPrefab.GetComponent<ItemDrop>();
            if (drop != null && drop.m_itemData?.m_shared != null)
            {
                var s = drop.m_itemData.m_shared;
                s.m_itemType     = ItemDrop.ItemData.ItemType.Trophy;
                s.m_maxStackSize = 1;
                // m_questItem = true would lock the trophy onto the pedestal — you
                // wouldn't be able to take it back. Keep it false.
                s.m_questItem    = false;
            }

            // Retint deep crimson to match the Lord's body color — distinct from the
            // plain Neck trophy. Re-render the icon from the tinted mesh.
            var body     = new Color(0.85f, 0.20f, 0.20f, 1f);
            var emission = new Color(1.0f, 0.30f, 0.10f, 1f);
            RetintPrefab(trophy.ItemPrefab, body, emission);

            // Tint the prefab's materials (crimson) and snapshot it via
            // Jotunn's RenderManager — produces a vanilla-style icon in the
            // recognisable trophy shape, just recoloured.
            TintAndRenderIcon(trophy.ItemPrefab, drop, new Color(0.90f, 0.20f, 0.20f));

            ItemManager.Instance.AddItem(trophy);
            Jotunn.Logger.LogInfo($"[BiomeLords] Registered trophy: {NeckLordTrophy}");
        }

        private static void BuildGreydwarfShamanLordTrophy()
        {
            var trophy = new CustomItem(GreydwarfLordTrophy, "TrophyGreydwarfShaman", new ItemConfig
            {
                Name        = "$item_trophygreydwarflord",
                Description = "$item_trophygreydwarflord_desc",
            });

            var drop = trophy.ItemPrefab.GetComponent<ItemDrop>();
            if (drop != null && drop.m_itemData?.m_shared != null)
            {
                var s = drop.m_itemData.m_shared;
                s.m_itemType     = ItemDrop.ItemData.ItemType.Trophy;
                s.m_maxStackSize = 1;
                s.m_questItem    = false;
            }

            // In-world: small green Light parented to the prefab so the
            // trophy reads as "the Lord's" when mounted on a pedestal.
            // No scale change (kept 1.0x so the inventory icon — copied
            // from the vanilla shaman trophy by CustomItem — stays exactly
            // like the original vanilla shaman trophy icon).
            ApplyTrophyAura(trophy.ItemPrefab,
                            lightColor: new Color(0.30f, 1.00f, 0.20f),
                            scale: 1.0f);

            // Tint the prefab's materials (bright green) and snapshot it via
            // Jotunn's RenderManager.
            TintAndRenderIcon(trophy.ItemPrefab, drop, new Color(0.30f, 0.90f, 0.25f));

            ItemManager.Instance.AddItem(trophy);
            Jotunn.Logger.LogInfo($"[BiomeLords] Registered trophy: {GreydwarfLordTrophy}");
        }

        /// <summary>
        /// Tint every renderer's _Color on the prefab (cloning materials so we
        /// don't mutate vanilla assets), then snapshot the prefab via Jotunn's
        /// RenderManager at its default 3/4-view angle. The resulting Sprite
        /// becomes the inventory icon — vanilla trophy shape, just recoloured.
        /// </summary>
        private static void BuildDraugrEliteLordTrophy()
        {
            var trophy = new CustomItem(DraugrLordTrophy, "TrophyDraugrElite", new ItemConfig
            {
                Name        = "$item_trophydraugrlord",
                Description = "$item_trophydraugrlord_desc",
            });

            var drop = trophy.ItemPrefab.GetComponent<ItemDrop>();
            if (drop != null && drop.m_itemData?.m_shared != null)
            {
                var s = drop.m_itemData.m_shared;
                s.m_itemType     = ItemDrop.ItemData.ItemType.Trophy;
                s.m_maxStackSize = 1;
                s.m_questItem    = false;
            }

            // Sickly green tint + standard 3/4 render.
            TintAndRenderIcon(trophy.ItemPrefab, drop, new Color(0.45f, 0.95f, 0.20f));

            // In-world aura (when mounted on pedestal).
            ApplyTrophyAura(trophy.ItemPrefab,
                            lightColor: new Color(0.45f, 0.95f, 0.20f),
                            scale: 1.0f);

            ItemManager.Instance.AddItem(trophy);
            Jotunn.Logger.LogInfo($"[BiomeLords] Registered trophy: {DraugrLordTrophy}");
        }

        private static void TintAndRenderIcon(GameObject prefab, ItemDrop drop, Color tint)
        {
            if (prefab == null || drop?.m_itemData?.m_shared == null) return;

            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                var srcs = r.sharedMaterials;
                if (srcs == null || srcs.Length == 0) continue;
                var copies = new Material[srcs.Length];
                for (int i = 0; i < srcs.Length; i++)
                {
                    if (srcs[i] == null) continue;
                    var m = new Material(srcs[i]);
                    if (m.HasProperty("_Color")) m.color = tint;
                    copies[i] = m;
                }
                r.sharedMaterials = copies;
            }

            try
            {
                var req = new RenderManager.RenderRequest(prefab)
                {
                    Width = 64, Height = 64, UseCache = true,
                    // No Rotation override — let Jotunn pick its default item angle.
                };
                var sprite = RenderManager.Instance.Render(req);
                if (sprite != null)
                    drop.m_itemData.m_shared.m_icons = new[] { sprite };
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogWarning($"[BiomeLords] Trophy icon render failed: {ex.Message}");
            }
        }

        /// <summary>Scale up + attach a child Light (and optional loop particle).
        /// Works visually in inventory icon (renders the new mesh) and when the
        /// trophy is mounted on a pedestal (the light radiates).</summary>
        private static void ApplyTrophyAura(GameObject prefab, Color lightColor, float scale)
        {
            if (prefab == null) return;
            prefab.transform.localScale = Vector3.one * scale;

            var auraGO = new GameObject("BiomeLords_TrophyAura");
            auraGO.transform.SetParent(prefab.transform, false);
            auraGO.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            var light = auraGO.AddComponent<Light>();
            light.type      = LightType.Point;
            light.color     = lightColor;
            light.intensity = 1.5f;
            light.range     = 2.5f;
        }

        /// <summary>Same renderer/particle/light retint approach as ItemFactory.</summary>
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
        }
    }
}
