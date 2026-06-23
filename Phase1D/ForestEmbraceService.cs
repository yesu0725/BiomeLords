using UnityEngine;
using HarmonyLib;
using BiomeLords.Util;
using BiomeLords.Phase1C;

namespace BiomeLords.Phase1D
{
    /// <summary>
    /// "Forest's Embrace" — the Greydwarf Shaman Lord's Forsaken Power.
    /// While active, the player heals while standing close to any tree (heal
    /// scales with tree tier) AND has their comfort level bumped by the
    /// tree's tier so the vanilla Rested system grants a longer rest buff.
    /// </summary>
    public static class ForestEmbraceService
    {
        private const float TickSeconds         = 3f;
        private const float TreeRadius          = 3f;    // close range — "right beside the tree"
        private const float MonsterCheckRadius  = 30f;   // comfort only with no hostiles in range
        private const float SitSecondsRequired  = 60f;   // crouch this long to earn Rested

        private static float _nextTick;
        private static int   _embraceHash;
        private static float _sitSecondsAccumulated;

        /// <summary>Comfort bonus from the strongest tree currently within range.
        /// Zero when no tree is in range or the FP isn't active.</summary>
        public static int CurrentTreeComfort { get; private set; }

        /// <summary>True while the player is within range of any qualifying tree
        /// AND has the Forest's Embrace FP active. Used by the shelter override
        /// patch — shelter applies the moment you approach a tree, independent
        /// of the sit-for-60s comfort gate.</summary>
        public static bool IsNearQualifyingTree { get; private set; }

        private static readonly (string prefix, int heal, int comfort)[] TreeTable =
        {
            // Most-specific prefixes first.
            ("CharredTree",  6, 3),
            ("Yggashoot",    5, 3),
            ("YggaShoot",    5, 3),
            ("SwampTree",    4, 1),
            ("Oak",          3, 2),
            ("Pine",         2, 1),
            ("FirTree",      2, 1),
            ("Fir",          2, 1),
            ("Beech",        1, 1),
            ("Birch",        1, 1),
        };

