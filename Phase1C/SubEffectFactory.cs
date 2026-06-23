using System.Collections.Generic;
using UnityEngine;
using Jotunn.Managers;
using Jotunn.Entities;

namespace BiomeLords.Phase1C
{
    /// <summary>
    /// Sub-effects applied conditionally by PowerEffectsService while a marker
    /// Forsaken Power (e.g. Tide's Grace / Predator's Sense) is active.
    ///
    /// These SEs hold their own icons + HUD names so the player can see which
    /// situational effect is currently in play.
    /// </summary>
    public static class SubEffectFactory
    {
        public const string ForestSitSE  = "SE_ForestSitting";

        public static readonly Dictionary<string, StatusEffect> ByName =
            new Dictionary<string, StatusEffect>();

        public static void RegisterAll()
        {
            // ForestSit: infinite TTL → no countdown shown in HUD. Service
            // manually removes it when conditions break (sitting/safe/tree).
            RegisterMarker(ForestSitSE, "se_forestsit",  iconHint: "Rested",   ttl: 0f);
        }

        private static void RegisterMarker(string seName, string locKey, string iconHint, float ttl = 5f)
        {
            var se = ScriptableObject.CreateInstance<SE_Stats>();
            se.name      = seName;
            se.m_name    = "$" + locKey;
            se.m_tooltip = "$" + locKey + "_tooltip";
            se.m_ttl     = ttl;

            var custom = new CustomStatusEffect(se, fixReference: false);
            ItemManager.Instance.AddStatusEffect(custom);
            ByName[seName] = se;

            // Lazy icon — try a vanilla SE by name, then any non-null fallback.
            // ObjectDB isn't ready yet at registration time; PowerEffectsService
            // calls EnsureIcon on first apply.
        }

        /// <summary>Borrow an icon. `hint` is tried first as an ItemDrop prefab
        /// name (taking the item's first icon — gives us thematic, distinctive
        /// art like a Lord trophy). If that misses it's tried as a vanilla SE
        /// name. Falls back to the first non-null vanilla SE icon as a last resort.</summary>
        public static void EnsureIcon(StatusEffect se, string hint)
        {
            if (se == null || se.m_icon != null) return;

            // 1) Item icon (trophies, horns, etc. — distinctive thematic art).
            if (!string.IsNullOrEmpty(hint))
            {
                var item = Jotunn.Managers.PrefabManager.Instance.GetPrefab(hint)?.GetComponent<ItemDrop>();
                var icons = item?.m_itemData?.m_shared?.m_icons;
                if (icons != null && icons.Length > 0 && icons[0] != null)
                {
                    se.m_icon = icons[0];
                    return;
                }
            }

            var db = ObjectDB.instance;
            if (db == null || db.m_StatusEffects == null) return;

            // 2) Vanilla SE with matching name (e.g. "Wet", "Burning").
            if (!string.IsNullOrEmpty(hint))
            {
                foreach (var s in db.m_StatusEffects)
                {
                    if (s == null || s.m_icon == null) continue;
                    if (s.name == hint) { se.m_icon = s.m_icon; return; }
                }
            }

            // 3) Any non-null vanilla SE icon.
            foreach (var s in db.m_StatusEffects)
            {
                if (s != null && s.m_icon != null) { se.m_icon = s.m_icon; return; }
            }
        }
    }
}
