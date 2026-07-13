using System.Collections.Generic;
using UnityEngine;
using Jotunn.Managers;
using BiomeLords.Util;
using BiomeLords.Phase1C;

namespace BiomeLords.Phase1D
{
    /// <summary>
    /// Greydwarf Shaman Lord — Rootward (GP_Rootward).
    ///
    /// A one-shot self-defence burst. The instant the local player activates the
    /// power we:
    ///   • Heal the caster for 100 HP.
    ///   • Find the closest hostile creatures within range and erupt a protective
    ///     TentaRoot on top of each — up to 5. If fewer than 5 enemies are in
    ///     range, only that many roots spawn (one per enemy).
    /// The roots are spawned <b>tamed</b> so they fight for the player and never
    /// strike them; each carries a <see cref="WardRoot"/> MonoBehaviour that
    /// despawns it after a short life (the vanilla TentaRoot also retracts on its
    /// own once its attack sequence finishes).
    ///
    /// Driven from PowerEffectsService.Tick — fired exactly once on the marker's
    /// absent → present transition, mirroring Valkyrie's Rally.
    /// </summary>
    public static class RootwardService
    {
        private const float HealAmount      = 100f;
        private const int   MaxRoots        = 5;
        private const float EnemyScanRadius = 40f;   // hostiles this close are candidates
        private const float RootLifetime    = 30f;   // safety cap — TentaRoot usually retracts sooner

        // Candidate prefab names for the Elder's root tentacle — the first that
        // resolves in this game version wins. "TentaRoot" is the live id.
        private static readonly string[] RootPrefabCandidates =
            { "TentaRoot", "gd_king_root", "gdking_root", "Root" };

        private static int        _gpHash;
        private static GameObject _cachedRoot;

        // One-shot guard: only fire on the marker absent → present transition.
        private static StatusEffect _lastSeenMarker;

        public static void Tick()
        {
            if (_gpHash == 0) _gpHash = GuardianPowerFactory.GreydwarfLordGP.GetStableHashCode();

            var p = Player.m_localPlayer;
            if (p == null || p.IsDead()) { _lastSeenMarker = null; return; }
            var seman = p.GetSEMan();
            if (seman == null) { _lastSeenMarker = null; return; }

            var marker = seman.GetStatusEffect(_gpHash);
            if (marker == null) { _lastSeenMarker = null; return; }

            // Same marker instance we already handled? Wait for it to lapse.
            if (marker == _lastSeenMarker) return;
            _lastSeenMarker = marker;

            Activate(p);
        }

        private static void Activate(Player p)
        {
            // 1) Heal the caster — always, regardless of how many enemies are near.
            float missing = p.GetMaxHealth() - p.GetHealth();
            float heal = Mathf.Min(HealAmount, Mathf.Max(0f, missing));
            if (heal > 0f) p.Heal(heal, true);

            var castPos = p.transform.position + Vector3.up * 0.5f;
            FxLibrary.TrySpawn("fx_gdking_rootspawn", castPos);
            FxLibrary.TrySpawn("vfx_lootspawn",       castPos);

            // 2) Erupt a protective root on each of the closest enemies (≤ 5).
            var enemies = FindClosestEnemies(p, MaxRoots);
            EnsureRootPrefab();
            if (_cachedRoot == null)
            {
                Jotunn.Logger.LogWarning(
                    "[BiomeLords] Rootward: no TentaRoot prefab found among candidates " +
                    $"[{string.Join(", ", RootPrefabCandidates)}] — roots skipped.");
                p.Message(MessageHud.MessageType.Center, "$gp_rootward_activate");
                return;
            }

            int summoned = 0;
            foreach (var enemy in enemies)
            {
                if (SpawnWardRoot(enemy.transform.position)) summoned++;
            }

            p.Message(MessageHud.MessageType.Center, "$gp_rootward_activate");
            Jotunn.Logger.LogInfo(
                $"[BiomeLords] Rootward: healed {heal:F0} HP, erupted {summoned} protective root(s) " +
                $"on {enemies.Count} enemy target(s).");
        }