        public static void Tick()
        {
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + TickSeconds;

            CurrentTreeComfort  = 0;
            IsNearQualifyingTree = false;

            var p = Player.m_localPlayer;
            if (p == null || p.IsDead()) return;
            var seman = p.GetSEMan();
            if (seman == null) return;

            if (_embraceHash == 0)
                _embraceHash = GuardianPowerFactory.GreydwarfLordGP.GetStableHashCode();

            // Active condition: either (a) the SE marker is on the player from
            // a previous F-activation, OR (b) the GP is currently equipped AND
            // its cooldown timer says it's actively running. The "running"
            // signal is m_guardianPowerCooldown > vanilla cooldown threshold
            // — but simpler: just check the player's equipped GP name AND
            // that the SE is in the SEMan.
            if (!seman.HaveStatusEffect(_embraceHash))
            {
                _sitSecondsAccumulated = 0f;
                RemoveSitting(seman);
                if (BiomeLords.Config.LordConfig.DebugLogging != null
                    && BiomeLords.Config.LordConfig.DebugLogging.Value
                    && p.GetGuardianPowerName() == GuardianPowerFactory.GreydwarfLordGP
                    && p.m_guardianPowerCooldown > 0f)
                {
                    Jotunn.Logger.LogInfo(
                        "[BiomeLords] ForestEmbrace: GP equipped + on cooldown but marker SE missing from SEMan.");
                }
                return;
            }

            var (heal, comfort) = FindStrongestTreeNearby(p.transform.position);
            if (heal <= 0)
            {
                _sitSecondsAccumulated = 0f;
                RemoveSitting(seman);
                if (BiomeLords.Config.LordConfig.DebugLogging.Value)
                    Jotunn.Logger.LogInfo("[BiomeLords] ForestEmbrace: SE active, no tree within 3m.");
                return;
            }

            // From here on we're definitely near a tree — shelter applies.
            IsNearQualifyingTree = true;

            bool safe = !AnyHostileWithin(p.transform.position, MonsterCheckRadius, p);
            if (!safe)
            {
                _sitSecondsAccumulated = 0f;
                RemoveSitting(seman);
                if (BiomeLords.Config.LordConfig.DebugLogging.Value)
                    Jotunn.Logger.LogInfo($"[BiomeLords] ForestEmbrace: tree found (heal={heal}, comfort={comfort}) but hostile within 30m.");
                return;
            }

            // Both healing AND comfort/Rested require actually sitting —
            // resting under a tree is the whole point. Standing near a tree
            // does nothing but tag you as sheltered.
            bool sitting = p.InEmote() || p.IsAttached();
            if (!sitting)
            {
                _sitSecondsAccumulated = 0f;
                RemoveSitting(seman);
                if (BiomeLords.Config.LordConfig.DebugLogging.Value)
                    Jotunn.Logger.LogInfo("[BiomeLords] ForestEmbrace: sheltered but not seated. Sit to heal and earn Rested.");
                return;
            }

            // Seated — heal pulse.
            p.Heal(heal, showText: false);
            FxLibrary.TrySpawn("vfx_lootspawn", p.transform.position + Vector3.up * 0.5f);

            _sitSecondsAccumulated += TickSeconds;
            if (_sitSecondsAccumulated < SitSecondsRequired)
            {
                // Buildup phase — show the "sitting under a tree" icon so the
                // player has visible feedback that they're earning Rested.
                ApplySitting(seman);
                if (BiomeLords.Config.LordConfig.DebugLogging.Value)
                    Jotunn.Logger.LogInfo(
                        $"[BiomeLords] ForestEmbrace: sitting {_sitSecondsAccumulated:F0}/{SitSecondsRequired:F0}s, healed {heal}.");
                return;
            }

            // Earned — drop the buildup icon, apply comfort + refresh Rested.
            RemoveSitting(seman);
            CurrentTreeComfort = comfort;
            ApplyVanillaRested(p, seman, comfort);

            if (BiomeLords.Config.LordConfig.DebugLogging.Value)
                Jotunn.Logger.LogInfo($"[BiomeLords] ForestEmbrace tick: healed {heal}, comfort+={comfort}, Rested applied.");
        }

        private static StatusEffect _restedTemplate;
        private static int          _sittingHash;
        private static int          _restedHash;
        private static System.Reflection.FieldInfo _comfortLevelField;

        private static void ApplySitting(SEMan seman)
        {
            if (!SubEffectFactory.ByName.TryGetValue(SubEffectFactory.ForestSitSE, out var se) || se == null) return;
            SubEffectFactory.EnsureIcon(se, "Rested");
            seman.AddStatusEffect(se, resetTime: true);
        }

        private static void RemoveSitting(SEMan seman)
        {
            if (_sittingHash == 0) _sittingHash = SubEffectFactory.ForestSitSE.GetStableHashCode();
            if (seman.HaveStatusEffect(_sittingHash)) seman.RemoveStatusEffect(_sittingHash);
        }

