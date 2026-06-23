using UnityEngine;
using BiomeLords.Util;

namespace BiomeLords.Phase1D
{
    /// <summary>
    /// Hovering poison cloud spawned by the Draugr Elite Lord at 60% HP.
    /// Stationary, fades after a fixed duration, ticks poison damage to any
    /// player inside its radius. Pulses mist + blob visuals so it reads as
    /// a clearly hazardous zone the player should not stand in.
    /// </summary>
    public class PlagueCloud : MonoBehaviour
    {
        public float Radius          = 5f;
        public float Duration        = 25f;
        public float PoisonPerSecond = 5f;
        public float TickInterval    = 1f;
        public float VisualInterval  = 0.5f;
        public Character Attacker;

        private float _spawnTime;
        private float _nextTick;
        private float _nextVisual;

        private void Awake()
        {
            _spawnTime  = Time.time;
            _nextTick   = Time.time + TickInterval;
            _nextVisual = Time.time;
        }

        private void Update()
        {
            if (Time.time - _spawnTime >= Duration)
            {
                Object.Destroy(gameObject);
                return;
            }

            if (Time.time >= _nextVisual)
            {
                _nextVisual = Time.time + VisualInterval;
                FxLibrary.TrySpawnTimed("vfx_blob_attack", transform.position, 3f);
                if (Random.value < 0.30f) FxLibrary.TrySpawnTimed("vfx_blob_attack", transform.position + Vector3.up * 0.6f, 4f);
            }

            if (Time.time >= _nextTick)
            {
                _nextTick = Time.time + TickInterval;
                var p = Player.m_localPlayer;
                if (p == null || p.IsDead()) return;

                var sqr = Radius * Radius;
                if ((p.transform.position - transform.position).sqrMagnitude > sqr) return;

                var hit = new HitData();
                hit.m_damage.m_poison = PoisonPerSecond * TickInterval;
                hit.m_point           = transform.position;
                hit.m_hitType         = HitData.HitType.EnemyHit;
                hit.m_blockable       = true;
                hit.m_dodgeable       = true;
                // Intentionally NOT setting Attacker — keeps the cloud
                // damage at its designed 5 poison/sec, bypassing the Lord
                // damage-boost patch.
                p.Damage(hit);
            }
        }
    }
}
