using System.Collections.Generic;
using UnityEngine;
using Jotunn.Managers;
using BiomeLords.Util;

namespace BiomeLords.Phase1D
{
    /// <summary>
    /// Per-instance behaviour for the Gammeltroll Lord (Deep North, tier 8),
    /// cloned from Valheim 1.0's <c>TrollFrost</c>:
    ///   • Petrify      — the first time its HP crosses 75 %, 50 % and 25 % it
    ///                    turns to stone for <see cref="PetrifyDuration"/> s: rooted,
    ///                    pose frozen, dull-grey, and every hit but pickaxe damage is
    ///                    cut to 10 % (GammeltrollShellPatch). Vanilla TrollFrost is
    ///                    already Weak to pickaxes, so a pick is the tool for the job.
    ///   • Shatter      — when the shell breaks it detonates: frost damage + knockback
    ///                    to every foe within <see cref="ShatterRadius"/> m, and the
    ///                    frost-rotted flesh beneath sloughs off as
    ///                    <see cref="MinionsPerShatter"/> Tiny Pulp (BlobMorkMini).
    ///   • Fimbul Fury  — after the third shatter (&lt;25 % HP): +30 % speed, attacks
    ///                    40 % more often, aura burns blue-white.
    ///
    /// The owner runs the logic and publishes the shell state through a ZDO flag,
    /// so every client renders the statue (tint + frozen animator) in sync; the
    /// damage rule is enforced by GammeltrollShellPatch on whichever peer processes
    /// the hit.
    /// </summary>
    public class GammeltrollLordBrain : MonoBehaviour
    {
        // ---- Petrify ----------------------------------------------------------
        public  const float PetrifyDuration   = 8f;
        public  const float ShellDamageFactor = 0.10f;   // non-pickaxe damage kept while petrified
        private static readonly float[] PetrifyThresholds = { 0.75f, 0.50f, 0.25f };

        // ---- Shatter ----------------------------------------------------------
        public  const float  ShatterRadius     = 14f;   // the 5.2x body alone is ~4 m across; melee range sits at 5-8 m from centre
        private const float  ShatterFrost      = 90f;
        private const float  ShatterPush       = 120f;
        private const int    MinionsPerShatter = 2;
        private const int    MaxNearbyMinions  = 4;
        private const float  MinionDetectRadius = 20f;
        private const string MinionPrefab      = "BlobMorkMini";

        // ---- Fimbul Fury ------------------------------------------------------
        private const float FurySpeedFactor  = 1.30f;
        private const float FuryAttackFactor = 0.60f;   // fraction of original min-attack interval

        private const string ZdoPetrified = "BiomeLords_GammeltrollPetrified";
        private const float  FrozenAnimSpeed = 0.02f;

        // ---- Runtime state ----------------------------------------------------
        private Character _character;
        private ZNetView  _nview;
        private MonsterAI _monsterAI;
        private Animator  _animator;

        private float _baseSpeed, _baseRunSpeed, _baseAttackInterval;
        private int   _shellsBroken;
        private bool  _furious;

        private float _petrifyEndTime = -1f;
        private bool  _petrifiedLocal;          // what this client currently renders

        // Aura light (child "BiomeLords_Aura" set up by CreatureFactory)
        private Light _auraLight;
        private Color _auraOrigColor;
        private float _auraOrigIntensity;
        private float _auraOrigRange;

        // Renderer tint snapshot for the stone look
        private readonly List<(Material mat, Color color)> _tintBackup = new List<(Material, Color)>();
        private static readonly Color StoneTint = new Color(0.45f, 0.45f, 0.47f, 1f);

        private static GameObject _cachedMinion;

        /// <summary>True while the stone shell is up — read by GammeltrollShellPatch on
        /// any peer, so it prefers the replicated ZDO flag over local state.</summary>
        public bool IsPetrified
        {
            get
            {
                if (_nview != null && _nview.IsValid())
                    return _nview.GetZDO().GetBool(ZdoPetrified, false);
                return _petrifiedLocal;
            }
        }

        // ---- Unity lifecycle --------------------------------------------------

        private void Awake()
        {
            _character = GetComponent<Character>();
            _nview     = GetComponent<ZNetView>();
            _monsterAI = GetComponent<MonsterAI>();
            _animator  = GetComponentInChildren<Animator>();

            if (_character != null)
            {
                _baseSpeed    = _character.m_speed;
                _baseRunSpeed = _character.m_runSpeed;
            }
            if (_monsterAI != null) _baseAttackInterval = _monsterAI.m_minAttackInterval;

            var auraT = transform.Find("BiomeLords_Aura");
            if (auraT != null)
            {
                _auraLight = auraT.GetComponent<Light>();
                if (_auraLight != null)
                {
                    _auraOrigColor     = _auraLight.color;
                    _auraOrigIntensity = _auraLight.intensity;
                    _auraOrigRange     = _auraLight.range;
                }
            }
        }

