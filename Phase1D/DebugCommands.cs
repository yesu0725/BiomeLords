using System.Collections.Generic;
using System.Linq;
using Jotunn.Entities;
using Jotunn.Managers;
using BiomeLords.Config;
using BiomeLords.Data;
using BiomeLords.Util;

namespace BiomeLords.Phase1D
{
    /// <summary>
    /// Admin-only console (F5) commands for debugging BiomeLords gameplay loops.
    /// </summary>
    public static class DebugCommands
    {
        public static void RegisterAll()
        {
            CommandManager.Instance.AddConsoleCommand(new ResetCooldowns());
            CommandManager.Instance.AddConsoleCommand(new CropTimes());
            CommandManager.Instance.AddConsoleCommand(new Intrinsic());
            CommandManager.Instance.AddConsoleCommand(new DumpAttacks());
            CommandManager.Instance.AddConsoleCommand(new TameTime());
            CommandManager.Instance.AddConsoleCommand(new DumpQueenAttackFx());
            Jotunn.Logger.LogInfo("[BiomeLords] Registered debug console commands.");
        }

        // -----------------------------------------------------------------

        /// <summary>biomelords_reset_cooldowns — clears the local player's
        /// Forsaken Power cooldown AND every BiomeLords blessing cooldown.</summary>
        private class ResetCooldowns : ConsoleCommand
        {
            public override string Name        => "biomelords_reset_cooldowns";
            public override string Help        => "Resets BiomeLords Forsaken Power and pedestal blessing cooldowns for the local player.";
            // IsCheat=true means the command only works when devcommands is enabled.
            // On dedicated servers, devcommands is gated to admins — so this is
            // effectively admin-only in multiplayer.
            public override bool IsCheat   => true;
            public override bool IsNetwork => false;

            public override void Run(string[] args)
            {
                var p = Player.m_localPlayer;
                if (p == null)
                {
                    Console.instance?.Print("BiomeLords: no local player.");
                    return;
                }

                // Forsaken Power cooldown.
                p.m_guardianPowerCooldown = 0f;

                // Blessing cooldowns — clear every "BiomeLords.bless.next.*" key.
                if (p.m_customData != null)
                {
                    var toRemove = p.m_customData.Keys
                        .Where(k => k.StartsWith("BiomeLords.bless.next."))
                        .ToList();
                    foreach (var k in toRemove) p.m_customData.Remove(k);
                    Console.instance?.Print($"BiomeLords: cleared {toRemove.Count} blessing cooldown(s) + GP cooldown.");
                }
                else
                {
                    Console.instance?.Print("BiomeLords: GP cooldown cleared.");
                }
            }
        }

        // -----------------------------------------------------------------

        /// <summary>biomelords_crop_times — toggle on/off hover-text display
        /// of remaining grow time on every planted crop.</summary>
        private class CropTimes : ConsoleCommand
        {
            public override string Name      => "biomelords_crop_times";
            public override string Help      => "Toggles hover-text display of remaining grow time on planted crops (debug).";
            public override bool IsCheat   => true;
            public override bool IsNetwork => false;

            public override void Run(string[] args)
            {
                bool before = LordConfig.ShowCropGrowTimes.Value;
                LordConfig.ShowCropGrowTimes.Value = !before;
                bool after  = LordConfig.ShowCropGrowTimes.Value;

                Console.instance?.Print(
                    $"BiomeLords: crop grow-time display {before} -> {after}.");

                if (after == before)
                {
                    Console.instance?.Print(
                        "  (Write may have been blocked. Edit the config file directly: " +
                        "BepInEx/config/com.taeguk.BiomeLords.cfg → [General] ShowCropGrowTimes = true)");
                }
                else
                {
                    Console.instance?.Print(
                        "  Hover over a planted crop to see remaining / total grow time in cyan.");
                }
            }
        }

        // -----------------------------------------------------------------

