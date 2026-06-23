using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Jotunn.Managers;
using BiomeLords.Util;

namespace BiomeLords.Phase1D
{
    /// <summary>
    /// Per-instance behaviour for the Fenring Lord (Mountain, tier 4):
    ///   • Bat Summon      — every 60 s spawn up to 2 Bats (cap 4 nearby);
    ///                       plays the attack_taunt animation on trigger.
    ///   • Vampiric Strike — when HP drops below 80 %, activate a 60 s buff:
    ///                       absorbs 80 % of each melee hit as healing,
    ///                       blood-red aura glow, GP activation VFX on trigger.
    ///                       Disabled once Blood Frenzy is active.
    ///   • Shadow Fade     — at &lt;60 % HP, every 60 s: plays attack_taunt,
    ///                       then after 1 s hides for 12 s (renderers hidden;
    ///                       particle/foot effects stay visible), summons Bats.
    ///   • Blood Frenzy    — at 30 % HP: +50 % speed, crimson aura, and
    ///                       MonsterAI jump interval cut to 30 %.
    /// Only the network owner runs the logic.
    /// </summary>
    public class FenringLordBrain : MonoBehaviour
    {
        // ---- Bat summon ------------------------------------------------------
        private const float  BatSummonCooldown = 60f;
        private const int    MaxNearbyBats     = 4;
        private const int    BatsPerSummon     = 2;
        private const float  BatDetectRadius   = 16f;
        private const string BatPrefab         = "Bat";

        // ---- Vampiric Strike buff -------------------------------------------
        private const float VampHpTrigger    = 0.80f;
        private const float VampCooldown     = 40f;   // gap after buff expires before re-arm
        public  const float VampHealFraction = 0.80f; // fraction of hit damage absorbed as healing — referenced by FenringVampPatch
        private const float VampDuration     = 60f;

        // ---- Shadow Fade (invisibility) -------------------------------------
        private const float InvisHpTrigger   = 0.60f; // only usable below this HP fraction
        private const float InvisCooldown    = 60f;
        private const float InvisDuration    = 12f;
        private const float InvisPreDelay    = 1f;    // seconds between taunt anim and hiding

        // ---- Blood Frenzy ---------------------------------------------------
        private const float FrenzyHpFraction        = 0.30f;
        private const float FrenzySpeedFactor       = 1.5f;
        private const float FrenzyJumpFactor   = 0.20f; // fraction of original jump interval
        private const float FrenzyAttackFactor = 0.30f; // fraction of original min-attack interval

        // ---- Runtime state --------------------------------------------------
        private Character _character;
        private ZNetView  _nview;
        private MonsterAI _monsterAI;

        private float _baseSpeed, _baseRunSpeed, _baseJumpInterval, _baseAttackInterval;
        private bool  _frenzied;

        private float _nextBatSummonTime;
        private float _nextVampTime;
        private float _vampEndTime = -1f;
        private float _nextInvisTime;
        private float _nextFrenzyPulse;

        // Aura light (child "BiomeLords_Aura" set up by CreatureFactory)
        private Light _auraLight;
        private Color _auraOrigColor;
        private float _auraOrigIntensity;
        private float _auraOrigRange;

        public bool IsVampActive => Time.time < _vampEndTime;

        // Cached prefab ref (static — shared across all instances)
        private static GameObject _cachedBat;

        // Animator for triggering ability animations
        private Animator _animator;

        // ---- Unity lifecycle ------------------------------------------------

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
            if (_monsterAI != null)
            {
                _baseJumpInterval   = _monsterAI.m_jumpInterval;
                _baseAttackInterval = _monsterAI.m_minAttackInterval;
            }

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

            _nextBatSummonTime = Time.time + 15f;
            _nextVampTime      = Time.time + 10f;
            _nextInvisTime     = Time.time + 20f;
        }

