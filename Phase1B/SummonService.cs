using UnityEngine;
using Jotunn.Managers;
using BiomeLords.Config;
using BiomeLords.Data;
using BiomeLords.Util;
// FxLibrary is in BiomeLords.Util — already imported above.

namespace BiomeLords.Phase1B
{
    /// <summary>
    /// Decides whether a Horn-use should summon a Lord (biome + kill threshold),
    /// spawns the Lord, applies boss-key scaling, and kicks off the random event.
    /// </summary>
    public static class SummonService
    {
        /// <summary>
        /// Returns true if the horn was successfully consumed by a summon.
        /// Sends messages to the player describing success or failure mode.
        /// </summary>
        public static bool TryUseHorn(Player player)
        {
            if (player == null) return false;

            // Night only — the Lord answers only when darkness is upon the land.
            if (EnvMan.instance != null && !EnvMan.IsNight())
            {
                player.Message(MessageHud.MessageType.Center, "$biomelords_horn_fail_day");
                return false;
            }

            var biome = player.GetCurrentBiome().ToString();
            var lord = FindLordForBiome(biome);
            if (lord == null)
            {
                player.Message(MessageHud.MessageType.Center, "$biomelords_horn_fail_biome");
                return false;
            }

            int progress = KillStore.SumFor(player, lord.KillTargets);
            int required = LordConfig.KillRequirement(lord.Id);
            if (progress < required)
            {
                player.Message(MessageHud.MessageType.Center,
                    $"$biomelords_horn_fail_kills ({progress}/{required})");
                return false;
            }

            string prefabName = ResolvePrefabName(lord.Id);
            if (string.IsNullOrEmpty(prefabName))
            {
                player.Message(MessageHud.MessageType.Center,
                    $"The {lord.DisplayName} sleeps still. (Not yet implemented.)");
                return false;
            }
            var prefab = PrefabManager.Instance.GetPrefab(prefabName);
            if (prefab == null)
            {
                Jotunn.Logger.LogError($"[BiomeLords] {prefabName} prefab missing at summon time.");
                return false;
            }

            // Spawn 8m in front of the player so it doesn't telefrag.
            Vector3 pos = player.transform.position + player.transform.forward * 8f + Vector3.up * 0.5f;

            FxLibrary.DumpFxNamesOnce();
            LordFx.PlaySummon(lord.Id, pos);

            var go = Object.Instantiate(prefab, pos, Quaternion.LookRotation(-player.transform.forward));

            ApplyScaling(go, lord);

            // Trigger the matching world event.
            var sys = RandEventSystem.instance;
            var eventName = EventFactory.EventNameFor(lord.Id);
            if (sys != null && !string.IsNullOrEmpty(eventName))
            {
                sys.SetRandomEventByName(eventName, pos);
            }

            Jotunn.Logger.LogInfo(
                $"[BiomeLords] {player.GetPlayerName()} summoned {lord.DisplayName} at {pos}.");
            return true;
        }

        private static string ResolvePrefabName(string lordId)
        {
            switch (lordId)
            {
                case "neck_lord":      return CreatureFactory.NeckLordPrefab;
                case "greydwarf_lord": return CreatureFactory.GreydwarfShamanLordPrefab;
                case "draugr_lord":    return CreatureFactory.DraugrEliteLordPrefab;
                case "fenring_lord":   return CreatureFactory.FenringLordPrefab;
                case "lox_lord":       return CreatureFactory.LoxLordPrefab;
                case "seeker_lord":    return CreatureFactory.SeekerLordPrefab;
                case "faller_valkyrie_lord": return CreatureFactory.FallerValkyrieLordPrefab;
                default:               return null;
            }
        }

        private static BiomeLordDef FindLordForBiome(string biomeName)
        {
            foreach (var l in LordRegistry.All)
                if (l.Biome.Equals(biomeName, System.StringComparison.OrdinalIgnoreCase)) return l;
            return null;
        }

        // Summon FX moved to LordFx.PlaySummon(lordId, pos) for per-Lord theming.

        /// <summary>
        /// Progression scaling. effectiveTier = max(lord.Tier, highestDefeatedLordTier).
        ///
        /// Full convergence: once the player has defeated a higher-tier Lord, lower
        /// Lords adopt that tier's boss stats outright — HP becomes the higher boss's
        /// HP and the attack adopts the higher boss's attack profile. At the native
        /// tier each Lord is exactly its own biome boss.
        /// </summary>
        private static void ApplyScaling(GameObject go, BiomeLordDef lord)
        {
            int highestLordTier = LordDefeatStore.HighestDefeatedTier();
            int effectiveTier   = System.Math.Max(lord.Tier, highestLordTier);

            float hpCfgMult    = LordConfig.HealthMultiplier(lord.Id);
            float dmgCfgMult   = LordConfig.DamageMultiplier(lord.Id);
            float dmgIntrinsic = LordIntrinsic.DamageMultiplier(lord.Id);

            // HP: base (= boss HP at native tier) × tier-progression ratio. For
            // boss-backed Lords this converges to HpFor(effectiveTier).
            float baseHp   = LordBaseStats.HpFor(lord.Id, lord.Tier);
            float hpRatio  = TierTable.HpFor(effectiveTier) / TierTable.HpFor(lord.Tier);
            float targetHp = baseHp * hpRatio * hpCfgMult;

            // Damage: adopt the effectiveTier boss attack profile (own profile at
            // native tier). The stored mult carries only admin config × intrinsic.
            DamageProfile profile = LordAttackProfile.Resolve(lord.Id, lord.Tier, effectiveTier);
            float dmgMult = dmgCfgMult * dmgIntrinsic;

            var character = go.GetComponent<Character>();
            var humanoid  = go.GetComponent<Humanoid>();
            if (humanoid != null)
            {
                humanoid.SetMaxHealth(targetHp);
                humanoid.SetHealth(targetHp);
            }
            if (character != null)
            {
                LordDamageRegistry.Set(character, dmgMult);
                LordProfileRegistry.Set(character, profile);
            }

            Jotunn.Logger.LogInfo(
                $"[BiomeLords] Scaling {lord.Id}: lordTier={lord.Tier} highestLordTier={highestLordTier} " +
                $"effectiveTier={effectiveTier} HP={targetHp:F0} (base {baseHp:F0} × ratio {hpRatio:F2} × cfg {hpCfgMult:F2}) " +
                $"profile@tier{effectiveTier} dmgMult={dmgMult:F2} (cfg {dmgCfgMult:F2} × intrinsic {dmgIntrinsic:F2})");
        }
    }
}
