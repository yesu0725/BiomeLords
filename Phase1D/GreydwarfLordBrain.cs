using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Jotunn.Managers;
using BiomeLords.Util;

namespace BiomeLords.Phase1D
{
    /// <summary>
    /// Per-instance behaviour for the Greydwarf Shaman Lord:
    ///   • Sapling Summon — periodic minion spawns.
    ///   • Root Spawn — spawns one gdking_root tentacle (capped at 1) that attacks the player.
    ///   • Healing Resonance — heals nearby greydwarves while above 30 % HP.
    ///   • Poison Nova — one-shot AoE when first dropping below 50 % HP.
    ///   • Frenzy at 30 % HP — speed, brighter tint, healing aura disabled.
    /// Only the network owner runs the logic (standard Valheim authority model).
    /// </summary>
    public class GreydwarfLordBrain : MonoBehaviour
    {
        private const float MinionCooldown      = 60f;   // was 30 — too spammy in a sustained fight
        private const float MinionDetectRadius  = 12f;
        private const int   MaxNearbyMinions    = 3;     // was 4
        private const int   MinionsPerSummon    = 2;     // was 3 — also cap-clamped below
        private const float RootCooldown        = 40f;
        private const float RootLifetime        = 30f;    // safety cap only; TentaRoot retracts/despawns via its own logic first (like the Elder)
        private const float RootMaxRange        = 25f;
        private const float RootMinRange        = 4f;     // melee-safe — Lord won't root-stomp adjacent player
        private const int   RootFrenzyCount     = 3;
        private const float RootRingRadius      = 5f;     // frenzy roots ring the player at this distance to surround them
        private const float RootRingJitter      = 18f;    // ± degrees of angular wobble so the ring isn't perfectly even
        private const float NovaPoisonBonus     = 18f;    // half of the vanilla poison-spit magnitude (36), added to every melee hit
        private const float HealCooldown        = 15f;    // was 6 — heal was firing too often
        private const float HealCastDelay       = 0.6f;   // wind-up so VFX/heal land partway through the cast animation
        private const float HealAmount          = 60f;
        private const float FrenzyHpFraction    = 0.30f;
        private const float NovaHpFraction      = 0.50f;
        private const float NovaRange           = 10f;
        private const float FrenzySpeedFactor   = 1.5f;

        private const string MinionPrefab       = "Greydwarf";
        // Candidate prefab names for the Elder's root tentacle — the first that
        // resolves in this game version wins. "TentaRoot" is the live id.
        private static readonly string[] RootPrefabCandidates =
            { "TentaRoot", "gd_king_root", "gdking_root", "Root" };

        private Character     _character;
        private ZNetView      _nview;
        private ZSyncAnimation _zanim;

        private float      _baseSpeed, _baseRunSpeed;
        private bool       _frenzied;
        private bool       _novaFired;
        private float      _nextMinionTime;
        private float      _nextRootTime;
        private float      _nextHealTime;
        private float      _nextFrenzyPulse;
        private readonly List<GameObject> _activeRoots = new List<GameObject>();

        private static GameObject _cachedMinion;
        private static GameObject _cachedRoot;

        // Genuine greydwarf-shaman heal cast, captured from the vanilla heal item
        // by CreatureFactory before that item is stripped (see ConfigureHeal).
        private static string       _healAnimTrigger;
        private static GameObject[] _healEffects;

        /// <summary>Called once at registration with the vanilla shaman heal item's
        /// attack-animation trigger and effect prefabs, so the brain can replay the
        /// real heal cast without keeping the (damage-bugged) item equipped.</summary>
        public static void ConfigureHeal(string animTrigger, GameObject[] effects)
        {
            _healAnimTrigger = animTrigger;
            _healEffects     = effects;
        }

        private void Awake()
        {
            _character = GetComponent<Character>();
            _nview     = GetComponent<ZNetView>();
            _zanim     = GetComponent<ZSyncAnimation>();
            if (_character != null)
            {
                _baseSpeed    = _character.m_speed;
                _baseRunSpeed = _character.m_runSpeed;
            }
            _nextMinionTime = Time.time + 15f;
            _nextRootTime   = Time.time + 8f;
            _nextHealTime   = Time.time + 6f;
        }

        private void Update()
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner()) return;
            if (_character == null || _character.IsDead()) return;