        private void Update()
        {
            // Visual sync runs on every peer, driven by the replicated flag.
            bool petrifiedNow = IsPetrified;
            if (petrifiedNow != _petrifiedLocal) ApplyStatueVisual(petrifiedNow);

            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner()) return;
            if (_character == null || _character.IsDead()) return;

            if (petrifiedNow)
            {
                HoldStill();
                if (Time.time >= _petrifyEndTime) Shatter();
                return;
            }

            TryPetrify();
        }

        private void OnDestroy()
        {
            // Never leave shared materials tinted if the object is torn down mid-shell.
            if (_petrifiedLocal) RestoreTint();
        }

        // ---- Petrify ----------------------------------------------------------

        private void TryPetrify()
        {
            if (_shellsBroken >= PetrifyThresholds.Length) return;
            float frac = _character.GetHealth() / _character.GetMaxHealth();
            if (frac > PetrifyThresholds[_shellsBroken]) return;

            _petrifyEndTime = Time.time + PetrifyDuration;
            _nview.GetZDO().Set(ZdoPetrified, true);
            HoldStill();

            var pos = transform.position + Vector3.up * 2f;
            FxLibrary.TrySpawn("fx_guardstone_activate",       pos);
            FxLibrary.TrySpawn("vfx_corpse_destruction_small", pos);
            FxLibrary.TrySpawn("vfx_frosttroll_hit",           pos);

            var local = Player.m_localPlayer;
            if (local != null) local.Message(MessageHud.MessageType.Center, "$biomelords_gammeltroll_petrify");
            Jotunn.Logger.LogInfo($"[BiomeLords] Gammeltroll Lord petrified (shell {_shellsBroken + 1}/{PetrifyThresholds.Length}).");
        }

        /// <summary>Rooted every frame while the shell is up — Fury or anything else
        /// touching speed mid-window must not undo it.</summary>
        private void HoldStill()
        {
            _character.m_speed    = 0f;
            _character.m_runSpeed = 0f;
        }

        private void Shatter()
        {
            _nview.GetZDO().Set(ZdoPetrified, false);
            _shellsBroken++;
            RestoreSpeed();

            var center = transform.position;
            var pos    = center + Vector3.up * 1.5f;
            FxLibrary.TrySpawn("vfx_TrollFrost_Death",      pos);
            FxLibrary.TrySpawn("vfx_trollsnow_groundslam",  center);
            FxLibrary.TrySpawn("vfx_troll_death",           pos);
            FxLibrary.TrySpawn("fx_himminafl_aoe",          center);
            for (int i = 0; i < 12; i++)
            {
                var off = Quaternion.Euler(0f, i * 30f, 0f) * Vector3.forward * (ShatterRadius * 0.5f);
                FxLibrary.TrySpawn("vfx_HitSparks", center + off + Vector3.up * 0.5f);
            }

            // Frost burst on every foe within reach.
            float sqr = ShatterRadius * ShatterRadius;
            int   struck = 0;
            var   all = Character.GetAllCharacters();
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || c == _character || c.IsDead()) continue;
                if (!BaseAI.IsEnemy(_character, c)) continue;
                var delta = c.transform.position - center;
                if (delta.sqrMagnitude > sqr) continue;

