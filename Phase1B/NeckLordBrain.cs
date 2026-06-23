using System.Collections;
using UnityEngine;
using Jotunn.Managers;
using BiomeLords.Util;

namespace BiomeLords.Phase1B
{
    /// <summary>
    /// Per-instance extra behaviour for the Neck Lord:
    ///   • Tide Caller — periodically summons normal Necks as minions.
    ///   • Frenzy      — at &lt;30% HP, speed and tint shift.
    ///
    /// Attached to the cloned NeckLord prefab in CreatureFactory.
    /// Only the network owner of the GameObject runs the logic (standard
    /// Valheim authority model), so it's safe in multiplayer.
    /// </summary>
    public class NeckLordBrain : MonoBehaviour
    {
        private const float MinionCooldown      = 45f;
        private const int   MaxNearbyMinions    = 3;
        private const float MinionSpawnRadius   = 3f;
        private const float MinionDetectRadius  = 20f;
        private const float FrenzyHpFraction    = 0.30f;
        private const float FrenzySpeedFactor   = 1.5f;
        private const string MinionPrefabName   = "Neck";

        private const float BlobCooldown        = 12f;
        private const float BlobMinRange        =  5f;
        private const float BlobMaxRange        = 18f;

        private const float BlockHpFraction     = 0.50f;
        private const float BlockDuration       =  2.5f;
        private const float BlockInterCooldown  = 12f;
        private const float BlockDetectRadius   =  6f;

        private Character _character;
        private ZNetView  _nview;

        private float  _baseSpeed;
        private float  _baseRunSpeed;
        private bool   _frenzied;
        private float  _nextMinionTime;
        private float  _nextFrenzyPulse;
        private float  _nextBlobTime;
        private bool   _blockPhaseActive;
        private float  _nextBlockTime;
        private float  _blockEndTime;
        private string _attackAnimTrigger;

        public bool IsBlocking { get; private set; }

        // Cached prefab ref (looked up once).
        private static GameObject _cachedMinion;

        private void Awake()
        {
            _character = GetComponent<Character>();
            _nview     = GetComponent<ZNetView>();

            if (_character != null)
            {
                _baseSpeed    = _character.m_speed;
                _baseRunSpeed = _character.m_runSpeed;
            }

            // Cache the Neck's attack animation trigger so we can replay it on blob fire.
            var anim = GetComponent<Animator>();
            if (anim != null)
            {
                foreach (var p in anim.parameters)
                {
                    if (p.type == AnimatorControllerParameterType.Trigger &&
                        p.name.ToLower().Contains("attack"))
                    {
                        _attackAnimTrigger = p.name;
                        break;
                    }
                }
            }

            // First summon attempt after a 15s grace so the player engages first.
            _nextMinionTime = Time.time + 15f;
            _nextBlobTime   = Time.time + 10f;
        }

        private void Update()
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner()) return;
            if (_character == null || _character.IsDead()) return;

