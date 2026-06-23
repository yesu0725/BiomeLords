using UnityEngine;
using BiomeLords.Util;

namespace BiomeLords.Phase1D
{
    /// <summary>
    /// Despawns its host GameObject after Lifetime seconds (or immediately if the
    /// owning local player is gone — e.g. logout), with a small poof FX. Attached
    /// to the cloned Wolf spawned alongside the Howl of the Pack Forsaken Power.
    /// </summary>
    public class PhantomWolf : MonoBehaviour
    {
        public float Lifetime = 60f;

        /// <summary>When true the wolf ignores all incoming damage — enforced by
        /// PhantomWolfInvulnPatch. Set on the synergy (Pack Whisperer) variant.</summary>
        public bool Invulnerable;

        private float    _spawnTime;
        private ZNetView _nview;

        private void Awake()
        {
            _spawnTime = Time.time;
            _nview     = GetComponent<ZNetView>();
        }

        private void Update()
        {
            // Despawn when the lifetime elapses, or as soon as the local player
            // is gone (logout / death-to-menu) so phantoms never linger.
            if (Player.m_localPlayer != null && Time.time - _spawnTime < Lifetime) return;

            var pos = transform.position + Vector3.up * 0.5f;
            FxLibrary.TrySpawn("vfx_corpse_destruction_small", pos);
            FxLibrary.TrySpawn("vfx_HitSparks", pos);
            Despawn();
        }

        private void Despawn()
        {
            // Networked objects must be torn down through ZNetScene so the ZDO is
            // removed too; fall back to a plain Destroy if there's no live nview.
            if (_nview != null && _nview.IsValid() && ZNetScene.instance != null)
                ZNetScene.instance.Destroy(gameObject);
            else
                Object.Destroy(gameObject);
        }
    }
}
