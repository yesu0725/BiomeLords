using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Jotunn.Managers;
using BiomeLords.Util;

namespace BiomeLords.Phase1D
{
    /// <summary>
    /// Draugr Elite Lord abilities:
    ///   • Rotten Cleave — Lord pauses with the vanilla forsaken-power activation
    ///     VFX (fx_GP_Activation), then performs a real equipped-sword swing whose
    ///     animation hitbox carries the damage (slash, via the lord's profile).
    ///   • Undying Surge — at 40% HP (one-shot), Lord stops, plays boss pre-spawn
    ///     VFX, then instantly kills every creature in a very wide radius and heals
    ///     200 HP per kill. Fails (0 heal) if no creatures are nearby.
    ///   • Plague Cloud — at 60% HP (one-shot), spawns a stationary green
    ///     poison cloud at the Lord's position; ticks 5 poison/s within 5 m
    ///     for 25 s — controls the arena.
    ///   • Summon Draugr — every 60 s spawns 2 regular Draugr (max 3 nearby).
    ///   • Death Throes — at 25% HP, speed +50%, blood-red aura, melee
    ///     strikes apply Wet to the player.
    /// </summary>
    public class DraugrLordBrain : MonoBehaviour
    {
        private const float CleaveCooldown       = 30f;   // long gap so it never looks spammed
        private const float CleaveRange         =  5f;
        private const float WoundDuration       = 60f;
        private const float WoundTickDamage     =  5f;
        private const float WoundTickInterval   =  1f;
        private const float SurgeHpFraction     = 0.40f;
        private const float SurgeHealPerKill    = 500f;
        private const float SurgeAbsorbRadius   = 60f;
        private const float MinionCooldown      = 60f;
        private const int   MaxNearbyMinions    = 3;     // was 4
        private const int   MinionsPerSummon    = 2;     // also cap-clamped below
        private const float MinionDetectRadius  = 12f;
        private const float CloudHpFraction     = 0.60f;
        private const float DeathThroesHpFrac   = 0.25f;
        private const float DeathThroesSpeed    = 1.5f;
        private const float WetTickInterval     = 2f;
        private const float WetTouchRadius      = 3f;

        private const string MinionPrefab       = "Draugr";

        private Character _character;
        private Humanoid  _humanoid;
        private ZNetView  _nview;

        private float _baseSpeed, _baseRunSpeed;
        private bool  _cloudFired;
        private bool  _surgeFired;
        private bool  _surgeActive;
        private bool  _deathThroes;
        private float _nextCleaveTime;
        private float _nextMinionTime;
        private float _nextWetTouch;
        private float _nextDeathThroesPulse;

        private static GameObject _cachedMinion;
        private static StatusEffect _cachedWetSE;
        private static SE_DraugrWound _woundProto;

        /// <summary>InstanceIDs of Draugr Lords whose Rotten Cleave swing is mid-air.
        /// DraugrWoundPatch consults this so the Wounded bleed is applied only when
        /// the cleave's weapon hit actually lands (not on proximity).</summary>
        private static readonly HashSet<int> _cleaveWindowLords = new HashSet<int>();
        public static bool IsCleaving(int instanceId) => _cleaveWindowLords.Contains(instanceId);

        private void Awake()
        {
            _character = GetComponent<Character>();
            _humanoid  = GetComponent<Humanoid>();
            _nview     = GetComponent<ZNetView>();
            if (_character != null)
            {
                _baseSpeed    = _character.m_speed;
                _baseRunSpeed = _character.m_runSpeed;
            }
            _nextCleaveTime = Time.time + 8f;
            _nextMinionTime = Time.time + 15f;
        }

        private void Update()
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner()) return;
            if (_character == null || _character.IsDead()) return;