            TryFrenzy();
            TrySummonMinions();
            TryWaterBlob();
            TryEnterBlockPhase();
            TryBlock();
            TickBlock();
            TickFrenzyAura();
        }

        /// <summary>While frenzied, pulse a small VFX on the Lord every ~0.4s so
        /// the player has a continuous visual cue something has changed.</summary>
        private void TickFrenzyAura()
        {
            if (!_frenzied) return;
            if (Time.time < _nextFrenzyPulse) return;
            _nextFrenzyPulse = Time.time + 0.4f;

            var pos = transform.position + Vector3.up * 1.0f;
            FxLibrary.TrySpawn("vfx_HitSparks", pos);
            // Small chance of a red lightning ember for a "boiling rage" feel.
            // (No fx_crit — its yellow number can read as the Lord taking damage.)
            if (Random.value < 0.4f) FxLibrary.TrySpawn("fx_redlightning_burst", pos + Vector3.up * 0.3f);
        }

        private void TryFrenzy()
        {
            if (_frenzied) return;
            float frac = _character.GetHealth() / _character.GetMaxHealth();
            if (frac > FrenzyHpFraction) return;

            _frenzied = true;
            _character.m_speed    = _baseSpeed    * FrenzySpeedFactor;
            _character.m_runSpeed = _baseRunSpeed * FrenzySpeedFactor;

            // Brighter, more saturated red on frenzy.
            var frenzyTint = new Color(1f, 0.1f, 0.1f, 1f);
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.materials)
                {
                    if (m == null) continue;
                    if (m.HasProperty("_Color"))        m.color = frenzyTint;
                    if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", frenzyTint * 0.8f);
                }

            // One-shot frenzy burst — bigger so the moment is unmistakable.
            // Avoid fx_crit (yellow crit number is misleading — looks like self-damage).
            var pos = transform.position + Vector3.up * 1.0f;
            FxLibrary.TrySpawn("vfx_neck_hit", pos);
            FxLibrary.TrySpawn("vfx_corpse_destruction_small", pos);
            FxLibrary.TrySpawn("fx_redlightning_burst", pos);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f;
                var off = Quaternion.Euler(0f, a, 0f) * Vector3.forward * 1.2f;
                FxLibrary.TrySpawn("vfx_HitSparks", pos + off);
            }

            Jotunn.Logger.LogInfo("[BiomeLords] Neck Lord entered frenzy.");
        }

        private void TryEnterBlockPhase()
        {
            if (_blockPhaseActive) return;
            if (_character.GetHealth() / _character.GetMaxHealth() > BlockHpFraction) return;
            _blockPhaseActive = true;
            _nextBlockTime    = Time.time + 2f; // brief grace before first block can fire
            Jotunn.Logger.LogInfo("[BiomeLords] Neck Lord entered block phase.");
        }

        private void TryBlock()
        {
            if (!_blockPhaseActive || IsBlocking) return;
            if (Time.time < _nextBlockTime) return;

            var player = Player.m_localPlayer;
            if (player == null || player.IsDead()) return;
            if (Vector3.Distance(transform.position, player.transform.position) > BlockDetectRadius) return;
            if (!player.InAttack()) return;

            IsBlocking     = true;
            _blockEndTime  = Time.time + BlockDuration;
            _nextBlockTime = _blockEndTime + BlockInterCooldown;

            FxLibrary.TrySpawn("fx_guardstone_activate", transform.position + Vector3.up * 0.8f);
        }

        private void TickBlock()
        {
            if (!IsBlocking) return;
            if (Time.time < _blockEndTime) return;
            IsBlocking = false;
        }

        private void TryWaterBlob()
        {
            if (Time.time < _nextBlobTime) return;
            var target = Player.m_localPlayer;
            if (target == null || target.IsDead()) return;
            float dist = Vector3.Distance(transform.position, target.transform.position);
            if (dist < BlobMinRange || dist > BlobMaxRange) return;

            // Only fire when facing the player (within ~60° forward arc).
            var flat = target.transform.position - transform.position;
            flat.y = 0f;
            if (Vector3.Dot(transform.forward, flat.normalized) < 0.5f) return;

            _nextBlobTime = Time.time + BlobCooldown;
            StartCoroutine(BlobThrowSequence(target.transform.position));
        }

        private IEnumerator BlobThrowSequence(Vector3 targetPos)
        {
            // Freeze in place for 1s so the throw is telegraphed.
            _character.m_speed    = 0f;
            _character.m_runSpeed = 0f;

            yield return new WaitForSeconds(1f);

            float speedMult       = _frenzied ? FrenzySpeedFactor : 1f;
            _character.m_speed    = _baseSpeed    * speedMult;
            _character.m_runSpeed = _baseRunSpeed * speedMult;

            NeckBlobProjectile.Fire(transform.position, targetPos, _character);

            if (_frenzied)
            {
                var dir   = targetPos - transform.position;
                dir.y = 0f;
                var left  = transform.position + Quaternion.Euler(0f, -20f, 0f) * dir;
                var right = transform.position + Quaternion.Euler(0f,  20f, 0f) * dir;
                NeckBlobProjectile.Fire(transform.position, left,  _character);
                NeckBlobProjectile.Fire(transform.position, right, _character);
            }
        }

        private void TrySummonMinions()
        {
            if (Time.time < _nextMinionTime) return;
            if (CountNearbyMinions() >= MaxNearbyMinions)
            {
                // Push next attempt out a bit so we don't poll constantly when capped.
                _nextMinionTime = Time.time + 10f;
                return;
            }

            EnsurePrefabs();
            if (_cachedMinion == null) return;

            for (int i = 0; i < 2; i++)
            {
                float angle  = (i * 180f) + Random.Range(-30f, 30f);
                Vector3 dir  = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 pos  = transform.position + dir * MinionSpawnRadius + Vector3.up * 0.2f;
                Instantiate(_cachedMinion, pos, Quaternion.LookRotation(-dir));
                FxLibrary.TrySpawn("vfx_spawn", pos);
            }

            _nextMinionTime = Time.time + MinionCooldown;
        }

        private int CountNearbyMinions()
        {
            int count = 0;
            var center = transform.position;
            var sqrR   = MinionDetectRadius * MinionDetectRadius;
            var all    = Character.GetAllCharacters();
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || c == _character || c.IsDead()) continue;
                if ((c.transform.position - center).sqrMagnitude > sqrR) continue;
                var n = c.gameObject.name;
                if (n.StartsWith("Neck")) count++;
            }
            return count;
        }

        private static void EnsurePrefabs()
        {
            if (_cachedMinion == null) _cachedMinion = PrefabManager.Instance.GetPrefab(MinionPrefabName);
        }
    }
}
