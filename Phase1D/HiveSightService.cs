using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using BiomeLords.Util;
using BiomeLords.Phase1C;

namespace BiomeLords.Phase1D
{
    /// <summary>
    /// Seeker Lord — Hive Sight Forsaken Power driver.
    ///
    /// While GP_HiveSense is active on the local player, every second we
    /// scan all Characters within 80 m, drop a red Minimap pin on each
    /// hostile (the "see through walls" effect — the minimap is the
    /// reliable always-visible surface) and pulse a small spark above
    /// their head so they read in-world when in line of sight.
    ///
    /// Pins are tracked per-character and cleaned up when the creature
    /// dies, leaves range, or the GP expires.
    /// </summary>
    public static class HiveSightService
    {
        public const float Range          = 80f;
        public const float RefreshInterval = 1.0f;
        public const float PulseInterval   = 1.5f;

        private static int _gpHash;
        private static float _nextRefresh;
        private static float _nextPulse;

        // Tracked: hostile Character → its Minimap pin.
        private static readonly Dictionary<Character, Minimap.PinData> Pins =
            new Dictionary<Character, Minimap.PinData>();

        // Resolved reflectively to dodge Splatform dependency in newer overloads.
        private static MethodInfo _addPinMethod;
        private static bool       _addPinResolved;

        public static void Tick()
        {
            if (_gpHash == 0) _gpHash = GuardianPowerFactory.SeekerLordGP.GetStableHashCode();

            var p = Player.m_localPlayer;
            if (p == null || p.IsDead())
            {
                ClearAll();
                return;
            }
            var seman = p.GetSEMan();
            bool active = seman != null && seman.HaveStatusEffect(_gpHash);
            if (!active)
            {
                ClearAll();
                return;
            }

            if (Time.time >= _nextRefresh)
            {
                _nextRefresh = Time.time + RefreshInterval;
                RefreshPins(p);
            }
            if (Time.time >= _nextPulse)
            {
                _nextPulse = Time.time + PulseInterval;
                PulseSparks(p);
            }
        }

        private static void RefreshPins(Player p)
        {
            var center = p.transform.position;
            float sqr  = Range * Range;
            var map    = Minimap.instance;
            if (map == null) return;

            // Add pins for any new in-range hostile; refresh existing.
            var all = Character.GetAllCharacters();
            var seen = new HashSet<Character>();
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (!IsRelevantHostile(c, p)) continue;
                if ((c.transform.position - center).sqrMagnitude > sqr) continue;
                seen.Add(c);

                if (Pins.TryGetValue(c, out var pin))
                {
                    // Refresh position.
                    pin.m_pos = c.transform.position;
                }
                else
                {
                    var fresh = AddPinViaReflection(map, c.transform.position);
                    if (fresh != null) Pins[c] = fresh;
                }
            }

            // Remove pins for anything that left range, died, or was destroyed.
            var stale = new List<Character>();
            foreach (var kv in Pins)
            {
                var c = kv.Key;
                if (c == null || c.IsDead() || !seen.Contains(c)) stale.Add(c);
            }
            for (int i = 0; i < stale.Count; i++)
            {
                var c = stale[i];
                if (Pins.TryGetValue(c, out var pin) && pin != null)
                    map.RemovePin(pin);
                Pins.Remove(c);
            }
        }

        private static void PulseSparks(Player p)
        {
            var center = p.transform.position;
            float sqr  = Range * Range;
            var all = Character.GetAllCharacters();
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (!IsRelevantHostile(c, p)) continue;
                if ((c.transform.position - center).sqrMagnitude > sqr) continue;
                FxLibrary.TrySpawn("vfx_HitSparks", c.transform.position + Vector3.up * 1.4f);
            }
        }

        private static bool IsRelevantHostile(Character c, Player p)
        {
            if (c == null || c.IsDead()) return false;
            if (c == p) return false;
            if (c.IsTamed()) return false;
            if (c.IsPlayer()) return false;
            return c.GetBaseAI() is MonsterAI || c.GetBaseAI() != null;
        }

        /// <summary>
        /// Pick the smallest-arity AddPin overload we can find that has the
        /// shape (Vector3, PinType, string, bool, bool, ...). Fill any extra
        /// args with defaults. Avoids compile-time binding to a newer Valheim
        /// signature that pulls a Splatform reference.
        /// </summary>
        private static Minimap.PinData AddPinViaReflection(Minimap map, Vector3 pos)
        {
            if (!_addPinResolved)
            {
                _addPinResolved = true;
                MethodInfo best = null;
                int bestArgs = int.MaxValue;
                foreach (var m in typeof(Minimap).GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (m.Name != "AddPin") continue;
                    var ps = m.GetParameters();
                    if (ps.Length < 5) continue;
                    if (ps[0].ParameterType != typeof(Vector3)) continue;
                    if (ps[2].ParameterType != typeof(string))  continue;
                    if (ps.Length < bestArgs) { best = m; bestArgs = ps.Length; }
                }
                _addPinMethod = best;
            }
            if (_addPinMethod == null) return null;

            var paramInfos = _addPinMethod.GetParameters();
            var args = new object[paramInfos.Length];
            args[0] = pos;
            args[1] = Minimap.PinType.EventArea;
            args[2] = "";
            args[3] = false; // save
            args[4] = false; // isChecked
            for (int i = 5; i < args.Length; i++)
            {
                var pt = paramInfos[i].ParameterType;
                if (paramInfos[i].HasDefaultValue) args[i] = paramInfos[i].DefaultValue;
                else if (pt.IsValueType)            args[i] = System.Activator.CreateInstance(pt);
                else                                args[i] = null;
            }
            try { return _addPinMethod.Invoke(map, args) as Minimap.PinData; }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogWarning($"[BiomeLords] HiveSight AddPin failed: {ex.Message}");
                return null;
            }
        }

        public static void ClearAll()
        {
            var map = Minimap.instance;
            if (map != null)
            {
                foreach (var kv in Pins)
                    if (kv.Value != null) map.RemovePin(kv.Value);
            }
            Pins.Clear();
        }
    }
}
