using UnityEngine;
using Jotunn.Managers;
using BiomeLords.Util;

namespace BiomeLords.Phase1D
{
    /// <summary>
    /// Per-instance behaviour for the Lox Lord (Plains, tier 5). No minion
    /// summons — a Lox is a solitary tank — vanilla Lox already covers the
    /// offense, so this brain is all defense plus two panic buttons:
    ///   • Bone Bulwark — reactive ward: pops for a few seconds whenever a
    ///     player is in melee range (on cooldown), mitigating incoming
    ///     damage and shrugging off stagger while it holds.
    ///   • Unyielding Bulwark — one-shot at 40 % HP: roots in place for 5 s,
    ///     mitigating 80 % of incoming damage while healing back a chunk of
    ///     its max HP (Second Wind + Bulwark Stance, combined).
    ///   • Roaring Bellow — one-shot at 50 % HP: 15 m stagger/push wave.
    ///   • Rage at 30 % HP — +50 % speed, aura swap to crimson.
    /// </summary>
    public class LoxLordBrain : MonoBehaviour
    {
        private const float BellowHpFraction     = 0.50f;
        private const float BellowRadius         = 15f;
        private const float BellowPushForce      = 100f;

        private const float RageHpFraction       = 0.30f;
        private const float RageSpeedFactor      = 1.5f;

        private const float ShieldDetectRadius   = 5f;    // melee range
        private const float ShieldDuration       = 2.5f;
        private const float ShieldInterCooldown  = 18f;

        private const float UnyieldingHpFraction = 0.40f;
        private const float UnyieldingDuration   = 5f;
        private const float UnyieldingHealFrac   = 0.20f;  // total heal over the window, % of max HP

        private Character _character;
        private ZNetView  _nview;

        private float _baseSpeed, _baseRunSpeed;
        private bool  _bellowFired;
        private bool  _raging;
        private float _nextRagePulse;

        private float _nextShieldTime;
        private float _shieldEndTime;

        private bool  _unyieldingFired;
        private float _unyieldingEndTime;
        private float _unyieldingHealPerSecond;

        /// <summary>True while Bone Bulwark is up — read by LoxLordShieldPatch.</summary>
        public bool IsShielded { get; private set; }

        /// <summary>True during the one-time Unyielding Bulwark window — read by LoxLordShieldPatch.</summary>
        public bool IsLastStand { get; private set; }

        private void Awake()
        {
            _character = GetComponent<Character>();
            _nview     = GetComponent<ZNetView>();
            if (_character != null)
            {
                _baseSpeed    = _character.m_speed;
                _baseRunSpeed = _character.m_runSpeed;
            }
            _nextShieldTime = Time.time + 6f; // brief grace before first shield
        }

        private void Update()
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner()) return;
            if (_character == null || _character.IsDead()) return;