        private void Update()
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner()) return;
            if (_character == null || _character.IsDead()) return;

            TryFrenzy();
            TryBatSummon();
            TryVampiricStrike();
            TryInvisibility();
            TickFrenzyAura();
        }

        // ---- Abilities -------------------------------------------------------

        private void TryFrenzy()
        {
            if (_frenzied) return;
            float frac = _character.GetHealth() / _character.GetMaxHealth();
            if (frac > FrenzyHpFraction) return;
            _frenzied = true;

            _character.m_speed    = _baseSpeed    * FrenzySpeedFactor;
            _character.m_runSpeed = _baseRunSpeed * FrenzySpeedFactor;

            if (_monsterAI != null)
            {
                _monsterAI.m_jumpInterval      = _baseJumpInterval   * FrenzyJumpFactor;
                _monsterAI.m_minAttackInterval = _baseAttackInterval * FrenzyAttackFactor;
            }

            // Directly bias the AI attack item intervals so the jump attack is
            // heavily preferred over standing claw swings.
            ApplyFrenzyAttackBias();

            UpdateAura();

            var pos = transform.position + Vector3.up * 1f;
            FxLibrary.TrySpawn("vfx_corpse_destruction_small", pos);
            FxLibrary.TrySpawn("fx_redlightning_burst",        pos);
            for (int i = 0; i < 8; i++)
            {
                float a   = i * 45f;
                var   off = Quaternion.Euler(0f, a, 0f) * Vector3.forward * 1.4f;
                FxLibrary.TrySpawn("vfx_HitSparks", pos + off);
            }
            Jotunn.Logger.LogInfo("[BiomeLords] Fenring Lord entered Blood Frenzy.");
        }

        private void TryBatSummon()
        {
            if (Time.time < _nextBatSummonTime) return;
            int alive = CountNearbyBats();
            if (alive >= MaxNearbyBats)
            {
                _nextBatSummonTime = Time.time + 15f;
                return;
            }
            PlayTaunt();
            SpawnBats(BatsPerSummon);
            _nextBatSummonTime = Time.time + BatSummonCooldown;
        }

        private void TryVampiricStrike()
        {
            // Vampiric Strike is suppressed once Blood Frenzy takes over.
            if (_frenzied || IsVampActive || Time.time < _nextVampTime) return;

            float frac = _character.GetHealth() / _character.GetMaxHealth();
            if (frac > VampHpTrigger) return;

            _vampEndTime  = Time.time + VampDuration;
            _nextVampTime = _vampEndTime + VampCooldown;

            // Vanilla Forsaken Power activation VFX (same prefab used by DraugrLordBrain's Rotten Cleave).
            var pos = transform.position + Vector3.up * 1.5f;
            FxLibrary.TrySpawn("fx_GP_Activation", pos);

            UpdateAura();
            StartCoroutine(VampExpireCoroutine());
            Jotunn.Logger.LogInfo("[BiomeLords] Fenring Lord activated Vampiric Strike.");
        }

        private IEnumerator VampExpireCoroutine()
        {
            yield return new WaitForSeconds(VampDuration);
            UpdateAura();
        }

        private void TryInvisibility()
        {
            if (Time.time < _nextInvisTime) return;

            // Shadow Fade only becomes available once the Fenring is wounded.
            float frac = _character.GetHealth() / _character.GetMaxHealth();
            if (frac > InvisHpTrigger) return;

            var p = Player.m_localPlayer;
            if (p == null || p.IsDead()) return;
            if ((p.transform.position - transform.position).sqrMagnitude > 20f * 20f) return;

            _nextInvisTime = Time.time + InvisCooldown;
            StartCoroutine(InvisibilityRoutine());
        }

        private IEnumerator InvisibilityRoutine()
        {
            // Taunt animation plays first — gives the player a 1 s window to react.
            PlayTaunt();
            SpawnBats(BatsPerSummon);

            var centerPos = transform.position + Vector3.up * 1f;
            FxLibrary.TrySpawn("vfx_mist_puff", centerPos);
            FxLibrary.TrySpawn("vfx_spawn",     centerPos);

            yield return new WaitForSeconds(InvisPreDelay);

            // Hide all mesh renderers; deliberately skip ParticleSystemRenderer so
            // the footstep / ground-dust particles stay visible as tracking hints.
            var allRenderers = GetComponentsInChildren<Renderer>(false);
            var hidden       = new List<Renderer>(allRenderers.Length);
            foreach (var r in allRenderers)
            {
                if (r is ParticleSystemRenderer) continue;
                r.enabled = false;
                hidden.Add(r);
            }

            yield return new WaitForSeconds(InvisDuration);

            // Restore visibility.
            foreach (var r in hidden)
                if (r != null) r.enabled = true;

            FxLibrary.TrySpawn("vfx_spawn", transform.position + Vector3.up * 1f);
        }

        private void TickFrenzyAura()
        {
            if (!_frenzied) return;
            if (Time.time < _nextFrenzyPulse) return;
            _nextFrenzyPulse = Time.time + 0.5f;
            var pos = transform.position + Vector3.up * 1.0f;
            FxLibrary.TrySpawn("vfx_HitSparks", pos);
            if (Random.value < 0.4f) FxLibrary.TrySpawn("fx_redlightning_burst", pos + Vector3.up * 0.3f);
        }

        // ---- Attack bias ----------------------------------------------------

        /// <summary>
        /// On Blood Frenzy entry, find the jump-attack item in the Humanoid's
        /// weapon arrays and heavily favour it over all other attacks by:
        ///   • shrinking its m_aiAttackInterval to 15% of the original
        ///   • setting m_aiPrioritized = true
        ///   • multiplying every other attack's interval by 5×
        ///
        /// m_aiAttackInterval is per-item SharedData — modifying it is safe
        /// here because fenring_lord is a unique cloned prefab and frenzy is
        /// a one-way permanent state (values never need restoring).
        ///
        /// A diagnostic log lists every attack animation found so the animation
        /// name match can be verified in BepInEx/LogOutput.log at first use.
        /// </summary>
        private void ApplyFrenzyAttackBias()
        {
            var humanoid = GetComponent<Humanoid>();
            if (humanoid == null) return;

            bool applied = BiasArray(humanoid.m_defaultItems)
                         | BiasArray(humanoid.m_randomWeapon);

            if (!applied)
                Jotunn.Logger.LogWarning(
                    "[BiomeLords] Fenring Lord frenzy: no jump-attack item found — " +
                    "attack bias not applied. Check the attack-animation names in the log.");
        }

        private static bool BiasArray(GameObject[] items)
        {
            if (items == null || items.Length == 0) return false;

            bool hasJump = false;
            foreach (var go in items)
                if (go != null && IsJumpAttack(go)) { hasJump = true; break; }
            if (!hasJump) return false;

            foreach (var go in items)
            {
                if (go == null) continue;
                var shared = go.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
                if (shared == null) continue;

                if (IsJumpAttack(go))
                {
                    shared.m_aiAttackInterval = Mathf.Max(0.5f, shared.m_aiAttackInterval * 0.15f);
                    shared.m_aiPrioritized    = true;
                }
                else
                {
                    shared.m_aiAttackInterval *= 5f;
                    shared.m_aiPrioritized     = false;
                }
            }
            return true;
        }

        // The jump attack is identified by its prefab name, not animation string.
        private static bool IsJumpAttack(GameObject go) =>
            go.name.IndexOf("jump", System.StringComparison.OrdinalIgnoreCase) >= 0;

        // ---- Aura state machine ---------------------------------------------
        // Priority: frenzy (permanent) > vampiric buff (temporary) > default.

        private void UpdateAura()
        {
            if (_auraLight == null) return;

            if (_frenzied)
            {
                _auraLight.color     = new Color(1.0f, 0.15f, 0.10f);
                _auraLight.intensity = 4.0f;
                _auraLight.range     = 6.5f;
            }
            else if (IsVampActive)
            {
                // Deep blood-red pulsing glow signals the vampiric buff.
                _auraLight.color     = new Color(0.75f, 0.0f, 0.15f);
                _auraLight.intensity = 3.5f;
                _auraLight.range     = 5.5f;
            }
            else
            {
                _auraLight.color     = _auraOrigColor;
                _auraLight.intensity = _auraOrigIntensity;
                _auraLight.range     = _auraOrigRange;
            }
        }

        // ---- Bat summon helpers ---------------------------------------------

        private void SpawnBats(int requested)
        {
            EnsurePrefabs();
            if (_cachedBat == null) return;
            int alive  = CountNearbyBats();
            int budget = System.Math.Min(requested, MaxNearbyBats - alive);
            for (int i = 0; i < budget; i++)
            {
                float   angle = i * (360f / System.Math.Max(1, budget)) + Random.Range(-25f, 25f);
                Vector3 dir   = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 pos   = transform.position + dir * 3f + Vector3.up * 1.5f;
                Instantiate(_cachedBat, pos, Quaternion.LookRotation(-dir));
                FxLibrary.TrySpawn("vfx_spawn", pos);
            }
        }

        private int CountNearbyBats()
        {
            int   count  = 0;
            var   center = transform.position;
            float sqr    = BatDetectRadius * BatDetectRadius;
            var   all    = Character.GetAllCharacters();
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || c == _character || c.IsDead()) continue;
                if ((c.transform.position - center).sqrMagnitude > sqr) continue;
                if (c.gameObject.name.StartsWith("Bat")) count++;
            }
            return count;
        }

        private void PlayTaunt()
        {
            _animator?.SetTrigger("Taunt");
        }

        private static void EnsurePrefabs()
        {
            if (_cachedBat == null) _cachedBat = PrefabManager.Instance.GetPrefab(BatPrefab);
        }
    }
}
