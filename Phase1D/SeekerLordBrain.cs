using System.Collections;
using UnityEngine;
using Jotunn.Managers;
using BiomeLords.Util;

namespace BiomeLords.Phase1D
{
    /// <summary>
    /// Per-instance behaviour for the Seeker Lord (Mistlands, tier 6):
    ///   • Acid Spit — vision-gated like Gjall's spit (needs line of sight, not
    ///     just proximity). Takes off and hovers a wide standoff distance out
    ///     from the player (using the engine's built-in Character.TakeOff/Land
    ///     flight system), raining down acidic volleys — each a Queen-style
    ///     glowing/dripping spit projectile with an acid trail — but only while
    ///     facing the player, and with scattered (inaccurate) aim so the player
    ///     can sidestep. Lands when the airborne window ends.
    ///   • Burrow Ambush — every ~22s, digs in (renderers hidden), dashes
    ///     underground toward the player, and erupts beneath them with a
    ///     telegraphed knock-up burst. Ground-game gap-closer that contrasts
    ///     the airborne Acid Spit (the two never overlap — see _inSpecial).
    ///   • Brood Call — every 60s summon up to 2 SeekerBrood (cap 3 nearby, cap-clamped).
    ///   • Hive Frenzy at 30% HP — +50% speed, crimson aura, all cooldowns ×0.6.
    /// </summary>
    public class SeekerLordBrain : MonoBehaviour
    {
        private const float AcidSpitCooldown    = 24f;
        private const float SpitTriggerRange    = 22f;
        private const float SpitRadius          = 3.5f;
        private const float SpitHpThreshold     = 0.80f; // Acid Spit unlocks below 80% HP
        private const float SpitInaccuracy      = 4.0f;  // max horizontal scatter of the impact point
        private const float FacingDotThreshold  = 0.55f; // must face within ~57° of the player to fire

        private const float FlightAscendDuration = 1.3f;
        private const float FlightHoverDuration  = 7f;     // longer airborne time — multiple volleys
        private const float FlightDescendDuration = 1.1f;
        private const float VolleyInterval       = 2.2f;   // ~3 volleys across the hover window

        // Hover keeps a wide standoff gap from the player instead of closing in
        // to hover overhead — same kited-distance feel as Gjall's spit. The Lord
        // flies out to this range before it starts spitting.
        private const float HoverStandoffDistance  = 16f;
        private const float HoverStandoffTolerance = 2.5f;
        private const float MaxRepositionTime      = 2.5f;  // give up forcing standoff after this

        private const float ProjectileSize        = 0.6f;   // diameter of the core acid blob
        private const float ProjectileTravelTime  = 0.7f;
        private const float ProjectileArcHeight   = 2.0f;

        // ---- Burrow Ambush ---------------------------------------------------
        private const float BurrowHpThreshold   = 0.60f; // Burrow Ambush unlocks below 60% HP
        private const float BurrowCooldown      = 22f;
        private const float BurrowMinRange      = 6f;    // ignore if the player is already on top of it
        private const float BurrowMaxRange      = 25f;
        private const float BurrowTelegraph     = 0.6f;  // dig-in tell before vanishing
        private const float BurrowTravelSpeed   = 13f;   // underground dash speed
        private const float BurrowTravelMaxTime = 1.8f;  // safety cap if it can't reach the player
        private const float BurrowArriveDist    = 2.5f;  // erupt once this close to the player
        private const float EruptTelegraph      = 0.4f;  // ground-crack warning before the burst
        private const float EruptRadius         = 4.0f;
        private const float EruptPushForce      = 50f;   // knock-up force

        private const float MinionCooldown      = 60f;
        private const int   MaxNearbyMinions    = 3;
        private const int   MinionsPerSummon    = 2;
        private const float MinionDetectRadius  = 16f;
        private const string MinionPrefab       = "SeekerBrood";

