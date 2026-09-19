using UnityEngine;
using BiomeLords.Config;
using BiomeLords.Util;
using BiomeLords.Phase1C;

namespace BiomeLords.Phase1D
{
    /// <summary>
    /// Gammeltroll Lord — Petrify (GP_Petrify).
    ///
    /// The Lord's own trick, turned on its foes. On activation the caster's skin
    /// turns to stone for LordConfig.PetrifyDuration seconds — SE_PetrifiedSkin
    /// (SubEffectFactory): very resistant to every damage type (−75 %),
    /// stagger-immune, −60 % move speed. When the shell lapses it shatters:
    /// LordConfig.PetrifyShatterDamage frost + a heavy knockback to every hostile
    /// within LordConfig.PetrifyShatterRadius.
    ///
    /// Driven from PowerEffectsService.Tick — the marker's absent → present
    /// transition arms the shell (same one-shot pattern as Rootward), and the
    /// shatter fires from this tick when the timer runs out. Dying with the shell
    /// up simply clears it.
    /// </summary>
    public static class PetrifyService
    {
        private static int _gpHash;
        private static int _skinHash;

        private static StatusEffect _lastSeenMarker;
        private static float _shatterAt = -1f;

        public static void Tick()
        {
            if (_gpHash   == 0) _gpHash   = GuardianPowerFactory.GammeltrollLordGP.GetStableHashCode();
            if (_skinHash == 0) _skinHash = SubEffectFactory.PetrifiedSkinSE.GetStableHashCode();

            var p = Player.m_localPlayer;
            if (p == null || p.IsDead()) { _lastSeenMarker = null; _shatterAt = -1f; return; }
            var seman = p.GetSEMan();
            if (seman == null) { _lastSeenMarker = null; _shatterAt = -1f; return; }

            // Pending shell? Fire when the timer lapses (or the skin SE is gone).
            if (_shatterAt > 0f && (Time.time >= _shatterAt || !seman.HaveStatusEffect(_skinHash)))
            {
                _shatterAt = -1f;
                seman.RemoveStatusEffect(_skinHash);
                Shatter(p);
            }

            var marker = seman.GetStatusEffect(_gpHash);
            if (marker == null) { _lastSeenMarker = null; return; }
            if (marker == _lastSeenMarker) return;
            _lastSeenMarker = marker;

            Activate(p, seman);
        }

        private static void Activate(Player p, SEMan seman)
        {
            float duration = Mathf.Max(1f, LordConfig.PetrifyDuration?.Value ?? 6f);

            if (SubEffectFactory.ByName.TryGetValue(SubEffectFactory.PetrifiedSkinSE, out var proto) && proto != null)
                SubEffectFactory.EnsureIcon(proto, "Stone");

            var skin = seman.AddStatusEffect(_skinHash, resetTime: true);
            if (skin != null) skin.m_ttl = duration;
            _shatterAt = Time.time + duration;

            var pos = p.transform.position + Vector3.up;
            FxLibrary.TrySpawn("fx_guardstone_activate",       pos);
            FxLibrary.TrySpawn("vfx_corpse_destruction_small", pos);

            p.Message(MessageHud.MessageType.Center, "$gp_petrify_activate");
            Jotunn.Logger.LogInfo($"[BiomeLords] Petrify: stone skin for {duration:F0}s.");
        }

        private static void Shatter(Player p)
        {
            float radius = Mathf.Max(1f, LordConfig.PetrifyShatterRadius?.Value ?? 8f);
            float frost  = Mathf.Max(0f, LordConfig.PetrifyShatterDamage?.Value ?? 80f);
            float sqr    = radius * radius;
            var   center = p.transform.position;

            FxLibrary.TrySpawn("vfx_trollsnow_groundslam", center);
            FxLibrary.TrySpawn("vfx_TrollFrost_Death",     center + Vector3.up);
            FxLibrary.TrySpawn("fx_himminafl_aoe",         center);
            for (int i = 0; i < 10; i++)
            {
                var off = Quaternion.Euler(0f, i * 36f, 0f) * Vector3.forward * 2f;
                FxLibrary.TrySpawn("vfx_HitSparks", center + off + Vector3.up * 0.5f);
            }

            int struck = 0;
            var all = Character.GetAllCharacters();
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || c.IsDead() || c is Player || c.IsTamed()) continue;
                if (!BaseAI.IsEnemy(p, c)) continue;
                var delta = c.transform.position - center;
                if (delta.sqrMagnitude > sqr) continue;

                var hit = new HitData();
                hit.m_damage.m_frost = frost;
                hit.m_point          = c.transform.position + Vector3.up;
                hit.m_dir            = delta.sqrMagnitude > 0.01f ? delta.normalized : Vector3.forward;
                hit.m_pushForce      = 120f;
                hit.m_hitType        = HitData.HitType.PlayerHit;
                hit.SetAttacker(p);
                c.Damage(hit);
                struck++;
            }

            p.Message(MessageHud.MessageType.Center, "$gp_petrify_shatter");
            Jotunn.Logger.LogInfo($"[BiomeLords] Petrify: shell shattered, {struck} foe(s) struck for {frost:F0} frost.");
        }
    }
}