                var hit = new HitData();
                hit.m_damage.m_frost = ShatterFrost;
                hit.m_point          = c.transform.position + Vector3.up;
                hit.m_dir            = delta.sqrMagnitude > 0.01f ? delta.normalized : Vector3.forward;
                hit.m_pushForce      = ShatterPush;
                hit.m_hitType        = HitData.HitType.EnemyHit;
                hit.m_blockable      = true;
                hit.m_dodgeable      = true;
                // No attacker on purpose: the burst is designed at ShatterFrost and
                // must not be rewritten to the Lord's melee profile by Character_Damage_LordBoost.
                c.Damage(hit);
                struck++;
            }

            SpawnMinions(MinionsPerShatter);

            var local = Player.m_localPlayer;
            if (local != null) local.Message(MessageHud.MessageType.Center, "$biomelords_gammeltroll_shatter");
            Jotunn.Logger.LogInfo($"[BiomeLords] Gammeltroll Lord shell shattered: {struck} struck.");

            if (_shellsBroken >= PetrifyThresholds.Length) EnterFury();
        }

        private void RestoreSpeed()
        {
            _character.m_speed    = _furious ? _baseSpeed    * FurySpeedFactor : _baseSpeed;
            _character.m_runSpeed = _furious ? _baseRunSpeed * FurySpeedFactor : _baseRunSpeed;
        }

        // ---- Fimbul Fury ------------------------------------------------------

        private void EnterFury()
        {
            if (_furious) return;
            _furious = true;
            RestoreSpeed();
            if (_monsterAI != null) _monsterAI.m_minAttackInterval = _baseAttackInterval * FuryAttackFactor;
            UpdateAura();

            var pos = transform.position + Vector3.up * 2f;
            FxLibrary.TrySpawn("fx_redlightning_burst", pos);
            FxLibrary.TrySpawn("vfx_frosttroll_hit",    pos);
            Jotunn.Logger.LogInfo("[BiomeLords] Gammeltroll Lord entered Fimbul Fury.");
        }

        // ---- Statue visuals (all peers) --------------------------------------

        private void ApplyStatueVisual(bool petrified)
        {
            _petrifiedLocal = petrified;
            if (petrified)
            {
                // Not 0: CharacterAnimEvent.FixedUpdate snaps any speed < 0.01 back to 1
                // every frame (its own freeze-frame guard). 0.02 reads as a statue and
                // survives; ZSyncAnimation replicates the owner's value to every peer.
                if (_animator != null) _animator.speed = FrozenAnimSpeed;
                ApplyStoneTint();
            }
            else
            {
                if (_animator != null) _animator.speed = 1f;
                RestoreTint();
            }
            UpdateAura();
        }

        private void ApplyStoneTint()
        {
            if (_tintBackup.Count > 0) return;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                foreach (var m in r.materials)      // instance materials — safe to edit
                {
                    if (m == null || !m.HasProperty("_Color")) continue;
                    _tintBackup.Add((m, m.color));
                    m.color = StoneTint;
                }
            }
        }

        private void RestoreTint()
        {
            foreach (var (mat, color) in _tintBackup)
                if (mat != null) mat.color = color;
            _tintBackup.Clear();
        }

        // Priority: petrified (grey) > fury (blue-white blaze) > default.
        private void UpdateAura()
        {
            if (_auraLight == null) return;
            if (_petrifiedLocal)
            {
                _auraLight.color     = new Color(0.55f, 0.55f, 0.58f);
                _auraLight.intensity = 1.2f;
                _auraLight.range     = 10f;
            }
            else if (_furious)
            {
                _auraLight.color     = new Color(0.80f, 0.95f, 1.00f);
                _auraLight.intensity = 4.5f;
                _auraLight.range     = 20f;
            }
            else
            {
                _auraLight.color     = _auraOrigColor;
                _auraLight.intensity = _auraOrigIntensity;
                _auraLight.range     = _auraOrigRange;
            }
        }

        // ---- Minions ------------------------------------------------------------

        private void SpawnMinions(int requested)
        {
            if (_cachedMinion == null) _cachedMinion = PrefabManager.Instance.GetPrefab(MinionPrefab);
            if (_cachedMinion == null)
            {
                Jotunn.Logger.LogWarning($"[BiomeLords] Gammeltroll Lord: '{MinionPrefab}' prefab not found — no minions.");
                return;
            }
            int alive  = CountNearbyMinions();
            int budget = System.Math.Min(requested, MaxNearbyMinions - alive);
            for (int i = 0; i < budget; i++)
            {
                float   angle = i * (360f / System.Math.Max(1, budget)) + Random.Range(-30f, 30f);
                Vector3 dir   = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 pos   = transform.position + dir * 6f + Vector3.up * 0.5f;   // clear of the 5.2x body collider
                Instantiate(_cachedMinion, pos, Quaternion.LookRotation(dir));
                FxLibrary.TrySpawn("vfx_blob_attack", pos);
            }
        }

        private int CountNearbyMinions()
        {
            int   count  = 0;
            var   center = transform.position;
            float sqr    = MinionDetectRadius * MinionDetectRadius;
            var   all    = Character.GetAllCharacters();
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || c == _character || c.IsDead()) continue;
                if ((c.transform.position - center).sqrMagnitude > sqr) continue;
                if (c.gameObject.name.StartsWith(MinionPrefab)) count++;
            }
            return count;
        }
    }
}
