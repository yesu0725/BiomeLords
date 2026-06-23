using System.Collections;
using UnityEngine;
using BiomeLords.Util;

namespace BiomeLords.Phase1D
{
    /// <summary>
    /// Per-instance behaviour for the Fallen Valkyrie Lord (Ashlands, tier 7),
    /// cloned from the vanilla FallenValkyrie boss so the Lord keeps genuine
    /// flight via the engine's Character.TakeOff/Land system. Every ability
    /// opens with a short cast tell (an alert/taunt VFX on the Valkyrie's body)
    /// so the player gets a visual warning before it fires:
    ///   • Wind Gust Knockback — a spin-up (the Valkyrie's wing-spin pose) then
    ///     a radial gust that pushes nearby players back. Available at any HP.
    ///   • Dive Bomb Slam (≤80% HP) — climbs skyward, then dives onto the
    ///     player's snapshotted position; the slam VFX fires on impact.
    ///   • Soul Harvest (≤30% HP, locks out again above 50%) — hovers airborne,
    ///     tethering to nearby players and draining spirit health to heal itself.
    ///   • Rage (≤30% HP, permanent) — +50% speed, radiant aura, cooldowns ×0.6.
    /// </summary>
    public class FallerValkyrieLordBrain : MonoBehaviour
    {
        private const float AbilityTellDelay = 0.5f;   // wind-up before any ability fires

        private const float DiveCooldown    = 13f;
        private const float DiveRange       = 20f;
        private const float DiveRadius      = 4f;
        private const float DiveHpThreshold = 0.80f;   // Dive Bomb unlocks below 80% HP
        private const float DiveAscend      = 0.6f;
        private const float DiveDescend     = 0.5f;
        private const float DiveSkyHeight   = 10f;
        private const float DivePushForce   = 70f;

        private const float GustCooldown     = 16f;
        private const float GustTriggerRange = 7f;
        private const float GustRadius       = 6f;
        private const float GustPushForce    = 50f;
        private const float GustTelegraph    = 0.4f;

        private const float HarvestCooldown      = 30f;
        private const float HarvestTriggerRange  = 12f;
        private const float HarvestRadius        = 10f;
        private const float HarvestDuration      = 6f;
        private const float HarvestTickInterval  = 0.5f;
        private const float HarvestDrainPerTick  = 6f;
        private const float HarvestArmThreshold    = 0.30f; // arms below 30% HP
        private const float HarvestDisarmThreshold = 0.50f; // locks out again above 50% HP

        private const float RageHpFraction  = 0.30f;
        private const float RageSpeedFactor = 1.5f;

        private Character _character;
        private ZNetView  _nview;
        private MonsterAI _monsterAI;

        private float _baseSpeed, _baseRunSpeed;
        private bool  _raging;
        private bool  _inSpecial;
        private bool  _harvestArmed;
        private float _nextDiveTime;
        private float _nextGustTime;
        private float _nextHarvestTime;
        private float _nextRagePulse;

        private void Awake()
        {
            _character = GetComponent<Character>();
            _nview     = GetComponent<ZNetView>();
            _monsterAI = GetComponent<MonsterAI>();
            if (_character != null)
            {
                _baseSpeed    = _character.m_speed;
                _baseRunSpeed = _character.m_runSpeed;
            }
            _nextDiveTime    = Time.time + 8f;
            _nextGustTime    = Time.time + 6f;
            _nextHarvestTime = Time.time + 12f;
        }

        private void Update()
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner()) return;
            if (_character == null || _character.IsDead()) return;