            TryPlagueCloud();
            TryUndyingSurge();
            TryDeathThroes();
            TryRottenCleave();
            TrySummonMinions();
            TickWetTouch();
            TickDeathThroesAura();
        }

        // ---- Abilities -----------------------------------------------------

        private void TryPlagueCloud()
        {
            if (_cloudFired) return;
            float frac = _character.GetHealth() / _character.GetMaxHealth();
            if (frac > CloudHpFraction) return;
            _cloudFired = true;

            // Dramatic activation burst — pure-particle VFX only (no ZNetView/Aoe).
            var cloudPos = transform.position + Vector3.up * 0.5f;
            FxLibrary.TrySpawnTimed("vfx_blob_attack", cloudPos, 3f);
            FxLibrary.TrySpawnTimed("vfx_blob_attack", cloudPos, 4f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f;
                var off = Quaternion.Euler(0f, a, 0f) * Vector3.forward * 1.6f;
                FxLibrary.TrySpawnTimed("vfx_blob_attack", cloudPos + off, 3f);
            }

            // Spawn the cloud as an independent GameObject so it can outlive
            // movement of the Lord — it stays where it was released.
            var cloudGO = new GameObject("BiomeLords_PlagueCloud");
            cloudGO.transform.position = transform.position;
            var cloud = cloudGO.AddComponent<PlagueCloud>();
            cloud.Attacker = _character;

            // Center message so the player understands the new hazard.
            var local = Player.m_localPlayer;
            if (local != null)
                local.Message(MessageHud.MessageType.Center, "$biomelords_plague_cloud");

            Jotunn.Logger.LogInfo("[BiomeLords] Draugr Lord released a plague cloud.");
        }

        private void TryDeathThroes()
        {
            if (_deathThroes) return;
            float frac = _character.GetHealth() / _character.GetMaxHealth();
            if (frac > DeathThroesHpFrac) return;
            _deathThroes = true;

            _character.m_speed    = _baseSpeed    * DeathThroesSpeed;
            _character.m_runSpeed = _baseRunSpeed * DeathThroesSpeed;

            var pos = transform.position + Vector3.up * 1f;
            FxLibrary.TrySpawnTimed("vfx_blob_attack", pos, 3f);
            FxLibrary.TrySpawn("vfx_corpse_destruction_small", pos);
            FxLibrary.TrySpawn("fx_redlightning_burst", pos);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f;
                var off = Quaternion.Euler(0f, a, 0f) * Vector3.forward * 1.4f;
                FxLibrary.TrySpawn("vfx_HitSparks", pos + off);
            }

            // Persistent visual change — turn the Lord's aura Light blood-red
            // and intensify it. Players see the change immediately.
            var auraT = transform.Find("BiomeLords_Aura");
            if (auraT != null)
            {
                var light = auraT.GetComponent<Light>();
                if (light != null)
                {
                    light.color     = new Color(1.0f, 0.15f, 0.10f);
                    light.intensity = 4.0f;
                    light.range     = 7.0f;
                }
            }
            Jotunn.Logger.LogInfo("[BiomeLords] Draugr Lord entered Death Throes.");
        }

        /// <summary>While in Death Throes, pulse mist + a red lightning ember
        /// at the Lord every ~0.6 s so the enraged state stays visible.</summary>
        private void TickDeathThroesAura()
        {
            if (!_deathThroes) return;
            if (Time.time < _nextDeathThroesPulse) return;
            _nextDeathThroesPulse = Time.time + 0.6f;

            var pos = transform.position + Vector3.up * 1.2f;
            if (Random.value < 0.5f) FxLibrary.TrySpawn("fx_redlightning_burst", pos);
        }

        private void TryRottenCleave()
        {
            if (_surgeActive) return;
            if (Time.time < _nextCleaveTime) return;
            var target = Player.m_localPlayer;
            if (target == null || target.IsDead()) return;
            if (Vector3.Distance(transform.position, target.transform.position) > CleaveRange) return;

            _nextCleaveTime = Time.time + CleaveCooldown;
            StartCoroutine(CleaveRoutine());
        }

        private IEnumerator CleaveRoutine()
        {
            // Lord stops — 1s cast pause so the player can react.
            _character.m_speed    = 0f;
            _character.m_runSpeed = 0f;

            // Vanilla forsaken-power activation VFX — the same green energy column
            // the player sees when activating a guardian power (fx_GP_Activation).
            var castPos = transform.position + Vector3.up * 1f;
            FxLibrary.TrySpawn("fx_GP_Activation", castPos);

            // Telegraph window so the player can react before the swing lands.
            yield return new WaitForSeconds(1.0f);

            // Real weapon attack — damage is carried by the equipped sword's swing
            // (animation-driven hitbox), not a separate invisible AoE. The
            // LordDamageBoostPatch rewrites that hit to the lord's profile.
            //
            // Open the cleave window so DraugrWoundPatch applies "Wounded" only if
            // this swing's hit actually lands on the player (strictly on landed
            // damage — a dodge avoids the bleed entirely).
            int cleaveId = _character.GetInstanceID();
            _cleaveWindowLords.Add(cleaveId);

            var target = Player.m_localPlayer;
            if (_humanoid != null && target != null && !target.IsDead())
            {
                // Face the target so the swing connects, then start the attack.
                var look = target.transform.position - transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(look.normalized);

                _humanoid.StartAttack(target, false);
            }

            // Dramatic slice VFX at the sword tip, timed to the swing.
            yield return new WaitForSeconds(0.4f);
            var slicePos = transform.position + transform.forward * 1.5f + Vector3.up * 1.2f;
            FxLibrary.TrySpawnFirst(
                new[] { "vfx_cut", "vfx_swing_sledge", "vfx_player_hit_blood", "vfx_HitSparks" },
                slicePos);

            // Follow-through — keep the window open until the swing's hit has resolved.
            yield return new WaitForSeconds(0.4f);
            _cleaveWindowLords.Remove(cleaveId);

            float cleaveSpeedMult = _deathThroes ? DeathThroesSpeed : 1f;
            _character.m_speed    = _baseSpeed    * cleaveSpeedMult;
            _character.m_runSpeed = _baseRunSpeed * cleaveSpeedMult;
        }

        /// <summary>Applies the "Wounded" bleed debuff to a player. The prototype
        /// is built lazily and applied directly via SEMan (no ObjectDB needed).
        /// Called by DraugrWoundPatch when a cleave swing lands.</summary>
        public static void ApplyWound(Player p)
        {
            if (_woundProto == null)
            {
                _woundProto = ScriptableObject.CreateInstance<SE_DraugrWound>();
                _woundProto.name          = "SE_DraugrWound";
                _woundProto.m_name        = "$se_draugrwound";
                _woundProto.m_ttl         = WoundDuration;
                _woundProto.DamagePerTick = WoundTickDamage;
                _woundProto.TickInterval  = WoundTickInterval;

                // Borrow a thematic vanilla icon so it renders in the HUD.
                var db = ObjectDB.instance;
                if (db != null)
                {
                    var donor = db.GetStatusEffect("Poison".GetStableHashCode());
                    if (donor != null && donor.m_icon != null)
                        _woundProto.m_icon = donor.m_icon;
                    else if (db.m_StatusEffects != null)
                        foreach (var s in db.m_StatusEffects)
                            if (s != null && s.m_icon != null) { _woundProto.m_icon = s.m_icon; break; }
                }
            }

            p.GetSEMan().AddStatusEffect(_woundProto, resetTime: true);
            FxLibrary.TrySpawnFirst(
                new[] { "vfx_BloodHit", "vfx_player_hit_blood", "vfx_HitSparks" },
                p.transform.position + Vector3.up * 1.0f);
        }

        private void TryUndyingSurge()
        {
            if (_surgeFired) return;
            float frac = _character.GetHealth() / _character.GetMaxHealth();
            if (frac > SurgeHpFraction) return;
            _surgeFired  = true;
            _surgeActive = true;
            StartCoroutine(UndyingSurgeRoutine());
        }

        private IEnumerator UndyingSurgeRoutine()
        {
            // Lord stops — casting stance.
            _character.m_speed    = 0f;
            _character.m_runSpeed = 0f;

            var pos = transform.position + Vector3.up * 1f;

            // Healing VFX — the lord visibly mends itself as it drains the dead.
            FxLibrary.TrySpawn("vfx_HealthUpgrade", pos);
            FxLibrary.TrySpawnTimed("vfx_Potion_health_medium", pos, 4f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f;
                var off = Quaternion.Euler(0f, a, 0f) * Vector3.forward * 1.4f;
                FxLibrary.TrySpawnTimed("vfx_Potion_health_medium", pos + off, 3f);
            }

            Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "$biomelords_undying_surge");
            Jotunn.Logger.LogInfo("[BiomeLords] Draugr Lord triggered Undying Surge.");

            // Dramatic pause — let the pre-spawn VFX build up.
            yield return new WaitForSeconds(1.5f);

            // Drain all non-player, non-lord creatures within the absorption radius.
            int   killed = 0;
            float sqr    = SurgeAbsorbRadius * SurgeAbsorbRadius;
            var   center = transform.position;
            var   all    = Character.GetAllCharacters();
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || c == _character || c.IsDead() || c.IsPlayer()) continue;
                if ((c.transform.position - center).sqrMagnitude > sqr) continue;

                // Life-drain spark at victim position before the kill.
                FxLibrary.TrySpawn("vfx_HitSparks", c.transform.position + Vector3.up * 1f);

                // Instant kill, not a damage tick. Deliberately leave the attacker
                // NULL so LordDamageBoostPatch's `attacker == null` early-return
                // skips it — otherwise the patch would clamp this down to the
                // lord's profile (80 slash + 50 poison) and merely wound them.
                // Massive multi-type damage guarantees death past any single
                // resistance/immunity (draugr, blobs, etc.).
                float lethal = c.GetMaxHealth() * 100f + 10000f;
                var killHit = new HitData();
                killHit.m_damage.m_blunt  = lethal;
                killHit.m_damage.m_slash  = lethal;
                killHit.m_damage.m_pierce = lethal;
                killHit.m_damage.m_fire   = lethal;
                killHit.m_hitType         = HitData.HitType.EnemyHit;
                killHit.m_point           = c.transform.position;
                c.Damage(killHit);
                killed++;
            }

            if (killed > 0)
            {
                _character.Heal(killed * SurgeHealPerKill);
                Jotunn.Logger.LogInfo($"[BiomeLords] Draugr Lord Undying Surge drained {killed} creature(s).");
            }
            else
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "$biomelords_undying_surge_fail");
                Jotunn.Logger.LogInfo("[BiomeLords] Draugr Lord Undying Surge found no prey — failed.");
            }

            // Hold stationary while the VFX finishes.
            yield return new WaitForSeconds(1.5f);

            float surgeSpeedMult  = _deathThroes ? DeathThroesSpeed : 1f;
            _character.m_speed    = _baseSpeed    * surgeSpeedMult;
            _character.m_runSpeed = _baseRunSpeed * surgeSpeedMult;
            _surgeActive = false;
        }

        private void TrySummonMinions()
        {
            if (_surgeActive) return;
            if (Time.time < _nextMinionTime) return;
            int alive = CountNearbyMinions();
            if (alive >= MaxNearbyMinions)
            {
                _nextMinionTime = Time.time + 15f;
                return;
            }
            DoSpawnMinions();
            _nextMinionTime = Time.time + MinionCooldown;
        }

        private void DoSpawnMinions()
        {
            EnsurePrefabs();
            if (_cachedMinion == null) return;

            int alive  = CountNearbyMinions();
            int budget = System.Math.Min(MinionsPerSummon, MaxNearbyMinions - alive);
            for (int i = 0; i < budget; i++)
            {
                float angle = i * (360f / System.Math.Max(1, budget)) + Random.Range(-25f, 25f);
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 pos = transform.position + dir * 3.5f + Vector3.up * 0.2f;
                Instantiate(_cachedMinion, pos, Quaternion.LookRotation(-dir));
                FxLibrary.TrySpawn("vfx_spawn", pos);
            }
        }

        /// <summary>In Death Throes, any player within melee range gets Wet
        /// applied periodically — simulates the Lord's grasping strikes leaving
        /// you soaked with bog water.</summary>
        private void TickWetTouch()
        {
            if (!_deathThroes) return;
            if (Time.time < _nextWetTouch) return;
            _nextWetTouch = Time.time + WetTickInterval;

            var p = Player.m_localPlayer;
            if (p == null || p.IsDead()) return;
            if ((p.transform.position - transform.position).sqrMagnitude > WetTouchRadius * WetTouchRadius) return;

            if (_cachedWetSE == null)
                _cachedWetSE = ObjectDB.instance?.GetStatusEffect("Wet".GetStableHashCode());
            if (_cachedWetSE != null)
                p.GetSEMan().AddStatusEffect(_cachedWetSE, resetTime: true);
        }

        // ---- Helpers -------------------------------------------------------

        private int CountNearbyMinions()
        {
            int count = 0;
            var center = transform.position;
            float sqr = MinionDetectRadius * MinionDetectRadius;
            var all = Character.GetAllCharacters();
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || c == _character || c.IsDead()) continue;
                if ((c.transform.position - center).sqrMagnitude > sqr) continue;
                if (c.gameObject.name.StartsWith("Draugr")) count++;
            }
            return count;
        }

        private static void EnsurePrefabs()
        {
            if (_cachedMinion == null) _cachedMinion = PrefabManager.Instance.GetPrefab(MinionPrefab);
        }
    }
}
