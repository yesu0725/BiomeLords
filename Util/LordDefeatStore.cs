using BiomeLords.Config;
using BiomeLords.Data;

namespace BiomeLords.Util
{
    /// <summary>
    /// Tracks which Biome Lords have been killed and exposes the highest defeated tier
    /// for scaling purposes.
    ///
    /// Storage mode (controlled by LordConfig.GlobalLordDefeats):
    ///   false (default) — per-player unique keys on Player.m_localPlayer.
    ///                     Each player's scaling is independent.
    ///   true            — ZoneSystem global keys shared across the whole world.
    ///                     Any Lord kill raises scaling for every player on the server.
    ///
    /// Key format: "biomelords_defeated_<lordId>"  (e.g. "biomelords_defeated_neck_lord")
    ///
    /// Written when a Lord dies (KillTrackerPatch.OnLordDeath).
    /// Read at summon time (SummonService) and at vanilla boss spawn (VanillaBossScalingPatch).
    /// </summary>
    public static class LordDefeatStore
    {
        private const string KeyPrefix = "biomelords_defeated_";

        public static void RecordDefeat(string lordId)
        {
            if (string.IsNullOrEmpty(lordId)) return;

            if (LordConfig.GlobalLordDefeats != null && LordConfig.GlobalLordDefeats.Value)
            {
                if (ZoneSystem.instance == null) return;
                ZoneSystem.instance.SetGlobalKey(KeyPrefix + lordId);
                Jotunn.Logger.LogInfo($"[BiomeLords] Recorded Lord defeat: {lordId} (global key — all players affected).");
            }
            else
            {
                var player = Player.m_localPlayer;
                if (player == null) return;
                player.AddUniqueKey(KeyPrefix + lordId);
                Jotunn.Logger.LogInfo($"[BiomeLords] Recorded Lord defeat: {lordId} (player key — {player.GetPlayerName()} only).");
            }
        }

        /// <summary>
        /// Returns the tier of the highest-tier Lord that has been killed,
        /// according to the current storage mode. Returns 0 if none.
        /// </summary>
        public static int HighestDefeatedTier()
        {
            int highest = 0;

            if (LordConfig.GlobalLordDefeats != null && LordConfig.GlobalLordDefeats.Value)
            {
                if (ZoneSystem.instance == null) return 0;
                foreach (var lord in LordRegistry.All)
                {
                    if (ZoneSystem.instance.GetGlobalKey(KeyPrefix + lord.Id) && lord.Tier > highest)
                        highest = lord.Tier;
                }
            }
            else
            {
                var player = Player.m_localPlayer;
                if (player == null) return 0;
                foreach (var lord in LordRegistry.All)
                {
                    if (player.HaveUniqueKey(KeyPrefix + lord.Id) && lord.Tier > highest)
                        highest = lord.Tier;
                }
            }

            return highest;
        }
    }
}
