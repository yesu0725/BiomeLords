using System.Collections.Generic;
using UnityEngine;
using Jotunn.Managers;
using BiomeLords.Phase1C;

namespace BiomeLords.Util
{
    /// <summary>
    /// Assigns relevant vanilla item icons to every BiomeLords-registered
    /// status effect (Forsaken Powers, blessings, sub-effects). This gives
    /// each SE a unique HUD icon — and because each SE has a non-zero
    /// m_ttl OR is permanent, vanilla draws either a countdown timer or a
    /// persistent icon. No more trophy-icon reuse.
    /// </summary>
    public static class IconAssignment
    {
        /// <summary>SE name → ordered list of candidates. Each candidate is
        /// tried first as an ItemDrop prefab name, then as a vanilla
        /// StatusEffect name (e.g. "Sheltered", "Poison"). First non-null
        /// icon wins.</summary>
        private static readonly Dictionary<string, string[]> Map = new Dictionary<string, string[]>
        {
            // ---- Forsaken Powers (combat / adventure) -----
            { GuardianPowerFactory.NeckLordGP,      new[] { "Harpoon", "FishingRod", "Wet" } },                  // Tide's Grace → water/harpoon
            { GuardianPowerFactory.GreydwarfLordGP, new[] { "Sapling_Birch", "Sapling_Beech", "BeechSeeds", "Wood" } }, // Forest's Embrace → sapling/tree
            { GuardianPowerFactory.DraugrLordGP,    new[] { "Poison", "Ooze", "BloodPudding", "Entrails" } },   // Plague Bearer → poison
            { GuardianPowerFactory.FenringLordGP,   new[] { "WolfFang", "FreezeGland" } },                       // Howl of the Pack
            { GuardianPowerFactory.LoxLordGP,       new[] { "ShieldWood", "ShieldBronzeBuckler", "ShieldIronTower" } }, // Bull Rush → shield
            { GuardianPowerFactory.SeekerLordGP,    new[] { "Wisplight", "Wisp", "Eitr" } },                    // Hive Sight → Wisplight
            { GuardianPowerFactory.FallerValkyrieLordGP, new[] { "Feathers" } },     // Valkyrie's Rally (overridden to Feather fall icon below)

            // ---- Blessings (chores) -----
            { StatusEffectFactory.NeckLordSpiritSE,      new[] { "FishingBait", "Fish1" } },                   // Fisher's Boon
            { StatusEffectFactory.GreydwarfLordSpiritSE, new[] { "Carrot", "CarrotSeeds" } },                  // Quick Sprout
            { StatusEffectFactory.DraugrLordSpiritSE,    new[] { "IronScrap", "Iron" } },                      // Iron Vein
            { StatusEffectFactory.FenringLordSpiritSE,   new[] { "WolfPelt", "WolfFang", "TrophyWolf" } },     // Pack Whisperer
            { StatusEffectFactory.LoxLordSpiritSE,       new[] { "CookedLoxMeat", "LoxMeat", "Barley" } },     // Hearth Master
            { StatusEffectFactory.SeekerLordSpiritSE,    new[] { "Coal", "Bronze", "Iron" } },                 // Refiner's Touch
            { StatusEffectFactory.FallerValkyrieLordSpiritSE, new[] { "Feathers" } }, // Featherweight (overridden to Feather fall icon below)

            // ---- Sub-effects (state indicators) -----
            { SubEffectFactory.ForestSitSE, new[] { "Sheltered", "Shelter", "Rested" } },                       // Forest Sit → Sheltered SE
        };

        /// <summary>Run after ObjectDB is populated so item prefabs resolve.</summary>
        public static void AssignAll()
        {
            int hits = 0, misses = 0;
            foreach (var kv in Map)
            {
                var se = ResolveSE(kv.Key);
                if (se == null) { misses++; continue; }

                var icon = FindFirstItemIcon(kv.Value);
                if (icon != null)
                {
                    se.m_icon = icon;
                    hits++;
                }
                else
                {
                    misses++;
                }
            }
            // Fallen Valkyrie SEs share the vanilla "Feather fall" buff icon
            // (from the Feather Cape) rather than a generic item icon — the
            // weightless feather reads for both Featherweight (blessing) and
            // Valkyrie's Rally (power). Overrides whatever the Map assigned.
            var feather = GetFeatherFallIcon();
            if (feather != null)
            {
                var bless = ResolveSE(StatusEffectFactory.FallerValkyrieLordSpiritSE);
                if (bless != null) bless.m_icon = feather;
                var power = ResolveSE(GuardianPowerFactory.FallerValkyrieLordGP);
                if (power != null) power.m_icon = feather;
            }
            else
            {
                Jotunn.Logger.LogWarning("[BiomeLords] IconAssignment: Feather fall icon " +
                                         "not found; Valkyrie SEs kept their fallback icons.");
            }

            Jotunn.Logger.LogInfo($"[BiomeLords] IconAssignment: {hits} SEs got icons, {misses} missed.");
        }

        /// <summary>The Feather Cape's equip status effect ("Feather fall") icon,
        /// with a fall-back to the Feathers item icon if the cape can't resolve.</summary>
        private static Sprite GetFeatherFallIcon()
        {
            var cape = PrefabManager.Instance.GetPrefab("CapeFeather")?.GetComponent<ItemDrop>();
            var equipSe = cape?.m_itemData?.m_shared?.m_equipStatusEffect;
            if (equipSe != null && equipSe.m_icon != null) return equipSe.m_icon;

            return FindFirstItemIcon(new[] { "Feathers" });
        }

        private static StatusEffect ResolveSE(string name)
        {
            if (GuardianPowerFactory.ByName.TryGetValue(name, out var s1)) return s1;
            if (StatusEffectFactory.ByName.TryGetValue(name, out var s2)) return s2;
            if (SubEffectFactory.ByName.TryGetValue(name, out var s3)) return s3;
            return null;
        }

        private static Sprite FindFirstItemIcon(string[] candidates)
        {
            var db = ObjectDB.instance;
            foreach (var name in candidates)
            {
                // 1) Try ItemDrop prefab (vanilla item icons).
                var item = PrefabManager.Instance.GetPrefab(name)?.GetComponent<ItemDrop>();
                var icons = item?.m_itemData?.m_shared?.m_icons;
                if (icons != null && icons.Length > 0 && icons[0] != null) return icons[0];

                // 2) Try vanilla StatusEffect by name (e.g. "Sheltered", "Poison",
                //    "Stealth") — these carry their own icon textures.
                if (db != null && db.m_StatusEffects != null)
                {
                    foreach (var se in db.m_StatusEffects)
                    {
                        if (se == null || se.m_icon == null) continue;
                        if (se.name == name) return se.m_icon;
                    }
                }
            }
            return null;
        }
    }
}
