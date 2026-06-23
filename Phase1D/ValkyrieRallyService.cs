using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Jotunn.Managers;
using BiomeLords.Util;
using BiomeLords.Config;
using BiomeLords.Phase1C;

namespace BiomeLords.Phase1D
{
    /// <summary>
    /// Fallen Valkyrie Lord — Valkyrie's Rally (GP_ValkyrieAscension).
    ///
    /// A one-shot group support burst. The instant the local player activates
    /// the power we broadcast a routed RPC to every client. Each client checks
    /// whether *its own* local player stands within the rally radius of the
    /// caster and, if so, restores itself in full:
    ///   • HP, Stamina and Eitr to maximum
    ///   • Adrenaline to maximum — only if a trinket is equipped
    ///   • A max-level StaffShield bubble (refreshed if one is already up)
    ///   • A 20-minute Rested buff
    ///
    /// Routing the effect through an RPC (rather than poking remote Player
    /// objects directly) is what makes the heal actually land on other clients
    /// in multiplayer — each player applies the restore to themselves.
    /// </summary>
    public static class ValkyrieRallyService
    {
        public const string RpcName = "BiomeLords_ValkyrieRally";

        private static int _gpHash;

        // One-shot guard: only fire on the marker absent → present transition.
        private static StatusEffect _lastSeenMarker;

        // Reflected access to the (private) equipped-trinket slot.
        private static FieldInfo _trinketField;

        // Cached StaffShield data, resolved once ObjectDB is up.
        private static bool _shieldResolved;
        private static int  _shieldHash;
        private static int  _shieldItemLevel = 1;

        // ---- RPC registration (per world join) -----------------------------

        private static ZRoutedRpc _registeredOn;

        /// <summary>Register the routed RPC on the current ZRoutedRpc instance.
        /// Called from a Game.Start postfix on every client.</summary>
        public static void RegisterRpc()
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc == null || rpc == _registeredOn) return;
            rpc.Register<Vector3>(RpcName, OnRallyRpc);
            _registeredOn = rpc;
            Jotunn.Logger.LogInfo("[BiomeLords] Valkyrie's Rally RPC registered.");
        }

        // ---- Activation detection (driven from PowerEffectsService.Tick) ----

        public static void Tick()
        {
            if (_gpHash == 0) _gpHash = GuardianPowerFactory.FallerValkyrieLordGP.GetStableHashCode();

            var p = Player.m_localPlayer;
            if (p == null || p.IsDead()) { _lastSeenMarker = null; return; }
            var seman = p.GetSEMan();
            if (seman == null) { _lastSeenMarker = null; return; }

            var marker = seman.GetStatusEffect(_gpHash);
            if (marker == null) { _lastSeenMarker = null; return; }

            // Same marker instance we already handled? Wait for it to lapse.
            if (marker == _lastSeenMarker) return;
            _lastSeenMarker = marker;

            BroadcastRally(p);
        }

        private static void BroadcastRally(Player caster)
        {
            var pos = caster.transform.position;

            // Cinematic burst at the caster so everyone sees the rally go off.
            // (No "fx_summon_start" — that's the green summon burst.)
            FxLibrary.TrySpawn("fx_himminafl_aoe", pos + Vector3.up * 0.5f);
            FxLibrary.TrySpawn("vfx_lootspawn",    pos + Vector3.up * 1.2f);

            var rpc = ZRoutedRpc.instance;
            if (rpc != null)
            {
                // ZRoutedRpc.Everybody includes the caster, so the caster is
                // restored by the same path as everyone else.
                rpc.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcName, pos);
            }
            else
            {
                // No networking context (shouldn't happen in a live world) —
                // at least restore the caster locally.
                ApplyRally(caster);
            }

            Jotunn.Logger.LogInfo("[BiomeLords] Valkyrie's Rally broadcast from " +
                                  $"{caster.GetPlayerName()} at {pos}.");
        }

        // ---- RPC receiver (runs on every client) ---------------------------

        private static void OnRallyRpc(long sender, Vector3 center)
        {
            var p = Player.m_localPlayer;
            if (p == null || p.IsDead()) return;

            float radius = LordConfig.ValkyrieRallyRadius.Value;
            if ((p.transform.position - center).sqrMagnitude > radius * radius) return;

            ApplyRally(p);
        }

        // ---- The restore itself --------------------------------------------

        private static void ApplyRally(Player p)
        {
            var seman = p.GetSEMan();
            if (seman == null) return;

            // Full HP / Stamina / Eitr.
            float missingHp = p.GetMaxHealth() - p.GetHealth();
            if (missingHp > 0f) p.Heal(missingHp, true);
            p.AddStamina(p.GetMaxStamina());
            p.AddEitr(p.GetMaxEitr());

            // Max Adrenaline — only when a trinket is equipped. AddAdrenaline
            // clamps to GetMaxAdrenaline(), so passing the max tops it off.
            if (HasTrinket(p))
                p.AddAdrenaline(p.GetMaxAdrenaline());

            GrantMaxStaffShield(seman);
            GrantRested(seman);

            FxLibrary.TrySpawn("vfx_HitSparks", p.transform.position + Vector3.up * 0.5f);
            p.Message(MessageHud.MessageType.Center, "$gp_valkyrieascension_rallied");
        }

        private static bool HasTrinket(Player p)
        {
            if (_trinketField == null)
                _trinketField = AccessTools.Field(typeof(Humanoid), "m_trinketItem");
            if (_trinketField == null) return false;
            return _trinketField.GetValue(p) != null;
        }

        /// <summary>Wrap the player in the StaffShield's bubble at max Blood
        /// Magic skill (100) and the staff's max upgrade level. Any shield
        /// already up is removed first so the absorb pool is refreshed to full.</summary>
        private static void GrantMaxStaffShield(SEMan seman)
        {
            EnsureShieldData();
            if (!_shieldResolved || _shieldHash == 0) return;

            seman.RemoveStatusEffect(_shieldHash, true);
            // The 4-arg overload runs Setup + SetLevel(itemLevel, skillLevel)
            // on a fresh clone pulled from ObjectDB, so the absorb scales to a
            // skill-100, max-quality StaffShield.
            seman.AddStatusEffect(_shieldHash, true, _shieldItemLevel, 100f);
        }

        private static void EnsureShieldData()
        {
            if (_shieldResolved) return;
            _shieldResolved = true;

            var staff = PrefabManager.Instance.GetPrefab("StaffShield")?.GetComponent<ItemDrop>();
            var shared = staff?.m_itemData?.m_shared;
            var shieldSe = shared?.m_attackStatusEffect;
            if (shieldSe == null)
            {
                Jotunn.Logger.LogWarning("[BiomeLords] Valkyrie's Rally: StaffShield " +
                                         "status effect not found — shield half disabled.");
                return;
            }
            _shieldHash      = shieldSe.name.GetStableHashCode();
            _shieldItemLevel = Mathf.Max(1, shared.m_maxQuality);
        }

        /// <summary>Grant a fixed-duration Rested buff, refreshing any existing one.</summary>
        private static void GrantRested(SEMan seman)
        {
            int restedHash = "Rested".GetStableHashCode();
            seman.RemoveStatusEffect(restedHash, true);
            var rested = seman.AddStatusEffect(restedHash, true);
            if (rested != null)
                rested.m_ttl = LordConfig.ValkyrieRallyRestedSeconds.Value;
            else
                Jotunn.Logger.LogWarning("[BiomeLords] Valkyrie's Rally: vanilla 'Rested' " +
                                         "status effect not found — rested half skipped.");
        }
    }
}
