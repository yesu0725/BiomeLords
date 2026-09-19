using UnityEngine;
using BiomeLords.Config;
using BiomeLords.Phase1C;

namespace BiomeLords.Phase1D
{
    /// <summary>
    /// Gammeltroll Lord blessing — Fimbul Hide (SE_GammeltrollLordSpirit).
    ///
    /// A Deep North chore blessing in two parts, both driven here from the
    /// Player.Update tick rather than SE_Stats fields:
    ///   • <b>Sure-footed</b> — the deep-snow movement slow is switched off.
    ///     Vanilla computes it inline in Character.UpdateWalking from
    ///     <c>m_deepSnowSlowMax</c> (0.5 on the player = up to −50 % speed in
    ///     drifts); we zero that field while the blessing is active and put the
    ///     original back the moment it lapses.
    ///   • <b>Snow shedding</b> — every <see cref="ShedInterval"/> s, every building
    ///     piece within LordConfig.FimbulHideSnowShedRadius loses
    ///     <see cref="ShedPerTick"/> of its snow buildup (a full load clears in
    ///     ~20 s), so heavy snow never reaches the crushing threshold while you
    ///     are home. Only runs in the Deep North — nowhere else accumulates snow.
    /// </summary>
    public static class FimbulHideService
    {
        private const float ShedInterval = 2f;
        private const float ShedPerTick  = 0.10f;

        private static int   _seHash;
        private static bool  _applied;
        private static float _origSnowSlow = 0.5f;
        private static float _nextShed;

        public static void Tick()
        {
            if (_seHash == 0) _seHash = StatusEffectFactory.GammeltrollLordSpiritSE.GetStableHashCode();

            var p = Player.m_localPlayer;
            if (p == null || p.IsDead())
            {
                // A dead/absent player drops the field with the body; just forget our state.
                _applied = false;
                return;
            }

            bool blessed = p.GetSEMan() != null && p.GetSEMan().HaveStatusEffect(_seHash);

            if (blessed && !_applied)
            {
                _origSnowSlow = p.m_deepSnowSlowMax;
                p.m_deepSnowSlowMax = 0f;
                _applied = true;
                _nextShed = Time.time + 1f;
            }
            else if (!blessed && _applied)
            {
                p.m_deepSnowSlowMax = _origSnowSlow;
                _applied = false;
                return;
            }

            if (!_applied) return;
            if (Time.time < _nextShed) return;
            _nextShed = Time.time + ShedInterval;

            if (p.GetCurrentBiome() != Heightmap.Biome.DeepNorth) return;
            ShedSnowAround(p);
        }

        private static void ShedSnowAround(Player p)
        {
            float radius = LordConfig.FimbulHideSnowShedRadius?.Value ?? 30f;
            if (radius <= 0f) return;
            float sqr    = radius * radius;
            var   center = p.transform.position;

            int shed = 0;
            var all = WearNTear.GetAllInstances();
            for (int i = 0; i < all.Count; i++)
            {
                var w = all[i];
                if (w == null || w.m_snowBuildup <= 0f) continue;
                if ((w.transform.position - center).sqrMagnitude > sqr) continue;
                w.ChangeSnow(-ShedPerTick);
                shed++;
            }

            if (shed > 0 && LordConfig.DebugLogging != null && LordConfig.DebugLogging.Value)
                Jotunn.Logger.LogInfo($"[BiomeLords] Fimbul Hide: shed snow from {shed} piece(s).");
        }
    }
}
