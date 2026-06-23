using System.Collections.Generic;
using UnityEngine;

namespace BiomeLords.Util
{
    /// <summary>
    /// Per-Lord-instance resolved attack profile — the convergence target for the
    /// current progression, baked at spawn (SummonService) and on Awake
    /// (LordAutoRegisterPatch) and read by the damage patch.
    ///
    /// Keyed by Character.GetInstanceID(), parallel to LordDamageRegistry.
    /// </summary>
    public static class LordProfileRegistry
    {
        private static readonly Dictionary<int, DamageProfile> _profiles = new Dictionary<int, DamageProfile>();

        public static void Set(Character c, DamageProfile profile)
        {
            if (c == null) return;
            _profiles[c.GetInstanceID()] = profile;
        }

        public static bool TryGet(Character c, out DamageProfile profile)
        {
            if (c != null && _profiles.TryGetValue(c.GetInstanceID(), out profile)) return true;
            profile = default;
            return false;
        }

        public static void Clear(Character c)
        {
            if (c == null) return;
            _profiles.Remove(c.GetInstanceID());
        }
    }
}
