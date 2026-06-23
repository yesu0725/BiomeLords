using System.Collections.Generic;

namespace BiomeLords.Util
{
    /// <summary>
    /// Runtime registry of Lord creature prefab names. Populated when each Lord
    /// is built in CreatureFactory; queried by gameplay patches (stagger immunity,
    /// event end-on-death, future hooks).
    /// </summary>
    public static class RegisteredLords
    {
        public static readonly HashSet<string> PrefabNames = new HashSet<string>();

        /// <summary>Maps Lord prefab name → world-event id, used to end the event on Lord death.</summary>
        public static readonly Dictionary<string, string> EventByPrefab = new Dictionary<string, string>();

        /// <summary>Maps Lord prefab name → BiomeLordDef.Id, used to reset kill counters on Lord death.</summary>
        public static readonly Dictionary<string, string> LordIdByPrefab = new Dictionary<string, string>();

        public static void Register(string prefabName, string eventName = null, string lordId = null)
        {
            if (string.IsNullOrEmpty(prefabName)) return;
            PrefabNames.Add(prefabName);
            if (!string.IsNullOrEmpty(eventName)) EventByPrefab[prefabName] = eventName;
            if (!string.IsNullOrEmpty(lordId))    LordIdByPrefab[prefabName] = lordId;
        }

        public static string LordIdFor(Character c)
        {
            if (c == null) return null;
            return LordIdByPrefab.TryGetValue(StripClone(c.gameObject.name), out var id) ? id : null;
        }

        public static bool IsLord(Character c)
        {
            if (c == null) return false;
            return PrefabNames.Contains(StripClone(c.gameObject.name));
        }

        public static string EventFor(Character c)
        {
            if (c == null) return null;
            return EventByPrefab.TryGetValue(StripClone(c.gameObject.name), out var e) ? e : null;
        }

        private static string StripClone(string n)
        {
            if (string.IsNullOrEmpty(n)) return n;
            const string suffix = "(Clone)";
            return n.EndsWith(suffix) ? n.Substring(0, n.Length - suffix.Length) : n;
        }
    }
}