        /// <summary>
        /// biomelords_intrinsic — read/write the per-Lord intrinsic damage
        /// multiplier at runtime. Persists only for the current session; the
        /// baked default in LordIntrinsic.cs is what the next launch starts
        /// from. Useful for live balance testing without rebuilding the dll.
        ///
        /// Usage:
        ///   biomelords_intrinsic                       — list every Lord's current vs default
        ///   biomelords_intrinsic &lt;lord_id&gt;             — show one Lord
        ///   biomelords_intrinsic &lt;lord_id&gt; &lt;value&gt;     — set (also refreshes already-spawned instances)
        ///   biomelords_intrinsic &lt;lord_id&gt; reset       — restore one Lord to its baked default
        ///   biomelords_intrinsic reset                 — restore all Lords
        /// </summary>
        private class Intrinsic : ConsoleCommand
        {
            public override string Name      => "biomelords_intrinsic";
            public override string Help      =>
                "Read/write per-Lord intrinsic damage multiplier. " +
                "Usage: biomelords_intrinsic [<lord_id> [<value>|reset] | reset]. " +
                "No args = list all. Changes are session-only.";
            public override bool IsCheat   => true;
            public override bool IsNetwork => false;

            public override void Run(string[] args)
            {
                if (args == null || args.Length == 0)
                {
                    PrintAll();
                    return;
                }

                // Bulk reset.
                if (args.Length == 1 && args[0].Equals("reset", System.StringComparison.OrdinalIgnoreCase))
                {
                    int n = LordIntrinsic.ResetAll();
                    RefreshAllLordInstances();
                    Console.instance?.Print($"BiomeLords: reset {n} Lord intrinsics to defaults.");
                    return;
                }

                string lordId = args[0].ToLowerInvariant();
                if (!LordIntrinsic.IsKnown(lordId))
                {
                    Console.instance?.Print($"BiomeLords: unknown lord_id '{lordId}'. Known: {string.Join(", ", LordIntrinsic.Ids)}");
                    return;
                }

                // Single read.
                if (args.Length == 1)
                {
                    PrintOne(lordId);
                    return;
                }

                // Single reset.
                if (args.Length == 2 && args[1].Equals("reset", System.StringComparison.OrdinalIgnoreCase))
                {
                    float before = LordIntrinsic.DamageMultiplier(lordId);
                    LordIntrinsic.Reset(lordId);
                    float after = LordIntrinsic.DamageMultiplier(lordId);
                    RefreshLordInstances(lordId);
                    Console.instance?.Print($"BiomeLords: {lordId} intrinsic {before:F2} -> {after:F2} (default).");
                    return;
                }

                // Set.
                if (args.Length >= 2)
                {
                    if (!float.TryParse(args[1], System.Globalization.NumberStyles.Float,
                                        System.Globalization.CultureInfo.InvariantCulture, out var value))
                    {
                        Console.instance?.Print($"BiomeLords: '{args[1]}' is not a number. Examples: 1.0, 0.35, 2.5");
                        return;
                    }
                    if (value < 0f)
                    {
                        Console.instance?.Print("BiomeLords: value must be >= 0.");
                        return;
                    }
                    float before = LordIntrinsic.Set(lordId, value);
                    int refreshed = RefreshLordInstances(lordId);
                    Console.instance?.Print(
                        $"BiomeLords: {lordId} intrinsic {before:F2} -> {value:F2} " +
                        $"(refreshed {refreshed} live instance(s); next spawns use the new value).");
                    return;
                }
            }

            private static void PrintAll()
            {
                Console.instance?.Print("BiomeLords intrinsic damage multipliers (current / default):");
                foreach (var id in LordIntrinsic.Ids)
                {
                    float cur = LordIntrinsic.DamageMultiplier(id);
                    float def = LordIntrinsic.DefaultFor(id);
                    string tag = System.Math.Abs(cur - def) > 0.0001f ? "  *" : "";
                    Console.instance?.Print($"  {id,-16}  {cur:F2} / {def:F2}{tag}");
                }
                Console.instance?.Print("Set: biomelords_intrinsic <lord_id> <value>   |   Reset: biomelords_intrinsic [<lord_id>] reset");
            }

            private static void PrintOne(string lordId)
            {
                float cur = LordIntrinsic.DamageMultiplier(lordId);
                float def = LordIntrinsic.DefaultFor(lordId);
                Console.instance?.Print($"BiomeLords: {lordId} intrinsic = {cur:F2} (default {def:F2}).");
            }

