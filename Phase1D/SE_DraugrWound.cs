using UnityEngine;
using BiomeLords.Util;

namespace BiomeLords.Phase1D
{
    /// <summary>
    /// "Wounded" debuff applied by the Draugr Lord's Rotten Cleave. Bleeds the
    /// victim for a small amount each tick over a short duration and spawns a
    /// blood splatter on them every tick so they visibly look wounded.
    ///
    /// The bleed uses the generic <c>m_damage</c> channel with NO attacker set,
    /// so it ignores armor (a true bleed) and LordDamageBoostPatch leaves it
    /// alone (the patch early-returns on a null attacker).
    ///
    /// Applied directly as a prototype via SEMan.AddStatusEffect — it does not
    /// need to live in ObjectDB.
    /// </summary>
    public class SE_DraugrWound : StatusEffect
    {
        public float DamagePerTick = 5f;
        public float TickInterval  = 1f;

        private float _nextTick;

        public override void Setup(Character character)
        {
            base.Setup(character);
            _nextTick = TickInterval;
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);
            if (m_character == null || m_character.IsDead()) return;
            if (m_time < _nextTick) return;
            _nextTick = m_time + TickInterval;

            // Bleed tick — null attacker so the lord-damage patch ignores it and
            // armor is bypassed; the player just slowly loses HP.
            var hit = new HitData();
            hit.m_damage.m_damage = DamagePerTick;
            hit.m_point           = m_character.transform.position + Vector3.up;
            hit.m_hitType         = HitData.HitType.EnemyHit;
            m_character.Damage(hit);

            // Visible wound — blood splatter on the victim every tick.
            FxLibrary.TrySpawnFirst(
                new[] { "vfx_BloodHit", "vfx_player_hit_blood", "vfx_HitSparks" },
                m_character.transform.position + Vector3.up * 1.0f);
        }
    }
}
