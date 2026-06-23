using System.Collections.Generic;
using UnityEngine;
using Jotunn.Managers;

namespace BiomeLords.Util
{
    /// <summary>
    /// Helper around vanilla VFX lookups.
    ///   • TrySpawn() — spawn first prefab whose name exists; silently skip if none.
    ///   • DumpFxNames() — one-shot debug dump of every "fx_*" / "vfx_*" prefab
    ///     in the live ZNetScene, so we can pick names that actually exist
    ///     in the player's Valheim version instead of guessing.
    /// </summary>
    public static class FxLibrary
    {
        private static bool _dumped;

        public static GameObject TrySpawn(string name, Vector3 pos, Quaternion? rot = null, Transform parent = null)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var prefab = PrefabManager.Instance.GetPrefab(name);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, pos, rot ?? Quaternion.identity);
            if (parent != null) go.transform.SetParent(parent, worldPositionStays: true);
            return go;
        }

        /// <summary>Spawn and auto-destroy after `lifetime` seconds — for FX that
        /// don't clean up after themselves (e.g. vfx_water_surface is a persistent
        /// asset with no built-in lifetime).</summary>
        public static GameObject TrySpawnTimed(string name, Vector3 pos, float lifetime)
        {
            var go = TrySpawn(name, pos);
            if (go != null && lifetime > 0f) Object.Destroy(go, lifetime);
            return go;
        }

        /// <summary>Try a list of names in order, spawn the first one that exists.</summary>
        public static GameObject TrySpawnFirst(IEnumerable<string> names, Vector3 pos)
        {
            foreach (var n in names)
            {
                var go = TrySpawn(n, pos);
                if (go != null) return go;
            }
            return null;
        }

        /// <summary>
        /// Logs every fx_*/vfx_* prefab in the ZNetScene exactly once per game session.
        /// Use the log output to pick prefab names that definitely exist.
        /// </summary>
        public static void DumpFxNamesOnce()
        {
            if (_dumped) return;
            var scene = ZNetScene.instance;
            if (scene == null || scene.m_prefabs == null) return;

            _dumped = true;
            int count = 0;
            foreach (var p in scene.m_prefabs)
            {
                if (p == null) continue;
                var n = p.name;
                if (n.StartsWith("fx_") || n.StartsWith("vfx_") || n.Contains("ummon"))
                {
                    Jotunn.Logger.LogInfo($"[BiomeLords.FxDump] {n}");
                    count++;
                }
            }
            Jotunn.Logger.LogInfo($"[BiomeLords.FxDump] {count} candidate FX prefabs dumped.");
        }
    }
}
