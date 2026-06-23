using System.Collections.Generic;

namespace BiomeLords.Data
{
    /// <summary>
    /// Static design data for a single Biome Lord.
    /// Phase 1A: only the fields needed for kill tracking + future summon gating.
    /// Later phases will add prefab tweak data, drop tables, status effects, etc.
    /// </summary>
    public class BiomeLordDef
    {
        /// <summary>Internal id used for config keys, kill-counter keys, prefab names.</summary>
        public string Id;

        /// <summary>Display name shown in UI / messages.</summary>
        public string DisplayName;

        /// <summary>Vanilla prefab name(s) whose kills count toward summoning this Lord.</summary>
        public string[] KillTargets;

        /// <summary>Default number of kills required before the Horn can summon this Lord.</summary>
        public int DefaultKillRequirement;

        /// <summary>Vanilla biome name (matches Heightmap.Biome enum names) where the Horn must be used.</summary>
        public string Biome;

        /// <summary>Vanilla prefab name this Lord is cloned from.</summary>
        public string BasePrefab;

        /// <summary>Biome tier (1=Meadows … 7=Ashlands). Used by tier-based scaling.</summary>
        public int Tier;

        public BiomeLordDef(string id, string display, string basePrefab, string biome,
                            int tier, int killReq, params string[] killTargets)
        {
            Id = id;
            DisplayName = display;
            BasePrefab = basePrefab;
            Biome = biome;
            Tier = tier;
            DefaultKillRequirement = killReq;
            KillTargets = killTargets;
        }
    }

    /// <summary>
    /// The 7 primary Biome Lords (Phase 1).
    /// KillTargets use vanilla prefab names — Character.m_name is a localization token,
    /// so we match on prefab name via gameObject.name (stripped of "(Clone)").
    /// </summary>
    public static class LordRegistry
    {
        public static readonly List<BiomeLordDef> All = new List<BiomeLordDef>
        {
            // (id, display, basePrefab, biome, tier, killReq, killTargets...)
            new BiomeLordDef("neck_lord",       "Neck Lord",            "Neck",            "Meadows",     1, 20, "Neck"),
            new BiomeLordDef("greydwarf_lord",  "Greydwarf Shaman Lord","Greydwarf_Shaman","BlackForest", 2, 30, "Greydwarf", "Greydwarf_Elite", "Greydwarf_Shaman"),
            new BiomeLordDef("draugr_lord",     "Draugr Elite Lord",    "Draugr_Elite",    "Swamp",       3, 25, "Draugr", "Draugr_Elite", "Draugr_Ranged"),
            new BiomeLordDef("fenring_lord",    "Fenring Lord",         "Fenring",         "Mountain",    4, 20, "Wolf", "Fenring", "Fenring_Cultist"),
            new BiomeLordDef("lox_lord",        "Lox Lord",             "Lox",             "Plains",      5, 10, "Lox"),
            new BiomeLordDef("seeker_lord",     "Seeker Lord",          "Seeker",          "Mistlands",   6, 20, "Seeker", "SeekerBrute", "SeekerBrood"),
            new BiomeLordDef("faller_valkyrie_lord", "Fallen Valkyrie Lord", "FallenValkyrie", "AshLands", 7, 25, "Charred_Melee", "Charred_Archer", "Charred_Mage"),
        };

        public static BiomeLordDef ById(string id) => All.Find(l => l.Id == id);

        /// <summary>Returns the Lord whose KillTargets include the given prefab name, or null.</summary>
        public static BiomeLordDef FindByKillTarget(string prefabName)
        {
            foreach (var lord in All)
                foreach (var t in lord.KillTargets)
                    if (t == prefabName) return lord;
            return null;
        }
    }
}