            TryRoaringBellow();
            TryRage();
            TryUnyieldingBulwark();
            TickUnyieldingBulwark(); // re-asserts the root after TryRage, in case both fire same frame
            TryShield();
            TickShield();
            TickRageAura();
        }

        // ---- Abilities -----------------------------------------------------

        private void TryRoaringBellow()
        {
            if (_bellowFired) return;
            float frac = _character.GetHealth() / _character.GetMaxHealth();
            if (frac > BellowHpFraction) return;
            _bellowFired = true;

            var center = transform.position + Vector3.up * 1f;

            FxLibrary.TrySpawn("fx_Fader_Roar",            center);
            FxLibrary.TrySpawn("fx_himminafl_aoe",         center);
            FxLibrary.TrySpawn("vfx_corpse_destruction_small", center);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f;
                var off = Quaternion.Euler(0f, a, 0f) * Vector3.forward * 2.5f;
                FxLibrary.TrySpawn("vfx_HitSparks", center + off);
            }

            // 15 m blast — knock everyone back, light blunt.
            float sqr = BellowRadius * BellowRadius;
            var all = Character.GetAllCharacters();
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (!(c is Player p) || p.IsDead()) continue;
                Vector3 toward = p.transform.position - transform.position;
                if (toward.sqrMagnitude > sqr) continue;

                var hit = new HitData();
                hit.m_pushForce      = BellowPushForce;
                hit.m_point          = p.transform.position;
                hit.m_dir            = toward.normalized;
                hit.m_hitType        = HitData.HitType.EnemyHit;
                hit.m_blockable      = true;
                hit.m_dodgeable      = true;
                hit.SetAttacker(_character);
                p.Damage(hit);
            }

            var local = Player.m_localPlayer;
            if (local != null) local.Message(MessageHud.MessageType.Center, "$biomelords_lox_bellow");
            Jotunn.Logger.LogInfo("[BiomeLords] Lox Lord released Roaring Bellow.");
        }

        private void TryRage()
        {
            if (_raging) return;
            float frac = _character.GetHealth() / _character.GetMaxHealth();
            if (frac > RageHpFraction) return;
            _raging = true;

            _character.m_speed    = _baseSpeed    * RageSpeedFactor;
            _character.m_runSpeed = _baseRunSpeed * RageSpeedFactor;

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
            Jotunn.Logger.LogInfo("[BiomeLords] Lox Lord entered Rage.");
        }

        private void TryUnyieldingBulwark()
        {
            if (_unyieldingFired) return;
            float frac = _character.GetHealth() / _character.GetMaxHealth();
            if (frac > UnyieldingHpFraction) return;
            _unyieldingFired = true;
            IsLastStand       = true;
            _unyieldingEndTime = Time.time + UnyieldingDuration;
            _unyieldingHealPerSecond = (_character.GetMaxHealth() * UnyieldingHealFrac) / UnyieldingDuration;

            _character.m_speed    = 0f;
            _character.m_runSpeed = 0f;

            var pos = transform.position + Vector3.up * 1.2f;
            FxLibrary.TrySpawn("fx_guardstone_activate",        pos);
            FxLibrary.TrySpawn("vfx_corpse_destruction_small",  pos);

            var local = Player.m_localPlayer;
            if (local != null) local.Message(MessageHud.MessageType.Center, "$biomelords_lox_bulwark");
            Jotunn.Logger.LogInfo("[BiomeLords] Lox Lord dug in for Unyielding Bulwark.");
        }

        private void TickUnyieldingBulwark()
        {
            if (!IsLastStand) return;

            // Hold the root every frame — Rage firing mid-window must not undo it.
            _character.m_speed    = 0f;
            _character.m_runSpeed = 0f;

            if (_character.GetHealth() < _character.GetMaxHealth())
                _character.Heal(_unyieldingHealPerSecond * Time.deltaTime, false);

            if (Time.time < _unyieldingEndTime) return;

            IsLastStand            = false;
            _character.m_speed    = _raging ? _baseSpeed    * RageSpeedFactor : _baseSpeed;
            _character.m_runSpeed = _raging ? _baseRunSpeed * RageSpeedFactor : _baseRunSpeed;
        }

        private void TryShield()
        {
            if (IsLastStand || IsShielded || Time.time < _nextShieldTime) return;
            var target = Player.m_localPlayer;
            if (target == null || target.IsDead()) return;
            if ((target.transform.position - transform.position).sqrMagnitude >
                ShieldDetectRadius * ShieldDetectRadius) return;

            IsShielded     = true;
            _shieldEndTime = Time.time + ShieldDuration;
            _nextShieldTime = _shieldEndTime + (_raging ? ShieldInterCooldown * 0.6f : ShieldInterCooldown);

            FxLibrary.TrySpawn("fx_guardstone_activate", transform.position + Vector3.up * 1.2f);
            Jotunn.Logger.LogInfo("[BiomeLords] Lox Lord raised Bone Bulwark.");
        }

        private void TickShield()
        {
            if (!IsShielded) return;
            if (Time.time >= _shieldEndTime) IsShielded = false;
        }

        private void TickRageAura()
        {
            if (!_raging) return;
            if (Time.time < _nextRagePulse) return;
            _nextRagePulse = Time.time + 0.5f;
            var pos = transform.position + Vector3.up * 1.0f;
            FxLibrary.TrySpawn("vfx_HitSparks", pos);
            if (Random.value < 0.4f) FxLibrary.TrySpawn("fx_redlightning_burst", pos + Vector3.up * 0.3f);
        }
    }
}