        /// <summary>
        /// Applies / refreshes vanilla SE_Rested on the player. Belt-and-braces:
        ///   (a) directly bump Player.m_comfortLevel via reflection so vanilla
        ///       Rested.Update sees the rest area as "active".
        ///   (b) explicitly set the applied SE's m_ttl to the comfort-extended
        ///       duration (m_baseTTL + comfort * m_TTLPerComfortLevel) so we
        ///       don't depend on the Setup-time GetComfortLevel patch firing.
        /// </summary>
        private static void ApplyVanillaRested(Player p, SEMan seman, int treeComfort)
        {
            // Resolve the vanilla "Rested" SE from ObjectDB once.
            if (_restedTemplate == null)
            {
                var db = ObjectDB.instance;
                if (db == null) return;
                if (_restedHash == 0) _restedHash = "Rested".GetStableHashCode();
                _restedTemplate = db.GetStatusEffect(_restedHash);
                if (_restedTemplate == null)
                {
                    Jotunn.Logger.LogWarning("[BiomeLords] Could not find vanilla 'Rested' SE in ObjectDB.");
                    return;
                }
            }

            // (a) Override the cached comfort field.
            if (_comfortLevelField == null)
                _comfortLevelField = AccessTools.Field(typeof(Player), "m_comfortLevel");
            if (_comfortLevelField != null)
            {
                int current = (int)(_comfortLevelField.GetValue(p) ?? 0);
                if (treeComfort > current)
                    _comfortLevelField.SetValue(p, treeComfort);
            }

            // (b) Apply / refresh the SE, then force the TTL.
            seman.AddStatusEffect(_restedTemplate, resetTime: true);

            var applied = seman.GetStatusEffect(_restedHash) as SE_Rested;
            if (applied != null)
            {
                float desired = applied.m_baseTTL + treeComfort * applied.m_TTLPerComfortLevel;
                if (applied.m_ttl < desired) applied.m_ttl = desired;
                if (BiomeLords.Config.LordConfig.DebugLogging.Value)
                    Jotunn.Logger.LogInfo($"[BiomeLords] Rested applied: m_ttl={applied.m_ttl:F0}s (base {applied.m_baseTTL}, +{treeComfort}*{applied.m_TTLPerComfortLevel}).");
            }
            else if (BiomeLords.Config.LordConfig.DebugLogging.Value)
            {
                Jotunn.Logger.LogWarning("[BiomeLords] Rested apply returned null — SEMan rejected.");
            }
        }

        private static bool AnyHostileWithin(Vector3 center, float r, Player self)
        {
            float sqr = r * r;
            var all = Character.GetAllCharacters();
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || c.IsDead() || c == self) continue;
                if (c is Player) continue;          // other players don't disturb rest
                if (c.IsTamed()) continue;          // tamed companions are fine
                if ((c.transform.position - center).sqrMagnitude > sqr) continue;
                return true;
            }
            return false;
        }

        private static (int heal, int comfort) FindStrongestTreeNearby(Vector3 pos)
        {
            int bestHeal = 0, bestComfort = 0;
            var cols = Physics.OverlapSphere(pos, TreeRadius);
            for (int i = 0; i < cols.Length; i++)
            {
                var col = cols[i];
                if (col == null) continue;
                var name = col.transform.root.name;
                if (!IsMatureTree(name)) continue;

                foreach (var entry in TreeTable)
                {
                    if (!name.StartsWith(entry.prefix)) continue;
                    if (entry.heal > bestHeal)
                    {
                        bestHeal    = entry.heal;
                        bestComfort = entry.comfort;
                    }
                    break;
                }
            }
            return (bestHeal, bestComfort);
        }

        /// <summary>Saplings, logs, stubs/stumps, and small juvenile variants
        /// don't count. Only the standing mature tree variants give shelter/heal/comfort.</summary>
        private static bool IsMatureTree(string rootName)
        {
            if (string.IsNullOrEmpty(rootName)) return false;
            var cmp = System.StringComparison.OrdinalIgnoreCase;
            if (rootName.IndexOf("Sapling", cmp) >= 0) return false;
            if (rootName.IndexOf("small",   cmp) >= 0) return false;   // Beech_small1, FirTree_small, YggashootSmall
            if (rootName.IndexOf("log",     cmp) >= 0) return false;   // _log, oldLog, etc.
            if (rootName.IndexOf("stub",    cmp) >= 0) return false;   // BirchStub
            if (rootName.IndexOf("stump",   cmp) >= 0) return false;   // CharredTreeStump
            if (rootName.IndexOf("dead",    cmp) >= 0) return false;   // dead/destroyed variants
            return true;
        }
    }
}