            TryRage();
            UpdateHarvestArming();
            TryDiveBombSlam();
            TryWindGustKnockback();
            TrySoulHarvest();
            TickRageAura();
        }

        // ---- Abilities -----------------------------------------------------

        private float HealthFrac()
        {
            float max = _character.GetMaxHealth();
            return max > 0f ? _character.GetHealth() / max : 1f;
        }

        /// <summary>Cast tell — an alert/taunt flash on the Valkyrie's body that
        /// warns the player an ability is about to fire. Played at the start of
        /// every ability before any movement or damage.</summary>
        private void SpawnAbilityTell()
        {
            var pos = transform.position + Vector3.up * 1.5f;
            FxLibrary.TrySpawn("fx_fallenvalkyrie_alert", pos);
            FxLibrary.TrySpawn("fx_fallenvalkyrie_taunt", transform.position);
            FxLibrary.TrySpawn("fx_himminafl_aoe",        pos);
        }

        private void TryRage()
        {
            if (_raging) return;
            if (HealthFrac() > RageHpFraction) return;
            _raging = true;

            _character.m_speed    = _baseSpeed    * RageSpeedFactor;
            _character.m_runSpeed = _baseRunSpeed * RageSpeedFactor;

            var auraT = transform.Find("BiomeLords_Aura");
            if (auraT != null)
            {
                var light = auraT.GetComponent<Light>();
                if (light != null)
                {
                    light.color     = new Color(1.0f, 0.95f, 0.75f);  // radiant white-gold
                    light.intensity = 5.0f;
                    light.range     = 8.5f;
                }
            }

            var pos = transform.position + Vector3.up * 1.2f;
            FxLibrary.TrySpawn("fx_fallenvalkyrie_screech",    pos);
            FxLibrary.TrySpawn("fx_himminafl_aoe",             pos);
            FxLibrary.TrySpawn("vfx_corpse_destruction_small", pos);
            FxLibrary.TrySpawn("fx_redlightning_burst",        pos);
            Jotunn.Logger.LogInfo("[BiomeLords] Fallen Valkyrie Lord entered Rage.");
        }

        /// <summary>Soul Harvest hysteresis: arm below 30% HP, lock out again
        /// once healing pushes the Valkyrie back above 50% HP.</summary>
        private void UpdateHarvestArming()
        {
            float hp = HealthFrac();
            if (hp < HarvestArmThreshold)         _harvestArmed = true;
            else if (hp > HarvestDisarmThreshold) _harvestArmed = false;
        }

        private void TryDiveBombSlam()
        {
            if (_inSpecial) return;
            if (HealthFrac() >= DiveHpThreshold) return;   // unlocks below 80% HP
            if (Time.time < _nextDiveTime) return;
            var target = Player.m_localPlayer;
            if (target == null || target.IsDead()) return;
            float dist = Vector3.Distance(transform.position, target.transform.position);
            if (dist > DiveRange) return;

            _nextDiveTime = Time.time + (_raging ? DiveCooldown * 0.6f : DiveCooldown);
            StartCoroutine(DiveBombSlamRoutine(target.transform.position));
        }

        /// <summary>
        /// Casts a tell, takes off, climbs above the player's snapshotted
        /// position, then dives straight down onto it. The slam VFX fires on
        /// impact with the ground/target — not mid-air.
        /// </summary>
        private IEnumerator DiveBombSlamRoutine(Vector3 targetPos)
        {
            _inSpecial = true;
            if (_monsterAI != null) _monsterAI.enabled = false;

            // Cast tell before the ability begins.
            SpawnAbilityTell();
            yield return new WaitForSeconds(AbilityTellDelay);

            _character.TakeOff();

            Vector3 skyPos = targetPos + Vector3.up * DiveSkyHeight;

            // Ascend toward the sky point above the target (faint contrail only).
            Vector3 startPos = transform.position;
            float t = 0f;
            while (t < DiveAscend)
            {
                float f = t / DiveAscend;
                transform.position = Vector3.Lerp(startPos, skyPos, f);
                FxLibrary.TrySpawn("fx_valkyrie_flapwing", transform.position);
                t += Time.deltaTime;
                yield return null;
            }
            transform.position = skyPos;
            transform.rotation = Quaternion.LookRotation(Vector3.down);

            // Telegraph the strike point on the ground.
            FxLibrary.TrySpawn("fx_redlightning_burst", targetPos);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f;
                var off = Quaternion.Euler(0f, a, 0f) * Vector3.forward * 1.2f;
                FxLibrary.TrySpawn("vfx_HitSparks", targetPos + off);
            }
            yield return new WaitForSeconds(0.3f);

            // Dive straight down onto the target (no slam VFX yet — that fires on impact).
            t = 0f;
            while (t < DiveDescend)
            {
                float f = t / DiveDescend;
                transform.position = Vector3.Lerp(skyPos, targetPos, f);
                t += Time.deltaTime;
                yield return null;
            }
            transform.position = targetPos;

            // Impact / slam — this is where the Dive Bomb VFX triggers.
            FxLibrary.TrySpawn("fx_fallenvalkyrie_attack_claw", targetPos);
            FxLibrary.TrySpawn("fx_himminafl_aoe",             targetPos);
            FxLibrary.TrySpawn("vfx_corpse_destruction_small", targetPos);
            FxLibrary.TrySpawnTimed("vfx_meteor_explosion",    targetPos, 3f);

            float sqr = DiveRadius * DiveRadius;
            var all = Character.GetAllCharacters();
            for (int i = 0; i < all.Count; i++)
            {
                if (!(all[i] is Player p) || p.IsDead()) continue;
                Vector3 toward = p.transform.position - targetPos;
                if (toward.sqrMagnitude > sqr) continue;

                var hit = new HitData();
                hit.m_pushForce = DivePushForce;
                hit.m_point     = targetPos;
                hit.m_dir       = (toward.sqrMagnitude > 0.01f ? toward.normalized : Vector3.up);
                hit.m_hitType   = HitData.HitType.EnemyHit;
                hit.m_blockable = true;
                hit.m_dodgeable = true;
                hit.SetAttacker(_character);
                p.Damage(hit);
            }

            _character.Land();
            if (_monsterAI != null) _monsterAI.enabled = true;
            _inSpecial = false;
        }

        private void TryWindGustKnockback()
        {
            if (_inSpecial) return;
            if (Time.time < _nextGustTime) return;
            var target = Player.m_localPlayer;
            if (target == null || target.IsDead()) return;
            if ((target.transform.position - transform.position).sqrMagnitude >
                GustTriggerRange * GustTriggerRange) return;

            _nextGustTime = Time.time + (_raging ? GustCooldown * 0.6f : GustCooldown);
            StartCoroutine(WindGustKnockbackRoutine());
        }

        /// <summary>
        /// Casts a tell, then a short spin-up (the Valkyrie's wing-spin pose)
        /// followed by a radial gust of wind that pushes every nearby player
        /// back — a positioning tool that fits Ashlands' cliff terrain.
        /// </summary>
        private IEnumerator WindGustKnockbackRoutine()
        {
            _inSpecial = true;

            // Cast tell before the ability begins.
            SpawnAbilityTell();
            yield return new WaitForSeconds(AbilityTellDelay);

            var center = transform.position;

            // Spin-up telegraph — the Valkyrie's own wing-spin charge FX plus a
            // ring of sparks tightening inward.
            FxLibrary.TrySpawn("fx_fallenvalkyrie_attack_spin_charge", center);
            FxLibrary.TrySpawn("fx_valkyrie_flapwing", center + Vector3.up * 1f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f;
                var off = Quaternion.Euler(0f, a, 0f) * Vector3.forward * (2.5f - i * 0.1f);
                FxLibrary.TrySpawn("vfx_HitSparks", center + off + Vector3.up * 1f);
            }
            FxLibrary.TrySpawn("fx_himminafl_aoe", center);
            yield return new WaitForSeconds(GustTelegraph);

            // Gust release — the Valkyrie's wing-spin release FX.
            FxLibrary.TrySpawn("fx_fallenvalkyrie_attack_spin_release", center + Vector3.up * 1f);
            FxLibrary.TrySpawn("fx_redlightning_burst", center + Vector3.up * 1f);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f;
                var off = Quaternion.Euler(0f, a, 0f) * Vector3.forward * GustRadius;
                FxLibrary.TrySpawn("vfx_HitSparks", center + off + Vector3.up * 0.5f);
            }

            float sqr = GustRadius * GustRadius;
            var all = Character.GetAllCharacters();
            for (int i = 0; i < all.Count; i++)
            {
                if (!(all[i] is Player p) || p.IsDead()) continue;
                Vector3 toward = p.transform.position - center;
                if (toward.sqrMagnitude > sqr) continue;

                var hit = new HitData();
                hit.m_pushForce = GustPushForce;
                hit.m_point     = p.transform.position;
                hit.m_dir       = (toward.sqrMagnitude > 0.01f ? toward.normalized : Vector3.forward);
                hit.m_hitType   = HitData.HitType.EnemyHit;
                hit.m_blockable = true;
                hit.m_dodgeable = true;
                hit.SetAttacker(_character);
                p.Damage(hit);
            }

            _inSpecial = false;
        }

        private void TrySoulHarvest()
        {
            if (_inSpecial) return;
            if (!_harvestArmed) return;                     // <30% HP to arm, >50% disarms
            if (Time.time < _nextHarvestTime) return;
            var target = Player.m_localPlayer;
            if (target == null || target.IsDead()) return;
            if ((target.transform.position - transform.position).sqrMagnitude >
                HarvestTriggerRange * HarvestTriggerRange) return;

            _nextHarvestTime = Time.time + (_raging ? HarvestCooldown * 0.6f : HarvestCooldown);
            StartCoroutine(SoulHarvestRoutine());
        }

        /// <summary>
        /// Casts a tell, takes off, and holds station in the air for
        /// HarvestDuration, tethering to every player within HarvestRadius — a
        /// chain of light FX strung along the line between the Valkyrie and each
        /// tethered player — and draining spirit health from them each tick to
        /// heal herself.
        /// </summary>
        private IEnumerator SoulHarvestRoutine()
        {
            _inSpecial = true;
            if (_monsterAI != null) _monsterAI.enabled = false;

            // Cast tell before the ability begins.
            SpawnAbilityTell();
            yield return new WaitForSeconds(AbilityTellDelay);

            _character.TakeOff();

            FxLibrary.TrySpawn("fx_fallenvalkyrie_attack_spit_charge", transform.position);
            FxLibrary.TrySpawn("fx_himminafl_aoe", transform.position);

            float elapsed = 0f;
            float nextTick = 0f;
            float nextFlap = 0f;
            while (elapsed < HarvestDuration)
            {
                _character.SetMoveDir(Vector3.up * 0.15f);

                // Keep her visibly airborne with periodic wingflaps.
                if (elapsed >= nextFlap)
                {
                    nextFlap = elapsed + 0.8f;
                    FxLibrary.TrySpawn("fx_valkyrie_flapwing", transform.position);
                }

                if (elapsed >= nextTick)
                {
                    nextTick = elapsed + HarvestTickInterval;
                    DrainTick();
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            _character.Land();
            if (_monsterAI != null) _monsterAI.enabled = true;
            _inSpecial = false;
        }

        private void DrainTick()
        {
            var center = transform.position;
            float sqr = HarvestRadius * HarvestRadius;
            var all = Character.GetAllCharacters();
            float totalDrained = 0f;

            for (int i = 0; i < all.Count; i++)
            {
                if (!(all[i] is Player p) || p.IsDead()) continue;
                Vector3 toward = p.transform.position - center;
                if (toward.sqrMagnitude > sqr) continue;

                var hit = new HitData();
                hit.m_damage.m_spirit = HarvestDrainPerTick;
                hit.m_point           = p.transform.position;
                hit.m_dir             = toward.normalized;
                hit.m_hitType         = HitData.HitType.PlayerHit;
                hit.SetAttacker(_character);
                p.Damage(hit);

                SpawnTether(center, p.transform.position + Vector3.up * 1f);
                totalDrained += HarvestDrainPerTick;
            }

            if (totalDrained > 0f) _character.Heal(totalDrained, true);
        }

        /// <summary>Approximates a beam by spawning a short run of spark FX along
        /// the line between the Valkyrie and a tethered player.</summary>
        private void SpawnTether(Vector3 from, Vector3 to)
        {
            // Anchor the beam at the Valkyrie's spit-projectile FX, then string
            // sparks along the line to the player so it reads as a soul tether.
            FxLibrary.TrySpawn("fx_fallenvalkyrie_attack_spit_projectile", from);
            const int Segments = 5;
            for (int i = 0; i <= Segments; i++)
            {
                float f = i / (float)Segments;
                Vector3 pos = Vector3.Lerp(from, to, f);
                FxLibrary.TrySpawn("vfx_HitSparks", pos);
            }
            FxLibrary.TrySpawn("fx_fallenvalkyrie_attack_spit_projectile_impact", to);
        }

        private void TickRageAura()
        {
            if (!_raging) return;
            if (Time.time < _nextRagePulse) return;
            _nextRagePulse = Time.time + 0.4f;
            var pos = transform.position + Vector3.up * 1.0f;
            FxLibrary.TrySpawn("vfx_HitSparks", pos);
            if (Random.value < 0.5f) FxLibrary.TrySpawn("fx_himminafl_aoe", pos + Vector3.up * 0.3f);
        }
    }
}