        private const float FrenzyHpFraction    = 0.30f;
        private const float FrenzySpeedFactor   = 1.5f;

        // Impact splash when an acid blob lands. All known-good vanilla names
        // (used elsewhere in this mod), tried in order; first that exists is used.
        private static readonly string[] QueenSpitImpactNames =
        {
            "vfx_blob_attack",
            "vfx_HitSparks",
        };

        private Character _character;
        private ZNetView  _nview;
        private MonsterAI _monsterAI;

        private float _baseSpeed, _baseRunSpeed;
        private bool  _frenzied;
        private bool  _inSpecial;   // an AI-commandeering ability (Acid Spit / Burrow) is running
        private float _nextSpitTime;
        private float _nextBurrowTime;
        private float _nextMinionTime;
        private float _nextFrenzyPulse;

        private static GameObject _cachedMinion;

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
            _nextSpitTime    = Time.time + 6f;
            _nextBurrowTime  = Time.time + 12f;
            _nextMinionTime  = Time.time + 15f;
        }

        private void Update()
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner()) return;
            if (_character == null || _character.IsDead()) return;

            TryFrenzy();
            TryAcidSpit();
            TryBurrowAmbush();
            TrySummonBrood();
            TickFrenzyAura();
        }

        // ---- Abilities -----------------------------------------------------

        /// <summary>Current HP as a 0–1 fraction of max (1 when max HP is 0).</summary>
        private float HealthFrac()
        {
            float max = _character.GetMaxHealth();
            return max > 0f ? _character.GetHealth() / max : 1f;
        }

        private void TryFrenzy()
        {
            if (_frenzied) return;
            if (HealthFrac() > FrenzyHpFraction) return;
            _frenzied = true;

            _character.m_speed    = _baseSpeed    * FrenzySpeedFactor;
            _character.m_runSpeed = _baseRunSpeed * FrenzySpeedFactor;

            var auraT = transform.Find("BiomeLords_Aura");
            if (auraT != null)
            {
                var light = auraT.GetComponent<Light>();
                if (light != null)
                {
                    light.color     = new Color(1.0f, 0.15f, 0.10f);
                    light.intensity = 4.5f;
                    light.range     = 7.5f;
                }
            }

            var pos = transform.position + Vector3.up * 1.2f;
            FxLibrary.TrySpawn("vfx_corpse_destruction_small", pos);
            FxLibrary.TrySpawn("fx_redlightning_burst",        pos);
            Jotunn.Logger.LogInfo("[BiomeLords] Seeker Lord entered Hive Frenzy.");
        }

        private void TryAcidSpit()
        {
            if (_inSpecial) return;
            // Only once the Lord is wounded below 80% HP.
            if (HealthFrac() >= SpitHpThreshold) return;
            if (Time.time < _nextSpitTime) return;
            var target = Player.m_localPlayer;
            if (target == null || target.IsDead()) return;
            float dist = Vector3.Distance(transform.position, target.transform.position);
            if (dist > SpitTriggerRange) return;
            // Vision-gated, like Gjall's spit — needs an actual line of sight to
            // the player, not just proximity, before it'll take off and cast.
            if (_monsterAI != null && !_monsterAI.CanSeeTarget(target)) return;

            _nextSpitTime = Time.time + (_frenzied ? AcidSpitCooldown * 0.6f : AcidSpitCooldown);
            StartCoroutine(AerialAcidSpitRoutine());
        }

        /// <summary>
        /// Takes off, hovers over the battlefield for FlightHoverDuration while
        /// raining down acid volleys on the player's current position, then
        /// lands. Movement is driven directly via Character.SetMoveDir while
        /// the MonsterAI is suspended, using the same built-in flight system
        /// vanilla flying creatures (e.g. Drake) use (Character.TakeOff/Land).
        /// </summary>
        private IEnumerator AerialAcidSpitRoutine()
        {
            _inSpecial = true;
            if (_monsterAI != null) _monsterAI.enabled = false;
            _character.TakeOff();

            // Ascend — climb while already backing away toward the standoff
            // point, so it gains height AND opens the gap at the same time
            // instead of hovering directly over the player's head.
            float t = 0f;
            while (t < FlightAscendDuration)
            {
                _character.SetMoveDir((StandoffSeekDir() * 0.7f + Vector3.up).normalized);
                t += Time.deltaTime;
                yield return null;
            }

            // Reposition: fly out until we're actually at standoff range before
            // the first volley — guarantees the spit happens from a distance,
            // not right on top of the player. Capped so it can't stall forever.
            float repo = 0f;
            while (repo < MaxRepositionTime && !AtStandoff())
            {
                _character.SetMoveDir(StandoffSeekDir());   // full-speed reposition
                repo += Time.deltaTime;
                yield return null;
            }

            // Hover and fire volleys — only while genuinely airborne.
            float hoverElapsed = 0f;
            float nextVolley   = 0f;
            float hoverDuration = _frenzied ? FlightHoverDuration * 0.6f : FlightHoverDuration;
            float volleyInterval = _frenzied ? VolleyInterval * 0.6f : VolleyInterval;
            while (hoverElapsed < hoverDuration && _character.IsFlying())
            {
                // Holds station at HoverStandoffDistance — closes in if the player
                // out-ranged it, backs off if they closed the gap — instead of
                // collapsing onto the player's position.
                Vector3 standoff = StandoffSeekDir();
                _character.SetMoveDir(standoff * 0.6f);
                // While holding station (not repositioning) turn to face the
                // player so the spit always comes from the front.
                if (standoff.sqrMagnitude < 0.01f) FacePlayerHorizontally();

                if (hoverElapsed >= nextVolley)
                {
                    nextVolley = hoverElapsed + volleyInterval;
                    var target = Player.m_localPlayer;
                    // Spit only happens airborne, vision-gated, from standoff range,
                    // and only while actually facing the player.
                    if (_character.IsFlying()
                        && target != null && !target.IsDead()
                        && (_monsterAI == null || _monsterAI.CanSeeTarget(target))
                        && !TooCloseToSpit(target)
                        && IsFacingPlayer(target))
                        StartCoroutine(SpitVolley(transform.position, target.transform.position));
                }

                hoverElapsed += Time.deltaTime;
                yield return null;
            }

            // Descend back to the ground. No volleys fire past this point —
            // the spit is purely an airborne ability.
            t = 0f;
            while (t < FlightDescendDuration)
            {
                _character.SetMoveDir((StandoffSeekDir() * 0.2f + Vector3.down).normalized);
                t += Time.deltaTime;
                yield return null;
            }

            _character.Land();
            if (_monsterAI != null) _monsterAI.enabled = true;
            _inSpecial = false;
        }

        /// <summary>True once the horizontal gap to the player is within the
        /// standoff tolerance band (or wider) — i.e. far enough to start spitting.</summary>
        private bool AtStandoff()
        {
            var target = Player.m_localPlayer;
            if (target == null) return true;
            Vector3 d = target.transform.position - transform.position;
            d.y = 0f;
            return d.magnitude >= HoverStandoffDistance - HoverStandoffTolerance;
        }

        /// <summary>Guards a volley from firing if the player is much closer than
        /// the standoff distance (e.g. they rushed in under the hover).</summary>
        private bool TooCloseToSpit(Player target)
        {
            Vector3 d = target.transform.position - transform.position;
            d.y = 0f;
            return d.magnitude < HoverStandoffDistance - HoverStandoffTolerance * 2f;
        }

        /// <summary>Rotates the Lord horizontally toward the player so the spit
        /// leaves from the front. Only meaningful while holding station — the
        /// flight system drives rotation itself whenever it's actually moving.</summary>
        private void FacePlayerHorizontally()
        {
            var target = Player.m_localPlayer;
            if (target == null) return;
            Vector3 d = target.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(d.normalized), 360f * Time.deltaTime);
        }

        /// <summary>True when the Lord's forward is within the facing cone of the
        /// player — gates spit volleys so they only fire when facing the target.</summary>
        private bool IsFacingPlayer(Player target)
        {
            Vector3 d = target.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return true;
            return Vector3.Dot(transform.forward, d.normalized) >= FacingDotThreshold;
        }

        /// <summary>Direction to move to hold HoverStandoffDistance from the
        /// player: toward them if too far out, away from them if too close,
        /// zero (just hold heading) once inside the tolerance band.</summary>
        private Vector3 StandoffSeekDir()
        {
            var target = Player.m_localPlayer;
            if (target == null) return Vector3.zero;
            Vector3 d = target.transform.position - transform.position;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist < 0.01f) return Vector3.zero;
            Vector3 dir = d / dist;

            if (dist > HoverStandoffDistance + HoverStandoffTolerance) return dir;
            if (dist < HoverStandoffDistance - HoverStandoffTolerance) return -dir;
            return Vector3.zero;
        }

        /// <summary>One acid volley: a Queen-style spit projectile arcs from the
        /// Lord's current (airborne) position down to the snapshotted target
        /// position, then explodes into the usual poison/pierce AoE.</summary>
        private IEnumerator SpitVolley(Vector3 sourcePos, Vector3 targetPos)
        {
            // Reduced accuracy — scatter the aim point on the ground plane so the
            // spit rarely lands dead-on and the player can read/sidestep it.
            Vector2 scatter = Random.insideUnitCircle * SpitInaccuracy;
            targetPos += new Vector3(scatter.x, 0f, scatter.y);

            // Launch FX + sound — replay a genuine vanilla acid-attack launch
            // effect list (vfx + sfx). Always pair it with a visible muzzle cue.
            ResolveSpitAssets();
            FxLibrary.TrySpawn("vfx_seeker_attack", sourcePos);
            if (_spitLaunchEffects != null)
                _spitLaunchEffects.Create(sourcePos, Quaternion.identity);

            var proj = BuildSpitProjectile(sourcePos);

            float t = 0f;
            while (t < ProjectileTravelTime)
            {
                if (proj != null)
                {
                    float f = t / ProjectileTravelTime;
                    Vector3 pos = Vector3.Lerp(sourcePos, targetPos, f);
                    pos.y += Mathf.Sin(f * Mathf.PI) * ProjectileArcHeight;
                    proj.transform.position = pos;
                }
                t += Time.deltaTime;
                yield return null;
            }
            if (proj != null) Destroy(proj);

            // Impact FX + sound — replay a genuine vanilla acid-attack hit effect
            // list (the acid splat vfx + impact sfx). Always add the blob splat.
            if (_spitHitEffects != null)
                _spitHitEffects.Create(targetPos, Quaternion.identity);
            else
                FxLibrary.TrySpawnFirst(QueenSpitImpactNames, targetPos);
            FxLibrary.TrySpawnTimed("vfx_blob_attack", targetPos, 3f);

            float sqr = SpitRadius * SpitRadius;
            var all = Character.GetAllCharacters();
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (!(c is Player p) || p.IsDead()) continue;
                Vector3 toward = p.transform.position - targetPos;
                if (toward.sqrMagnitude > sqr) continue;

                var hit = new HitData();
                hit.m_pushForce       = 25f;
                hit.m_point           = targetPos;
                hit.m_dir             = toward.normalized;
                hit.m_hitType         = HitData.HitType.EnemyHit;
                hit.m_blockable       = true;
                hit.m_dodgeable       = true;
                hit.SetAttacker(_character);
                p.Damage(hit);
            }
        }

        private static readonly Color AcidGreen     = new Color(0.55f, 0.85f, 0.15f, 1f);
        private static readonly Color AcidGreenDark  = new Color(0.20f, 0.40f, 0.05f, 1f);
        private static Material _acidMaterial;

        // ---- Genuine vanilla acid-attack FX/SFX (resolved once at runtime) --
        private static bool       _spitAssetsResolved;
        private static EffectList _spitLaunchEffects;  // launch vfx + sfx
        private static EffectList _spitHitEffects;       // impact vfx + sfx

        private static bool HasFx(EffectList l) =>
            l != null && l.m_effectPrefabs != null && l.m_effectPrefabs.Length > 0;

        /// <summary>
        /// Pulls genuine acid-attack effect lists (which carry both vfx AND sfx)
        /// off a live vanilla creature so the Seeker Lord's spit looks and — most
        /// importantly — SOUNDS like a real Mistlands acid attack, without
        /// hardcoding prefab names (the shipped assets don't expose them as
        /// strings). Source priority:
        ///   1. Gjall — the genuine Mistlands acid-lobber (real spit vfx + sfx).
        ///   2. SeekerQueen — any ranged projectile attack EXCEPT her teleport
        ///      (cloning that ZNetView'd teleport prefab corrupted ZNetScene).
        ///   3. Seeker — base creature's melee hit effect, as a last-resort
        ///      seeker vocalisation so there's always *some* sound.
        /// Only EffectLists are captured (never the projectile prefab itself), so
        /// nothing networked is ever instantiated — the projectile stays our
        /// safe code-built blob.
        /// </summary>
        private static void ResolveSpitAssets()
        {
            if (_spitAssetsResolved) return;
            _spitAssetsResolved = true;

            foreach (var prefabName in new[] { "Gjall", "SeekerQueen", "Seeker" })
            {
                try
                {
                    var prefab = PrefabManager.Instance.GetPrefab(prefabName);
                    var hum    = prefab != null ? prefab.GetComponent<Humanoid>() : null;
                    if (hum == null) continue;

                    foreach (var arr in new[] { hum.m_defaultItems, hum.m_randomWeapon })
                    {
                        if (arr == null) continue;
                        foreach (var itemGo in arr)
                        {
                            var atk = itemGo != null
                                ? itemGo.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_attack
                                : null;
                            if (atk == null) continue;

                            string projName = atk.m_attackProjectile != null ? atk.m_attackProjectile.name : "";
                            // Skip the Queen's teleport projectile — wrong attack, and
                            // it's what caused the ZNetScene corruption when cloned.
                            if (projName.IndexOf("teleport", System.StringComparison.OrdinalIgnoreCase) >= 0)
                                continue;

                            EffectList launch = atk.m_startEffect;
                            EffectList hit    = null;
                            if (atk.m_attackProjectile != null)
                            {
                                var pj = atk.m_attackProjectile.GetComponent<Projectile>();
                                if (pj != null) hit = pj.m_hitEffects;
                            }
                            if (!HasFx(hit)) hit = atk.m_hitEffect;

                            if (HasFx(launch) || HasFx(hit))
                            {
                                _spitLaunchEffects = HasFx(launch) ? launch : null;
                                _spitHitEffects    = HasFx(hit)    ? hit    : null;
                                Jotunn.Logger.LogInfo(
                                    $"[BiomeLords] Seeker Lord spit FX/SFX sourced from '{prefabName}'" +
                                    (projName != "" ? $" (projectile '{projName}')." : " (melee effect)."));
                                return;
                            }
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Jotunn.Logger.LogWarning($"[BiomeLords] Spit asset resolve from '{prefabName}' failed: {e.Message}");
                }
            }
            Jotunn.Logger.LogInfo("[BiomeLords] No vanilla spit FX/SFX resolved — using code FX only.");
        }

        /// <summary>
        /// Builds the spit projectile: a code-built emissive acid blob (sphere +
        /// acid trail + continuous particle spray + glow). Built entirely in code
        /// rather than cloning a vanilla projectile prefab — cloning a networked
        /// projectile (it has a ZNetView) corrupts ZNetScene. The genuine vanilla
        /// look/sound comes from the launch & impact EffectLists in SpitVolley.
        /// </summary>
        private GameObject BuildSpitProjectile(Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SeekerLord_AcidSpit";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            go.transform.position   = pos;
            go.transform.localScale = Vector3.one * ProjectileSize;

            var rend = go.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                rend.sharedMaterial   = AcidMaterial();
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows    = false;
            }

            AddAcidTrail(go);
            AddProjectileFx(go);

            return go;
        }

        /// <summary>Attaches the in-flight effects that sell the acid blob: a bright
        /// green glow Light plus a code-built particle spray that continuously emits
        /// dripping acid for the whole flight. Built in code (not from a vanilla
        /// prefab name) so it's guaranteed to render rather than depending on a
        /// prefab that may not exist / may be a one-shot that stops after spawn.</summary>
        private void AddProjectileFx(GameObject go)
        {
            // Acid-green glow so the blob reads at a distance and lights the mist.
            var lightGo = new GameObject("AcidGlow");
            lightGo.transform.SetParent(go.transform, worldPositionStays: false);
            var light = lightGo.AddComponent<Light>();
            light.type      = LightType.Point;
            light.color     = AcidGreen;
            light.intensity = 5f;
            light.range     = 7f;

            // Continuous acid spray — world-space so it leaves a dripping mist
            // trail behind the moving blob instead of a single puff at spawn.
            var psGo = new GameObject("AcidSpray");
            psGo.transform.SetParent(go.transform, worldPositionStays: false);
            var ps = psGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.startColor      = AcidGreen;
            main.startSize       = 0.32f;
            main.startSpeed      = 1.1f;
            main.startLifetime   = 0.55f;
            main.gravityModifier  = 0.4f;   // droplets sag like dripping acid
            main.maxParticles    = 250;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 70f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius    = 0.25f;

            var colOverLife = ps.colorOverLifetime;
            colOverLife.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(AcidGreen, 0f), new GradientColorKey(AcidGreenDark, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            colOverLife.color = grad;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var psr = psGo.GetComponent<ParticleSystemRenderer>();
            psr.material           = AcidMaterial();
            psr.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
            psr.receiveShadows     = false;

            ps.Play();
        }

        /// <summary>Acid-green material built from a shader that's guaranteed to be
        /// loaded in Valheim's build (Shader.Find can return null for stripped
        /// built-in shaders, so we fall back to one lifted off the Lord's own
        /// renderer). Cached and shared across every volley.</summary>
        private Material AcidMaterial()
        {
            if (_acidMaterial != null) return _acidMaterial;

            var shader = ResolveVisibleShader();
            _acidMaterial = new Material(shader) { color = AcidGreen };
            if (_acidMaterial.HasProperty("_Color"))         _acidMaterial.SetColor("_Color", AcidGreen);
            if (_acidMaterial.HasProperty("_EmissionColor"))
            {
                _acidMaterial.EnableKeyword("_EMISSION");
                _acidMaterial.SetColor("_EmissionColor", AcidGreen);
            }
            return _acidMaterial;
        }

        private Shader ResolveVisibleShader()
        {
            var s = Shader.Find("Particles/Standard Unlit")
                 ?? Shader.Find("Sprites/Default")
                 ?? Shader.Find("Standard")
                 ?? Shader.Find("Unlit/Color");
            if (s != null) return s;

            // Last resort: borrow a shader off a material that's definitely loaded
            // (this creature's own renderer) so the blob is never invisible/magenta.
            var ownRend = GetComponentInChildren<Renderer>();
            return ownRend != null && ownRend.sharedMaterial != null
                ? ownRend.sharedMaterial.shader
                : null;
        }

        /// <summary>Adds the trailing acid streak behind the projectile.</summary>
        private void AddAcidTrail(GameObject go)
        {
            var trail = go.AddComponent<TrailRenderer>();
            trail.time       = 0.4f;
            trail.startWidth = 0.45f;
            trail.endWidth   = 0.05f;
            trail.numCapVertices = 4;
            trail.sharedMaterial = AcidMaterial();
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows    = false;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(AcidGreen,     0f),
                    new GradientColorKey(AcidGreenDark, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0.85f, 0f),
                    new GradientAlphaKey(0f,    1f),
                });
            trail.colorGradient = gradient;
        }

        // ---- Burrow Ambush -------------------------------------------------

        private void TryBurrowAmbush()
        {
            if (_inSpecial) return;
            // Only once the Lord is wounded below 60% HP.
            if (HealthFrac() >= BurrowHpThreshold) return;
            // Ground-only: never start a burrow while airborne or mid-jump (e.g.
            // overlapping the Acid Spit flight, or a vanilla Seeker hop).
            if (_character.IsFlying() || !_character.IsOnGround()) return;
            if (Time.time < _nextBurrowTime) return;
            var target = Player.m_localPlayer;
            if (target == null || target.IsDead()) return;
            float dist = Vector3.Distance(transform.position, target.transform.position);
            if (dist < BurrowMinRange || dist > BurrowMaxRange) return;
            if (_monsterAI != null && !_monsterAI.CanSeeTarget(target)) return;

            _nextBurrowTime = Time.time + (_frenzied ? BurrowCooldown * 0.6f : BurrowCooldown);
            StartCoroutine(BurrowAmbushRoutine());
        }

        /// <summary>
        /// Digs in (renderers hidden + dust tell), dashes underground toward the
        /// player, then erupts beneath them with a telegraphed knock-up burst.
        /// A ground-game reposition/gap-closer that contrasts the airborne Acid
        /// Spit — drives the player to keep moving instead of turtling at range.
        /// Movement reuses the MonsterAI-suspended + SetMoveDir approach.
        /// </summary>
        private IEnumerator BurrowAmbushRoutine()
        {
            _inSpecial = true;
            if (_monsterAI != null) _monsterAI.enabled = false;

            // Dig-in tell — a big dust/debris burst so it's clearly burrowing,
            // not just blinking out of existence.
            SpawnDigBurst(transform.position);
            yield return new WaitForSeconds(BurrowTelegraph);

            // Submerge: hide mesh renderers, keep particle renderers (dust trail).
            var hidden = new System.Collections.Generic.List<Renderer>();
            foreach (var r in GetComponentsInChildren<Renderer>(false))
            {
                if (r is ParticleSystemRenderer) continue;
                r.enabled = false;
                hidden.Add(r);
            }

            float origRun = _character.m_runSpeed;
            _character.m_runSpeed = BurrowTravelSpeed;
            _character.SetRun(true);

            // Underground dash toward the player, kicking up a continuous mound
            // of dust along the path so the burrow is visibly tracked.
            float elapsed   = 0f;
            float nextDust  = 0f;
            while (elapsed < BurrowTravelMaxTime)
            {
                var t = Player.m_localPlayer;
                if (t == null || t.IsDead()) break;
                Vector3 toward = t.transform.position - transform.position;
                toward.y = 0f;
                if (toward.magnitude <= BurrowArriveDist) break;
                _character.SetMoveDir(toward.normalized);

                if (elapsed >= nextDust)
                {
                    nextDust = elapsed + 0.1f;
                    FxLibrary.TrySpawn("vfx_corpse_destruction_small", transform.position);
                    FxLibrary.TrySpawn("vfx_HitSparks",                transform.position);
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            _character.SetMoveDir(Vector3.zero);
            _character.m_runSpeed = origRun;
            _character.SetRun(false);

            // Resurface burst — same dramatic dust/debris so it visibly erupts
            // out of the ground rather than popping back into view.
            var center = transform.position;
            SpawnDigBurst(center);
            foreach (var r in hidden) if (r != null) r.enabled = true;

            // Erupt telegraph, then the knock-up burst.
            yield return new WaitForSeconds(EruptTelegraph);

            FxLibrary.TrySpawn("vfx_gdking_stomp",             center);
            FxLibrary.TrySpawn("fx_himminafl_aoe",             center);
            FxLibrary.TrySpawn("vfx_corpse_destruction_small", center);
            FxLibrary.TrySpawn("vfx_HitSparks",                center);
            FxLibrary.TrySpawn("fx_redlightning_burst",        center + Vector3.up * 0.5f);

            float sqr = EruptRadius * EruptRadius;
            var all = Character.GetAllCharacters();
            for (int i = 0; i < all.Count; i++)
            {
                if (!(all[i] is Player p) || p.IsDead()) continue;
                Vector3 flat = p.transform.position - center;
                flat.y = 0f;
                if (flat.sqrMagnitude > sqr) continue;

                // Knock-up: push outward + strongly upward.
                Vector3 knock = (flat.sqrMagnitude > 0.01f ? flat.normalized : Vector3.zero) + Vector3.up * 2f;

                var hit = new HitData();
                hit.m_pushForce = EruptPushForce;
                hit.m_point     = p.transform.position;
                hit.m_dir       = knock.normalized;
                hit.m_hitType   = HitData.HitType.EnemyHit;
                hit.m_blockable = true;
                hit.m_dodgeable = true;
                hit.SetAttacker(_character);
                p.Damage(hit);
            }

            if (_monsterAI != null) _monsterAI.enabled = true;
            _inSpecial = false;
        }

        /// <summary>A dramatic ground dig/erupt burst — several known-good vanilla
        /// FX layered together (stomp dust ring, flying debris, mist puff, sparks)
        /// plus a ring of extra debris around the point, so the Lord visibly tunnels
        /// into / bursts out of the ground rather than just vanishing/reappearing.</summary>
        private void SpawnDigBurst(Vector3 center)
        {
            FxLibrary.TrySpawn("vfx_gdking_stomp",             center);
            FxLibrary.TrySpawn("vfx_corpse_destruction_small", center);
            FxLibrary.TrySpawn("vfx_mist_puff",                center + Vector3.up * 0.5f);
            FxLibrary.TrySpawn("vfx_HitSparks",                center);

            // Ring of debris bursts around the dig point for extra volume.
            for (int i = 0; i < 6; i++)
            {
                float ang = i * 60f;
                Vector3 ringPos = center + Quaternion.Euler(0f, ang, 0f) * Vector3.forward * 1.6f;
                FxLibrary.TrySpawn("vfx_corpse_destruction_small", ringPos);
            }
        }

        private void TrySummonBrood()
        {
            if (Time.time < _nextMinionTime) return;
            int alive = CountNearbyBrood();
            if (alive >= MaxNearbyMinions)
            {
                _nextMinionTime = Time.time + 15f;
                return;
            }
            EnsurePrefabs();
            if (_cachedMinion == null) return;

            int budget = System.Math.Min(MinionsPerSummon, MaxNearbyMinions - alive);
            for (int i = 0; i < budget; i++)
            {
                float angle = i * (360f / System.Math.Max(1, budget)) + Random.Range(-25f, 25f);
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 pos = transform.position + dir * 3.5f + Vector3.up * 0.5f;
                Instantiate(_cachedMinion, pos, Quaternion.LookRotation(-dir));
                FxLibrary.TrySpawn("vfx_spawn", pos);
            }
            _nextMinionTime = Time.time + MinionCooldown;
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

        // ---- Helpers -------------------------------------------------------

        private int CountNearbyBrood()
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
                if (c.gameObject.name.StartsWith("Seeker")) count++;
            }
            return count;
        }

        private static void EnsurePrefabs()
        {
            if (_cachedMinion == null) _cachedMinion = PrefabManager.Instance.GetPrefab(MinionPrefab);
        }
    }
}
