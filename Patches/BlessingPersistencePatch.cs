using HarmonyLib;
using BiomeLords.Config;
using BiomeLords.Phase1C;
using BiomeLords.Util;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Blessing persistence — re-applies the player's active blessing on every
    /// spawn so it survives logout (and death). The active blessing SE name is
    /// stored in Player.m_customData (serialized with the character) by
    /// BlessingSystem; here we read it back and re-add the SE from the registry
    /// WITHOUT consuming any pedestal charges.
    ///
    /// Also reconciles the Featherweight inventory rows: by spawn time the load
    /// patch has pre-grown the inventory to a safe ceiling, so we set the final
    /// height (expanded if Featherweight is active, base otherwise) and crate any
    /// items left beyond it.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
    public static class Player_OnSpawned_BlessingPersistence
    {
        [HarmonyPostfix]
        public static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;

            var seName = BlessingSystem.GetActiveBlessing(__instance);
            if (!string.IsNullOrEmpty(seName) &&
                StatusEffectFactory.ByName.TryGetValue(seName, out var se) && se != null)
            {
                var seman = __instance.GetSEMan();
                if (seman != null && !seman.HaveStatusEffect(se.NameHash()))
                {
                    seman.AddStatusEffect(se, resetTime: true);
                    if (LordConfig.DebugLogging.Value)
                        Jotunn.Logger.LogInfo($"[BiomeLords] Re-applied persisted blessing {seName} on spawn.");
                }
            }

            // Match inventory height to blessing state (collapses extras if the
            // blessing is no longer Featherweight).
            FeatherweightInventory.Reconcile(__instance);
        }
    }
}