            TryPoisonNova();
            TryFrenzy();
            TrySummonMinions();
            TryRootSpawn();
            TryHealingResonance();
            TickFrenzyAura();
        }

        // ---- Abilities -----------------------------------------------------

        private void TryPoisonNova()
        {
            if (_novaFired) return;
            float frac = _character.GetHealth() / _character.GetMaxHealth();
            if (frac > NovaHpFraction) return;
            _novaFired = true;

            var pos = transform.position;
            // Visuals auto-destroy after 4s — vanilla vfx_blob_attack doesn't
            // self-destruct cleanly, so we wrap with TrySpawnTimed.
            FxLibrary.TrySpawnTimed("vfx_blob_attack",     pos, 4f);
            FxLibrary.TrySpawnTimed("fx_gdking_rootspawn", pos, 4f);

            // Permanently add poison to every melee swing for the rest of the
            // fight by mutating the per-instance profile the damage patch reads.
            // Fall back to the static profile if the registry entry is missing
            // (debug spawn / reload before SummonService ran).
            if (!LordProfileRegistry.TryGet(_character, out var profile))
                LordAttackProfile.TryGet("greydwarf_lord", out profile);
            profile.Poison += NovaPoisonBonus;
            LordProfileRegistry.Set(_character, profile);

            var local = Player.m_localPlayer;
            if (local != null && !local.IsDead()
                && (local.transform.position - pos).sqrMagnitude < NovaRange * NovaRange)
            {
                local.Message(MessageHud.MessageType.Center, "The Shaman seethes.");
            }
        }

        private void TryFrenzy()
        {
            if (_frenzied) return;
            float frac = _character.GetHealth() / _character.GetMaxHealth();
            if (frac > FrenzyHpFraction) return;
            _frenzied = true;

            _character.m_speed    = _baseSpeed    * FrenzySpeedFactor;
            _character.m_runSpeed = _baseRunSpeed * FrenzySpeedFactor;

            // Lime saturation flash.
            var tint = new Color(0.45f, 1.0f, 0.20f, 1f);
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.materials)
                {
                    if (m == null) continue;
                    if (m.HasProperty("_Color"))         m.color = tint;
                    if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", tint * 0.9f);
                }

            // One-shot frenzy burst.
            var pos = transform.position + Vector3.up * 1f;
            FxLibrary.TrySpawn("vfx_gdking_stomp",            pos);
            FxLibrary.TrySpawn("vfx_corpse_destruction_small", pos);
            FxLibrary.TrySpawn("fx_redlightning_burst",        pos);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f;
                var off = Quaternion.Euler(0f, a, 0f) * Vector3.forward * 1.2f;
                FxLibrary.TrySpawn("vfx_HitSparks", pos + off);
            }
            Jotunn.Logger.LogInfo("[BiomeLords] Greydwarf Shaman Lord entered frenzy.");
        }

        private void TrySummonMinions()
        {
            if (Time.time < _nextMinionTime) return;
            int alive = CountNearbyMinions();
            if (alive >= MaxNearbyMinions)
            {
                // Re-check sooner than full cooldown, but not every frame.
                _nextMinionTime = Time.time + 15f;
                return;
            }
            EnsurePrefabs();
            if (_cachedMinion == null) return;

            // Spawn only as many as the cap allows — never overshoot.
            int budget = System.Math.Min(MinionsPerSummon, MaxNearbyMinions - alive);
            for (int i = 0; i < budget; i++)
            {
                float angle = i * (360f / System.Math.Max(1, budget)) + Random.Range(-25f, 25f);
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 pos = transform.position + dir * 3.5f + Vector3.up * 0.2f;
                Instantiate(_cachedMinion, pos, Quaternion.LookRotation(-dir));
                FxLibrary.TrySpawn("vfx_spawn", pos);
            }
            _nextMinionTime = Time.time + MinionCooldown;
        }

        private void TryRootSpawn()
        {
            if (Time.time < _nextRootTime) return;

            // Normal mode: only one root at a time.
            if (!_frenzied)
            {
                PruneActiveRoots();
                if (_activeRoots.Count > 0) return;
            }

            var target = Player.m_localPlayer;
            if (target == null || target.IsDead()) return;
            float dist = Vector3.Distance(transform.position, target.transform.position);
            if (dist < RootMinRange || dist > RootMaxRange) return;

            _nextRootTime = Time.time + RootCooldown;
            StartCoroutine(RootSpawnRoutine(target.transform.position, _frenzied));
        }

        private IEnumerator RootSpawnRoutine(Vector3 targetPos, bool frenzy)
        {
            // Telegraph at the target centre.
            FxLibrary.TrySpawn("fx_gdking_rootspawn", targetPos);
            yield return new WaitForSeconds(0.6f);

            EnsurePrefabs();
            if (_cachedRoot == null) yield break;

            int count = frenzy ? RootFrenzyCount : 1;
            // Random starting angle so the surrounding ring isn't oriented the same way each time.
            float startAngle = Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                Vector3 spawnPos;
                if (frenzy)
                {
                    // Evenly distribute the roots in a ring around the player so they
                    // surround them, with a little angular jitter for variety.
                    float angle = startAngle + i * (360f / count) + Random.Range(-RootRingJitter, RootRingJitter);
                    Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                    spawnPos = targetPos + dir * RootRingRadius;
                }
                else
                {
                    spawnPos = targetPos;
                }

                // Snap to terrain so the root emerges from the ground, not floating or buried.
                if (ZoneSystem.instance != null
                    && ZoneSystem.instance.GetGroundHeight(spawnPos, out float groundY))
                    spawnPos.y = groundY;

                FxLibrary.TrySpawn("vfx_gdking_stomp",             spawnPos);
                FxLibrary.TrySpawn("fx_greenroots_projectile_hit", spawnPos);

                var root = Instantiate(_cachedRoot, spawnPos, Quaternion.identity);
                _activeRoots.Add(root);
                StartCoroutine(DespawnRootAfter(root, RootLifetime));
                Jotunn.Logger.LogInfo($"[BiomeLords] Greydwarf Lord spawned root '{_cachedRoot.name}' at {spawnPos}.");
            }
        }

        private void PruneActiveRoots()
        {
            _activeRoots.RemoveAll(r => r == null || !r.activeInHierarchy);
        }

        private IEnumerator DespawnRootAfter(GameObject root, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (root != null)
            {
                _activeRoots.Remove(root);
                Destroy(root);
            }
        }

        private void TryHealingResonance()
        {
            if (_frenzied) return;            // healing aura disabled when enraged
            if (Time.time < _nextHealTime) return;
            _nextHealTime = Time.time + HealCooldown;
            StartCoroutine(HealRoutine());
        }

        private IEnumerator HealRoutine()
        {
            // Play the genuine greydwarf-shaman heal cast animation first, then let
            // the VFX + heal land partway through it — mirroring the vanilla sequence.
            if (_zanim != null && !string.IsNullOrEmpty(_healAnimTrigger))
                _zanim.SetTrigger(_healAnimTrigger);

            yield return new WaitForSeconds(HealCastDelay);
            if (_character == null || _character.IsDead()) yield break;

            SpawnHealEffects(transform.position + Vector3.up * 1.2f);

            // Self-heal — skipped when already at full HP (no wasted heal).
            if (_character.GetHealth() < _character.GetMaxHealth())
                _character.Heal(HealAmount, true);

            // Heal nearby greydwarves with the same real effect on each — pure heal,
            // skipping any that are already topped off.
            var center = transform.position;
            float sqr = MinionDetectRadius * MinionDetectRadius;
            int healed = 0;
            var all = Character.GetAllCharacters();
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || c == _character || c.IsDead()) continue;
                if ((c.transform.position - center).sqrMagnitude > sqr) continue;
                if (!c.gameObject.name.StartsWith("Greydwarf")) continue;
                if (c.GetHealth() >= c.GetMaxHealth()) continue;

                c.Heal(HealAmount, true);   // showText: true so the heal is visible
                SpawnHealEffects(c.transform.position + Vector3.up * 0.5f);
                healed++;
            }

            if (BiomeLords.Config.LordConfig.DebugLogging != null
                && BiomeLords.Config.LordConfig.DebugLogging.Value)
                Jotunn.Logger.LogInfo(
                    $"[BiomeLords] Greydwarf Lord Healing Resonance: healed {healed} greydwarf(s) + self-check for {HealAmount}.");
        }

        /// <summary>Spawn the captured shaman-heal VFX at a position, stripped of any
        /// gameplay components (Aoe/Projectile) so it is purely cosmetic and can never
        /// deal damage. Instances are capped at 6s so nothing lingers.</summary>
        private void SpawnHealEffects(Vector3 pos)
        {
            if (_healEffects == null) return;
            foreach (var fx in _healEffects)
            {
                if (fx == null) continue;
                var inst = Instantiate(fx, pos, Quaternion.identity);
                foreach (var aoe in inst.GetComponentsInChildren<Aoe>(true))        Destroy(aoe);
                foreach (var pr  in inst.GetComponentsInChildren<Projectile>(true)) Destroy(pr);
                Destroy(inst, 6f);
            }
        }

        private void TickFrenzyAura()
        {
            if (!_frenzied) return;
            if (Time.time < _nextFrenzyPulse) return;
            _nextFrenzyPulse = Time.time + 0.4f;
            var pos = transform.position + Vector3.up * 1f;
            FxLibrary.TrySpawn("vfx_HitSparks", pos);
            if (Random.value < 0.4f) FxLibrary.TrySpawn("fx_redlightning_burst", pos + Vector3.up * 0.3f);
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
                if (c.gameObject.name.StartsWith("Greydwarf")) count++;
            }
            return count;
        }

        private static void EnsurePrefabs()
        {
            if (_cachedMinion == null) _cachedMinion = PrefabManager.Instance.GetPrefab(MinionPrefab);

            if (_cachedRoot == null)
            {
                foreach (var name in RootPrefabCandidates)
                {
                    var p = PrefabManager.Instance.GetPrefab(name);
                    if (p != null)
                    {
                        _cachedRoot = p;
                        Jotunn.Logger.LogInfo($"[BiomeLords] Greydwarf Lord root prefab resolved: '{name}'.");
                        break;
                    }
                }
                if (_cachedRoot == null)
                    Jotunn.Logger.LogWarning(
                        "[BiomeLords] Greydwarf Lord: no root prefab found among candidates " +
                        $"[{string.Join(", ", RootPrefabCandidates)}] — Root Spawn disabled.");
            }
        }
    }
}
