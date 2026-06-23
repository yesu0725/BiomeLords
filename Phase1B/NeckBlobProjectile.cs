using UnityEngine;
using Jotunn.Managers;
using BiomeLords.Util;

namespace BiomeLords.Phase1B
{
    /// <summary>
    /// Self-propelled water blob projectile for the Neck Lord's Tidal Shot ability.
    /// Visual built from a sphere primitive skinned with the Neck's own Valheim material
    /// (tinted blue) so no asset bundle is needed and the shader is always valid.
    /// Spawns water-ripple trail VFX while in flight, then detonates on arrival.
    /// </summary>
    internal sealed class NeckBlobProjectile : MonoBehaviour
    {
        private const float Speed        = 10f;
        private const float ArrivalDist  =  0.6f;
        private const float SplashRadius =  1.5f;
        private const float MaxLifetime  =  6f;

        private Vector3   _target;
        private Character _attacker;
        private bool      _detonated;
        private float     _nextTrailTime;

        // Built once from the Neck prefab's own shader — guaranteed valid in Valheim.
        private static Material _blueMat;

        internal static void Fire(Vector3 origin, Vector3 target, Character attacker)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(go.GetComponent<SphereCollider>());
            go.transform.position   = origin + Vector3.up * 1.2f;
            go.transform.localScale = Vector3.one * 0.4f;

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
                rend.material = GetBlueMaterial() ?? rend.material;

            var proj       = go.AddComponent<NeckBlobProjectile>();
            proj._target   = target + Vector3.up * 0.8f;
            proj._attacker = attacker;

            Object.Destroy(go, MaxLifetime);
        }

        // Borrows the Neck prefab's shader so the material is always valid in Valheim's pipeline.
        private static Material GetBlueMaterial()
        {
            if (_blueMat != null) return _blueMat;
            var prefab = PrefabManager.Instance.GetPrefab("Neck");
            if (prefab == null) return null;
            var r = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (r == null || r.sharedMaterial == null) return null;
            _blueMat = new Material(r.sharedMaterial);
            _blueMat.color = new Color(0.2f, 0.6f, 1.0f, 1f);
            if (_blueMat.HasProperty("_EmissionColor"))
                _blueMat.SetColor("_EmissionColor", new Color(0.0f, 0.3f, 0.8f) * 1.2f);
            return _blueMat;
        }

        private void Update()
        {
            if (_detonated) return;

            Vector3 dir  = _target - transform.position;
            float   dist = dir.magnitude;

            if (dist <= ArrivalDist)
            {
                Detonate();
                return;
            }

            transform.position += dir.normalized * Speed * Time.deltaTime;
            transform.Rotate(0f, 300f * Time.deltaTime, 0f);

            if (Time.time >= _nextTrailTime)
            {
                _nextTrailTime = Time.time + 0.08f;
                FxLibrary.TrySpawn("vfx_HitSparks", transform.position);
            }
        }

        private void Detonate()
        {
            _detonated = true;

            var pos = transform.position;
            FxLibrary.TrySpawnTimed("vfx_water_surface", pos, 2f);
            FxLibrary.TrySpawn("vfx_HitSparks", pos);

            float sqr = SplashRadius * SplashRadius;
            var all = Character.GetAllCharacters();
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (!(c is Player p) || p.IsDead()) continue;
                var offset = p.transform.position - pos;
                if (offset.sqrMagnitude > sqr) continue;

                var hit = new HitData();
                hit.m_pushForce      = 25f;
                hit.m_point          = pos;
                hit.m_dir            = offset.sqrMagnitude > 0f ? offset.normalized : Vector3.up;
                hit.m_hitType        = HitData.HitType.EnemyHit;
                hit.m_blockable      = true;
                hit.m_dodgeable      = true;
                hit.SetAttacker(_attacker);
                p.Damage(hit);
            }

            Destroy(gameObject);
        }
    }
}