            /// <summary>
            /// After an intrinsic change, recompute and overwrite the cached
            /// LordDamageRegistry entry for every alive Lord of this id so
            /// the change applies mid-fight without re-spawning.
            /// Returns the number of instances refreshed.
            /// </summary>
            private static int RefreshLordInstances(string lordId)
            {
                int touched = 0;
                var def = LordRegistry.ById(lordId);
                if (def == null) return 0;

                // Match the convergence model used at spawn (SummonService): the
                // resolved attack profile carries tier progression; the stored
                // mult is only config × intrinsic.
                int effectiveTier = System.Math.Max(def.Tier, LordDefeatStore.HighestDefeatedTier());
                DamageProfile profile = LordAttackProfile.Resolve(def.Id, def.Tier, effectiveTier);
                float cfgMult    = LordConfig.DamageMultiplier(def.Id);
                float intrinsic  = LordIntrinsic.DamageMultiplier(def.Id);
                float newDmgMult = cfgMult * intrinsic;

                var all = Character.GetAllCharacters();
                for (int i = 0; i < all.Count; i++)
                {
                    var c = all[i];
                    if (c == null || c.IsDead()) continue;
                    if (RegisteredLords.LordIdFor(c) != lordId) continue;
                    LordDamageRegistry.Set(c, newDmgMult);
                    LordProfileRegistry.Set(c, profile);
                    touched++;
                }
                return touched;
            }

            private static void RefreshAllLordInstances()
            {
                foreach (var id in LordIntrinsic.Ids) RefreshLordInstances(id);
            }
        }

        // -----------------------------------------------------------------

        /// <summary>
        /// biomelords_tame_time — shows how much taming time remains for every
        /// untamed tameable creature within a given radius of the local player.
        ///
        /// Usage:
        ///   biomelords_tame_time            — scan within 10 m (default)
        ///   biomelords_tame_time &lt;radius&gt;   — scan within &lt;radius&gt; metres
        /// </summary>
        private class TameTime : ConsoleCommand
        {
            public override string Name    => "biomelords_tame_time";
            public override string Help    =>
                "Shows remaining tame time for untamed tameable creatures near the local player. " +
                "Usage: biomelords_tame_time [radius]  (default radius = 10 m)";
            public override bool IsCheat   => true;
            public override bool IsNetwork => false;

            private const float DefaultRadius = 10f;

            public override void Run(string[] args)
            {
                var p = Player.m_localPlayer;
                if (p == null)
                {
                    Console.instance?.Print("BiomeLords: no local player.");
                    return;
                }

                float radius = DefaultRadius;
                if (args != null && args.Length >= 1)
                {
                    if (!float.TryParse(args[0],
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out radius) || radius <= 0f)
                    {
                        Console.instance?.Print(
                            "BiomeLords: invalid radius. Usage: biomelords_tame_time [radius]");
                        return;
                    }
                }

                var playerPos = p.transform.position;
                var results   = new List<string>();

                var allChars = Character.GetAllCharacters();
                for (int i = 0; i < allChars.Count; i++)
                {
                    var c = allChars[i];
                    if (c == null || c.IsDead()) continue;
                    if (UnityEngine.Vector3.Distance(c.transform.position, playerPos) > radius) continue;

                    var tameable = c.GetComponent<Tameable>();
                    if (tameable == null) continue;
                    if (c.IsTamed()) continue;

                    float total     = tameable.m_tamingTime;
                    var   nview     = c.GetComponent<ZNetView>();
                    float remaining = nview != null && nview.GetZDO() != null
                        ? nview.GetZDO().GetFloat(ZDOVars.s_tameTimeLeft, total)
                        : total;
                    float progress  = total > 0f ? (1f - remaining / total) * 100f : 100f;

                    int remMin = (int)(remaining / 60f);
                    int remSec = (int)(remaining % 60f);

                    string displayName = Localization.instance != null
                        ? Localization.instance.Localize(c.m_name)
                        : c.m_name;

                    float dist = UnityEngine.Vector3.Distance(c.transform.position, playerPos);

                    results.Add(
                        $"  {displayName} ({dist:F1}m away): " +
                        $"{remMin}m {remSec}s left  ({progress:F0}% tamed, " +
                        $"total {total / 60f:F1}m)");
                }

                if (results.Count == 0)
                {
                    Console.instance?.Print(
                        $"BiomeLords: no untamed tameable creatures within {radius:F0} m.");
                    return;
                }

                Console.instance?.Print(
                    $"BiomeLords: {results.Count} untamed tameable creature(s) within {radius:F0} m:");
                foreach (var line in results)
                    Console.instance?.Print(line);
            }
        }