        /// <summary>The up-to-<paramref name="max"/> hostile creatures nearest the
        /// player, sorted closest-first. Tamed companions, other players and
        /// non-hostile wildlife are excluded (BaseAI.IsEnemy handles faction).</summary>
        private static List<Character> FindClosestEnemies(Player p, int max)
        {
            var center = p.transform.position;
            float sqr  = EnemyScanRadius * EnemyScanRadius;

            var candidates = new List<(Character c, float d)>();
            var all = Character.GetAllCharacters();
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || c.IsDead()) continue;
                if (c is Player) continue;
                if (c.IsTamed()) continue;
                if (!BaseAI.IsEnemy(p, c)) continue;   // only creatures actually hostile to the player

                float d = (c.transform.position - center).sqrMagnitude;
                if (d > sqr) continue;
                candidates.Add((c, d));
            }

            candidates.Sort((a, b) => a.d.CompareTo(b.d));

            var result = new List<Character>();
            for (int i = 0; i < candidates.Count && i < max; i++)
                result.Add(candidates[i].c);
            return result;
        }

        /// <summary>Erupt a single tamed TentaRoot at <paramref name="pos"/> (snapped
        /// to the ground) so it fights for the player. Returns true on success.</summary>
        private static bool SpawnWardRoot(Vector3 pos)
        {
            if (ZoneSystem.instance != null
                && ZoneSystem.instance.GetGroundHeight(pos, out float groundY))
                pos.y = groundY;

            FxLibrary.TrySpawn("vfx_gdking_stomp",             pos);
            FxLibrary.TrySpawn("fx_greenroots_projectile_hit", pos);

            var go = Object.Instantiate(_cachedRoot, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            go.name = "WardRoot";

            // Never persist to the save — these are transient summons.
            var nview = go.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid())
                nview.GetZDO().Persistent = false;

            // Tame it so its faction flips to Players: it now attacks the player's
            // enemies and can never damage the player.
            var ch = go.GetComponent<Character>();
            if (ch != null) ch.SetTamed(true);

            // Anchor it as a stationary guardian at its spawn spot (the no-arg
            // overload patrols around the AI's current transform position, which
            // is the enemy we just erupted it on).
            var ai = go.GetComponent<BaseAI>();
            if (ai != null) ai.SetPatrolPoint();

            var ward = go.AddComponent<WardRoot>();
            ward.Lifetime = RootLifetime;
            return true;
        }

        private static void EnsureRootPrefab()
        {
            if (_cachedRoot != null) return;
            foreach (var name in RootPrefabCandidates)
            {
                var pf = PrefabManager.Instance.GetPrefab(name);
                if (pf != null)
                {
                    _cachedRoot = pf;
                    Jotunn.Logger.LogInfo($"[BiomeLords] Rootward root prefab resolved: '{name}'.");
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Despawns its host TentaRoot after Lifetime seconds (or immediately if the
    /// local player is gone), with a small poof FX. Attached to each protective
    /// root summoned by the Rootward Forsaken Power.
    /// </summary>
    public class WardRoot : MonoBehaviour
    {
        public float Lifetime = 30f;

        private float    _spawnTime;
        private ZNetView _nview;

        private void Awake()
        {
            _spawnTime = Time.time;
            _nview     = GetComponent<ZNetView>();
        }

        private void Update()
        {
            if (Player.m_localPlayer != null && Time.time - _spawnTime < Lifetime) return;

            var pos = transform.position + Vector3.up * 0.5f;
            FxLibrary.TrySpawn("vfx_corpse_destruction_small", pos);
            Despawn();
        }

        private void Despawn()
        {
            if (_nview != null && _nview.IsValid() && ZNetScene.instance != null)
                ZNetScene.instance.Destroy(gameObject);
            else
                Object.Destroy(gameObject);
        }
    }
}
