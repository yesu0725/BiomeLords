using HarmonyLib;
using UnityEngine;
using Jotunn.Managers;
using BiomeLords.Config;
using BiomeLords.Data;
using BiomeLords.Util;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Tracks creature kills against the local player.
    ///
    /// We patch Character.Damage as a prefix that captures whether the target was
    /// alive before the hit, and a postfix that, if it just died, attributes the
    /// kill to the HitData's attacker. This is more reliable than patching
    /// OnDeath because HitData carries the attacker reference directly.
    ///
    /// Only the local player's counter is updated to avoid double-counting in MP.
    /// Prefab match uses gameObject.name with "(Clone)" stripped, because
    /// Character.m_name is a localization token (e.g. "$enemy_neck").
    /// </summary>
    [HarmonyPatch(typeof(Character), "Damage")]
    public static class Character_Damage_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(Character __instance, out bool __state)
        {
            __state = __instance != null && __instance.GetHealth() > 0f;
        }

        [HarmonyPostfix]
        public static void Postfix(Character __instance, HitData hit, bool __state)
        {
            if (!LordConfig.EnableKillTracking.Value) return;
            if (__instance == null || hit == null) return;
            if (__instance is Player) return;          // ignore player deaths
            if (!__state) return;                       // was already dead before this hit
            if (__instance.GetHealth() > 0f) return;    // didn't die from this hit

            string prefabName = StripClone(__instance.gameObject.name);

            // If a Lord just died: end event, play a death sequence, reset kills.
            if (RegisteredLords.IsLord(__instance))
            {
                OnLordDeath(__instance, hit);
            }

            var lord = LordRegistry.FindByKillTarget(prefabName);
            if (lord == null) return;

            var attacker = hit.GetAttacker() as Player;
            if (attacker == null) return;
            if (attacker != Player.m_localPlayer) return;

            int total = KillStore.Increment(attacker, prefabName);

            if (LordConfig.DebugLogging.Value)
            {
                int progress = KillStore.SumFor(attacker, lord.KillTargets);
                int req = LordConfig.KillRequirement(lord.Id);
                Jotunn.Logger.LogInfo(
                    $"[BiomeLords] {attacker.GetPlayerName()} killed {prefabName} " +
                    $"({total} of this type). {lord.DisplayName}: {progress}/{req}.");
            }
        }

        /// <summary>
        /// Death sequence: end world event, reset the kill counter on the killing
        /// player, and play a multi-layered VFX burst.
        /// </summary>
        private static void OnLordDeath(Character lord, HitData hit)
        {
            var pos = lord.transform.position;

            // 1. End the event tied to this Lord.
            var lordEvent = RegisteredLords.EventFor(lord);
            if (lordEvent != null)
            {
                var sys = RandEventSystem.instance;
                var current = sys?.GetCurrentRandomEvent();
                if (current != null && current.m_name == lordEvent)
                {
                    sys.ResetRandomEvent();
                    if (LordConfig.DebugLogging.Value)
                        Jotunn.Logger.LogInfo($"[BiomeLords] Ended event {lordEvent} on Lord death.");
                }
            }

            // 2. Reset kill counts AND record the kill as a unique-key so the
            //    player can claim this Lord's Forsaken Power at a pedestal.
            var killer = hit?.GetAttacker() as Player;
            if (killer != null && killer == Player.m_localPlayer)
            {
                var lordId = RegisteredLords.LordIdFor(lord);
                var def = lordId != null ? LordRegistry.ById(lordId) : null;
                if (def != null)
                {
                    killer.AddUniqueKey(BiomeLords.Phase1C.PowerClaimSystem.DefeatKey(def.Id));
                    LordDefeatStore.RecordDefeat(def.Id);

                    foreach (var t in def.KillTargets) KillStore.Set(killer, t, 0);
                    killer.Message(MessageHud.MessageType.Center,
                        $"The hunt resets. Prove yourself again to summon the {def.DisplayName}.");

                    // Auto-award this Lord's Forsaken Power (replaces current GP).
                    BiomeLords.Phase1C.PowerClaimSystem.GrantOnDefeat(killer, def.Id);

                    if (LordConfig.DebugLogging.Value)
                        Jotunn.Logger.LogInfo($"[BiomeLords] Reset kill counters for {def.Id}, recorded defeat, awarded GP.");
                }
            }

            // Per-Lord themed death FX (forest roots for Greydwarf, water splash for Neck, etc.)
            var lordIdForFx = RegisteredLords.LordIdFor(lord) ?? "neck_lord";
            BiomeLords.Phase1B.LordFx.PlayDeath(lordIdForFx, pos);
        }

        private static string StripClone(string n)
        {
            if (string.IsNullOrEmpty(n)) return n;
            const string suffix = "(Clone)";
            return n.EndsWith(suffix) ? n.Substring(0, n.Length - suffix.Length) : n;
        }
    }
}
