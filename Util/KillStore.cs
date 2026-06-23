using System.Collections.Generic;

namespace BiomeLords.Util
{
    /// <summary>
    /// Per-character persistent kill counter, stored in Player.m_customData.
    /// Key format: "BiomeLords.kills.&lt;prefabName&gt;".
    /// Player.m_customData is serialized with the character profile, so counts
    /// survive logout, world swap, and game restart — but are per-character (intentional).
    /// </summary>
    public static class KillStore
    {
        private const string Prefix = "BiomeLords.kills.";

        public static int Get(Player p, string prefabName)
        {
            if (p == null || string.IsNullOrEmpty(prefabName)) return 0;
            return p.m_customData.TryGetValue(Prefix + prefabName, out var s)
                && int.TryParse(s, out var n) ? n : 0;
        }

        public static void Set(Player p, string prefabName, int value)
        {
            if (p == null || string.IsNullOrEmpty(prefabName)) return;
            p.m_customData[Prefix + prefabName] = value.ToString();
        }

        public static int Increment(Player p, string prefabName)
        {
            if (p == null || string.IsNullOrEmpty(prefabName)) return 0;
            var key = Prefix + prefabName;
            int cur = p.m_customData.TryGetValue(key, out var s) && int.TryParse(s, out var n) ? n : 0;
            cur++;
            p.m_customData[key] = cur.ToString();
            return cur;
        }

        /// <summary>Sum of kills across all prefab names (used to check Lord summon thresholds).</summary>
        public static int SumFor(Player p, IEnumerable<string> prefabNames)
        {
            int total = 0;
            foreach (var n in prefabNames) total += Get(p, n);
            return total;
        }
    }
}
