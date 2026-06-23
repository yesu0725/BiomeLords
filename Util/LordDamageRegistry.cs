using System.Collections.Generic;
using UnityEngine;

namespace BiomeLords.Util
{
    /// <summary>
    /// Per-Lord-instance damage multiplier. Set when the Lord is spawned
    /// (so different Lords can scale by different amounts based on tier),
    /// read by the damage-boost Harmony patch.
    ///
    /// Keyed by Character.GetInstanceID() — GameObject identity is stable
    /// for the lifetime of the instance.
    /// </summary>
    public static class LordDamageRegistry
    {
        private static readonly Dictionary<int, float> _mults = new Dictionary<int, float>();

        public static void Set(Character c, float multiplier)
        {
            if (c == null) return;
            _mults[c.GetInstanceID()] = multiplier;
        }

        public static float Get(Character c, float fallback = 1f)
        {
            if (c == null) return fallback;
            return _mults.TryGetValue(c.GetInstanceID(), out var m) ? m : fallback;
        }

        public static bool Has(Character c)
        {
            if (c == null) return false;
            return _mults.ContainsKey(c.GetInstanceID());
        }

        public static void Clear(Character c)
        {
            if (c == null) return;
            _mults.Remove(c.GetInstanceID());
        }
    }
}