        // -----------------------------------------------------------------

        /// <summary>
        /// biomelords_dump_attacks — prints every attack's base damage values
        /// for the seven vanilla base prefabs that Lords are cloned from.
        /// Copy the output from BepInEx/LogOutput.log into the handbook data.
        ///
        /// Usage: biomelords_dump_attacks
        /// </summary>
        private class DumpAttacks : ConsoleCommand
        {
            public override string Name    => "biomelords_dump_attacks";
            public override string Help    => "Dumps vanilla melee attack damage values for all 7 Lord base prefabs to the log.";
            public override bool IsCheat   => true;
            public override bool IsNetwork => false;

            private static readonly string[] BasePrefabs =
            {
                "Neck", "Greydwarf_Shaman", "Draugr_Elite",
                "Fenring", "Lox", "Seeker", "FallenValkyrie",
            };

            public override void Run(string[] args)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("[BiomeLords] === Vanilla attack damage dump ===");

                foreach (var prefabName in BasePrefabs)
                {
                    var go = PrefabManager.Instance.GetPrefab(prefabName);
                    if (go == null)
                    {
                        sb.AppendLine($"  {prefabName}: PREFAB NOT FOUND");
                        continue;
                    }

                    sb.AppendLine($"\n  [{prefabName}]");

                    var humanoid = go.GetComponent<Humanoid>();
                    if (humanoid == null)
                    {
                        sb.AppendLine("    (no Humanoid component)");
                        continue;
                    }

                    // Collect all attack items from default, random weapons, and left-hand.
                    var itemSets = new System.Collections.Generic.List<ItemDrop.ItemData>();
                    void Collect(ItemDrop.ItemData item)
                    {
                        if (item != null) itemSets.Add(item);
                    }
                    void CollectArray(UnityEngine.GameObject[] arr)
                    {
                        if (arr == null) return;
                        foreach (var go2 in arr)
                        {
                            if (go2 == null) continue;
                            var id2 = go2.GetComponent<ItemDrop>();
                            if (id2 != null) Collect(id2.m_itemData);
                        }
                    }
                    CollectArray(humanoid.m_defaultItems);
                    CollectArray(humanoid.m_randomWeapon);
                    CollectArray(humanoid.m_randomShield);

                    if (itemSets.Count == 0)
                    {
                        sb.AppendLine("    (no default items found)");
                        continue;
                    }

                    foreach (var item in itemSets)
                    {
                        if (item.m_shared == null) continue;
                        var dmg = item.m_shared.m_damages;
                        var s = new System.Text.StringBuilder($"    {item.m_shared.m_name}: ");
                        if (dmg.m_damage    > 0) s.Append($"damage={dmg.m_damage} ");
                        if (dmg.m_blunt     > 0) s.Append($"blunt={dmg.m_blunt} ");
                        if (dmg.m_slash     > 0) s.Append($"slash={dmg.m_slash} ");
                        if (dmg.m_pierce    > 0) s.Append($"pierce={dmg.m_pierce} ");
                        if (dmg.m_chop      > 0) s.Append($"chop={dmg.m_chop} ");
                        if (dmg.m_pickaxe   > 0) s.Append($"pickaxe={dmg.m_pickaxe} ");
                        if (dmg.m_fire      > 0) s.Append($"fire={dmg.m_fire} ");
                        if (dmg.m_frost     > 0) s.Append($"frost={dmg.m_frost} ");
                        if (dmg.m_lightning > 0) s.Append($"lightning={dmg.m_lightning} ");
                        if (dmg.m_poison    > 0) s.Append($"poison={dmg.m_poison} ");
                        if (dmg.m_spirit    > 0) s.Append($"spirit={dmg.m_spirit} ");
                        sb.AppendLine(s.ToString().TrimEnd());
                    }
                }

                sb.AppendLine("\n[BiomeLords] === End dump ===");
                var output = sb.ToString();
                Jotunn.Logger.LogInfo(output);
                Console.instance?.Print(output);
            }
        }

