using UnityEngine;
using Jotunn.Managers;
using BiomeLords.Util;
using BiomeLords.Phase1C;

namespace BiomeLords.Phase1D
{
    /// <summary>
    /// Drives the marker-based Forsaken Powers:
    ///
    ///   Tide's Grace (GP_TidesGrace) — the Neck Lord's mastery of water
    ///     • While swimming → stamina is restored instead of drained.
    ///     • While Wet      → all melee attacks deal +50% (Character_Damage_TidesGraceMelee).
    ///     • The Wet status's debuffs no longer affect you (NeckWetImmunityPatch).
    ///
    /// </summary>
    public static class PowerEffectsService
    {
        private static int _tidesHash;
        private static int _howlHash;

        // Howl of the Pack: only spawn a phantom wolf on the *transition* from
        // marker-absent → marker-present, not every tick.
        private static StatusEffect _lastSeenHowlMarker;

        // Tide's Grace: stamina restored per second while swimming, and the
        // throttle for the ambient water-ripple FX.
        private const  float SwimStaminaPerSecond = 25f;
        private static float _nextRipple;

        public static void Tick()
        {
            EnsureHashes();
            var p = Player.m_localPlayer;
            if (p == null || p.IsDead()) return;
            var seman = p.GetSEMan();
            if (seman == null) return;

            TickTidesGrace(p, seman);
            TickHowlOfThePack(p, seman);

            HiveSightService.Tick();
            ValkyrieRallyService.Tick();
        }

        // ---- Fenring Howl of the Pack --------------------------------------

        private const float HowlPackRange         = 30f;
        private const float HowlAuraLifetime      = 60f;     // base — 1 min
        private const float HowlAuraSynergyLife   = 120f;    // with Pack Whisperer — 2 min

        private static int _packBlessingHash;

        private static void TickHowlOfThePack(Player p, SEMan seman)
        {
            var marker = seman.GetStatusEffect(_howlHash);
            if (marker == null)
            {
                if (_lastSeenHowlMarker != null) _lastSeenHowlMarker = null;
                return;
            }
            // New activation? Run the one-shot pack rally exactly once per F-press.
            if (marker == _lastSeenHowlMarker) return;
            _lastSeenHowlMarker = marker;

            // Snapshot blessing presence at activation — determines synergy duration.
            if (_packBlessingHash == 0)
                _packBlessingHash = Phase1C.StatusEffectFactory.FenringLordSpiritSE.GetStableHashCode();
            bool synergy = seman.HaveStatusEffect(_packBlessingHash);
            float lifetime = synergy ? HowlAuraSynergyLife : HowlAuraLifetime;

            // 1) Heal + visually mark every tamed Character within range.
            EmpowerNearbyTames(p, lifetime);

            // 2) Summon the phantom pack. In synergy with Pack Whisperer the
            //    wolves are invulnerable, and Blood Magic mastery swells the
            //    pack: 50+ → 2 wolves, 100 → 3 (otherwise a single wolf).
            int wolfCount = 1;
            if (synergy)
            {
                float blood = p.GetSkills() != null
                    ? p.GetSkills().GetSkillLevel(Skills.SkillType.BloodMagic) : 0f;
                if (blood >= 100f)     wolfCount = 3;
                else if (blood >= 50f) wolfCount = 2;
            }
            SpawnPhantomPack(p, lifetime, wolfCount, invulnerable: synergy);

            string msg = synergy
                ? "$gp_howlofthepack_phantom_synergy"
                : "$gp_howlofthepack_phantom";
            p.Message(MessageHud.MessageType.Center, msg);
            Jotunn.Logger.LogInfo(
                $"[BiomeLords] Howl of the Pack: pack rallied (synergy={synergy}, " +
                $"wolves={wolfCount}, invuln={synergy}, lifetime={lifetime:F0}s).");
        }

        /// <summary>
        /// Find every tamed Character within HowlPackRange of the player,
        /// fully restore its HP, and attach a HowlAura visual marker so the
        /// player can see exactly which tames the rally reached.
        /// </summary>
        private static void EmpowerNearbyTames(Player p, float lifetime)
        {
            var center = p.transform.position;
            float sqr  = HowlPackRange * HowlPackRange;
            var all = Character.GetAllCharacters();
            int healed = 0, marked = 0;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || c.IsDead()) continue;
                if (!c.IsTamed()) continue;
                if (c.IsPlayer()) continue;
                if ((c.transform.position - center).sqrMagnitude > sqr) continue;

                // Heal to full — Character.Heal takes amount, second arg shows world text.
                float missing = c.GetMaxHealth() - c.GetHealth();
                if (missing > 0f) { c.Heal(missing, true); healed++; }

                // Attach aura if not already present.
                if (c.GetComponent<HowlAura>() == null)
                {
                    var aura = c.gameObject.AddComponent<HowlAura>();
                    aura.Lifetime = lifetime;
                    marked++;
                }
            }
            if (healed + marked > 0)
                Jotunn.Logger.LogInfo($"[BiomeLords] Howl of the Pack: healed {healed} tames, marked {marked}.");
        }

        private static void SpawnPhantomPack(Player p, float lifetime, int count, bool invulnerable)
        {
            var wolfPrefab = PrefabManager.Instance.GetPrefab("Wolf");
            if (wolfPrefab == null)
            {
                Jotunn.Logger.LogWarning("[BiomeLords] Howl of the Pack: Wolf prefab not found.");
                return;
            }

            for (int i = 0; i < count; i++)
                SpawnPhantomWolf(p, wolfPrefab, lifetime, invulnerable, count, i);
        }

        private static void SpawnPhantomWolf(Player p, GameObject wolfPrefab, float lifetime,
                                             bool invulnerable, int count, int index)
        {
            // Fan the pack out around the player's right/forward so multiple
            // wolves don't stack on one spot.
            float spread = count > 1 ? (index - (count - 1) * 0.5f) : 0f;
            Vector3 pos = p.transform.position
                        + p.transform.right   * (1.5f + spread)
                        + p.transform.forward * 0.5f
                        + Vector3.up * 0.3f;

            var go = Object.Instantiate(wolfPrefab, pos, Quaternion.LookRotation(p.transform.forward));
            go.name = "PhantomWolf";

            // Mark the wolf non-persistent so it is never written to the save —
            // it vanishes when the world unloads (e.g. the player logs out) and
            // is never restored as a permanent tamed wolf.
            var nview = go.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid())
                nview.GetZDO().Persistent = false;

            // Tame it so it fights for the player.
            var ch = go.GetComponent<Character>();
            if (ch != null)
            {
                ch.SetTamed(true);
                ch.m_name = "$enemy_phantomwolf";
            }
            var humanoid = go.GetComponent<Humanoid>();
            if (humanoid != null) humanoid.m_name = "$enemy_phantomwolf";

            // Keep the Tameable component — a tamed Character without it makes
            // vanilla spam "tamed but missing tameable…" (Character.RaiseSkill)
            // and NREs in Procreation.Procreate. We just make it non-commandable
            // so the phantom stays faceless (no follow/stay/rename menu).
            var tame = go.GetComponent<Tameable>();
            if (tame != null) tame.m_commandable = false;

            // Phantom wolves are transient — they must never breed. Removing
            // Procreation also avoids its Procreate() tick entirely.
            var procreation = go.GetComponent<Procreation>();
            if (procreation != null) Object.Destroy(procreation);

            // Default to following the local player.
            var monsterAi = go.GetComponent<MonsterAI>();
            if (monsterAi != null) monsterAi.SetFollowTarget(p.gameObject);

            // Visually distinct: ghostly violet body tint + a larger, brighter
            // HowlAura than the empowered tames get.
            ApplyPhantomTint(go);
            var distinct = go.AddComponent<HowlAura>();
            distinct.Lifetime      = lifetime;
            distinct.AuraColor     = new Color(0.55f, 0.35f, 1.0f);  // violet ghost-light
            distinct.AuraIntensity = 4.0f;
            distinct.AuraRange     = 4.0f;

            // Auto-despawn aligned with the rally lifetime (60 s or 10 min synergy).
            // In synergy the wolf is invulnerable (enforced by PhantomWolfInvulnPatch).
            var phantom = go.AddComponent<PhantomWolf>();
            phantom.Lifetime    = lifetime;
            phantom.Invulnerable = invulnerable;

            // No "fx_summon_start" — that's the green summon burst.
            FxLibrary.TrySpawn("vfx_HitSparks",          pos + Vector3.up * 0.5f);
            FxLibrary.TrySpawn("fx_redlightning_burst",  pos);
        }

        private static void ApplyPhantomTint(GameObject go)
        {
            var tint = new Color(0.55f, 0.35f, 1.0f, 1f);   // ghostly violet
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var srcs = r.sharedMaterials;
                if (srcs == null || srcs.Length == 0) continue;
                var copies = new Material[srcs.Length];
                for (int i = 0; i < srcs.Length; i++)
                {
                    if (srcs[i] == null) continue;
                    var m = new Material(srcs[i]);
                    if (m.HasProperty("_Color"))         m.color = tint;
                    if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", tint * 0.5f);
                    copies[i] = m;
                }
                r.sharedMaterials = copies;
            }
        }

        // ---- Neck Tide's Grace ---------------------------------------------

        private static void TickTidesGrace(Player p, SEMan seman)
        {
            if (!seman.HaveStatusEffect(_tidesHash)) return;

            // Water mastery: swimming restores stamina instead of draining it.
            // (The +50% wet melee bonus and Wet-debuff immunity are handled by
            // Character_Damage_TidesGraceMelee and NeckWetImmunityPatch.)
            if (p.IsSwimming())
            {
                p.AddStamina(SwimStaminaPerSecond * Time.deltaTime);
                PulseRipple(p);
            }
        }

        private static void PulseRipple(Player p)
        {
            if (Time.time < _nextRipple) return;
            _nextRipple = Time.time + 0.8f;
            FxLibrary.TrySpawn("vfx_MeadSplash", p.transform.position);
        }

        // ---- Helpers -------------------------------------------------------

        private static void EnsureHashes()
        {
            if (_tidesHash == 0) _tidesHash = GuardianPowerFactory.NeckLordGP.GetStableHashCode();
            if (_howlHash  == 0) _howlHash  = GuardianPowerFactory.FenringLordGP.GetStableHashCode();
        }

        public static int TidesHash { get { EnsureHashes(); return _tidesHash; } }
    }
}