        // -----------------------------------------------------------------

        /// <summary>
        /// biomelords_dump_queen_attack — prints the exact assets used by vanilla
        /// SeekerQueen's attack items: the projectile prefab, its visual, launch
        /// effects (vfx+sfx), impact effects, and flight audio clips. The Seeker
        /// Lord's Acid Spit resolves and reuses these same assets at runtime
        /// (SeekerLordBrain.ResolveQueenAssets); run this to confirm what it found,
        /// or to verify whether the Queen even has a projectile attack in this
        /// game version (if not, the Lord falls back to its code-built acid blob).
        ///
        /// Usage: biomelords_dump_queen_attack
        /// </summary>
        private class DumpQueenAttackFx : ConsoleCommand
        {
            public override string Name    => "biomelords_dump_queen_attack";
            public override string Help    => "Dumps vanilla SeekerQueen attack-item projectile/trail prefab names to the log.";
            public override bool IsCheat   => true;
            public override bool IsNetwork => false;

            public override void Run(string[] args)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("[BiomeLords] === SeekerQueen attack FX dump ===");

                var go = PrefabManager.Instance.GetPrefab("SeekerQueen");
                if (go == null)
                {
                    sb.AppendLine("  SeekerQueen: PREFAB NOT FOUND");
                }
                else
                {
                    var humanoid = go.GetComponent<Humanoid>();
                    var itemSets = new System.Collections.Generic.List<ItemDrop.ItemData>();
                    void Collect(ItemDrop.ItemData item) { if (item != null) itemSets.Add(item); }
                    void CollectArray(UnityEngine.GameObject[] arr)
                    {
                        if (arr == null) return;
                        foreach (var go2 in arr)
                        {
                            if (go2 == null) continue;
                            var id2 = go2.GetComponent<ItemDrop>();
                            if (id2 != null) Collect(id2.m_itemData);
                        }
                    }
                    if (humanoid != null)
                    {
                        CollectArray(humanoid.m_defaultItems);
                        CollectArray(humanoid.m_randomWeapon);
                    }

                    foreach (var item in itemSets)
                    {
                        var attack = item?.m_shared?.m_attack;
                        if (attack == null) continue;
                        sb.AppendLine($"  Attack item: {item.m_shared.m_name}  (type={attack.m_attackType})");
                        sb.AppendLine($"    m_attackProjectile = {(attack.m_attackProjectile != null ? attack.m_attackProjectile.name : "(none)")}");

                        void DumpEffectList(string label, EffectList list)
                        {
                            if (list?.m_effectPrefabs == null || list.m_effectPrefabs.Length == 0) return;
                            foreach (var fx in list.m_effectPrefabs)
                                if (fx?.m_prefab != null)
                                    sb.AppendLine($"    {label} = {fx.m_prefab.name}");
                        }
                        DumpEffectList("m_trailStartEffect", attack.m_trailStartEffect);
                        DumpEffectList("m_hitEffect",        attack.m_hitEffect);
                        DumpEffectList("m_startEffect (launch vfx+sfx)", attack.m_startEffect);

                        // Drill into the projectile prefab itself — its visual,
                        // hit effects (impact vfx + sfx) and any spawn-on-hit.
                        if (attack.m_attackProjectile != null)
                        {
                            var pj = attack.m_attackProjectile.GetComponent<Projectile>();
                            if (pj != null)
                            {
                                sb.AppendLine($"    projectile.m_visual = {(pj.m_visual != null ? pj.m_visual.name : "(none)")}");
                                sb.AppendLine($"    projectile.m_spawnOnHit = {(pj.m_spawnOnHit != null ? pj.m_spawnOnHit.name : "(none)")}");
                                DumpEffectList("    projectile.m_hitEffects (impact vfx+sfx)", pj.m_hitEffects);
                                DumpEffectList("    projectile.m_spawnOnHitEffects",            pj.m_spawnOnHitEffects);
                            }
                        }
                    }

                    if (itemSets.Count == 0)
                        sb.AppendLine("  (no attack items found on SeekerQueen)");
                }

                sb.AppendLine("\n[BiomeLords] === End dump ===");
                var output = sb.ToString();
                Jotunn.Logger.LogInfo(output);
                Console.instance?.Print(output);
            }
        }
    }
}
