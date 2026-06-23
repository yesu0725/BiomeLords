using System.Reflection;
using HarmonyLib;
using BiomeLords.Phase1C;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Pack Whisperer breed-speed half. Tamed Wolves / Wolf Cubs within 30 m
    /// of a player who has SE_FenringLordSpirit active have their procreation
    /// rolled TWICE per vanilla tick instead of once — effectively doubling
    /// the breeding rate.
    ///
    /// Vanilla Procreation invokes a private parameterless method named
    /// "Procreate" on m_updateInterval via InvokeRepeating. We postfix that
    /// method and, when conditions match, re-invoke Procreate once more.
    /// A thread-static re-entry guard prevents infinite recursion.
    ///
    /// Uses [HarmonyTargetMethod] returning AccessTools.Method so that if a
    /// future Valheim build renames or removes the method, the patch silently
    /// no-ops instead of throwing during Plugin.Awake's PatchAll pass.
    /// </summary>
    [HarmonyPatch]
    public static class Procreation_Procreate_PackBreed
    {
        private const float Range = 30f;

        private static int _seHash;

        [System.ThreadStatic] private static bool _reentering;

        private static MethodInfo _procreateMethod;

        [HarmonyTargetMethod]
        public static MethodBase Target()
        {
            // Returns null if missing — Harmony skips the patch without raising.
            var m = AccessTools.Method(typeof(Procreation), "Procreate");
            _procreateMethod = m;
            return m;
        }

        [HarmonyPostfix]
        public static void Postfix(Procreation __instance)
        {
            if (_reentering) return;
            if (__instance == null) return;
            if (_procreateMethod == null) return;
            if (!IsBreedableWolf(__instance)) return;

            var p = Player.m_localPlayer;
            if (p == null || p.IsDead()) return;
            if ((p.transform.position - __instance.transform.position).sqrMagnitude > Range * Range) return;

            if (_seHash == 0) _seHash = StatusEffectFactory.FenringLordSpiritSE.GetStableHashCode();
            var seman = p.GetSEMan();
            if (seman == null || !seman.HaveStatusEffect(_seHash)) return;

            try
            {
                _reentering = true;
                _procreateMethod.Invoke(__instance, null);
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogWarning($"[BiomeLords] PackBreed re-invoke failed: {ex.Message}");
            }
            finally
            {
                _reentering = false;
            }
        }

        private static bool IsBreedableWolf(Procreation proc)
        {
            var c = proc.GetComponent<Character>();
            if (c == null || c.IsDead() || !c.IsTamed()) return false;
            return c.gameObject.name.StartsWith("Wolf");
        }
    }

    // -----------------------------------------------------------------

    /// <summary>
    /// Pack Whisperer taming acceleration.  When the local player has the
    /// SE_FenringLordSpirit blessing active and is within 30 m, each
    /// TamingUpdate tick counts twice — the same doubling pattern used by
    /// Procreation_Procreate_PackBreed for breeding.
    /// </summary>
    [HarmonyPatch]
    public static class Tameable_DecreaseRemainingTime_PackWhisperer
    {
        private const float Range = 30f;

        private static int        _seHash;
        private static MethodInfo _decreaseMethod;

        [System.ThreadStatic] private static bool _reentering;

        [HarmonyTargetMethod]
        public static MethodBase Target()
        {
            var m = AccessTools.Method(typeof(Tameable), "DecreaseRemainingTime");
            _decreaseMethod = m;
            return m;
        }

        [HarmonyPostfix]
        public static void Postfix(Tameable __instance, float time)
        {
            if (_reentering) return;
            if (__instance == null || _decreaseMethod == null) return;
            if (__instance.IsTamed()) return;

            var p = Player.m_localPlayer;
            if (p == null || p.IsDead()) return;
            if ((p.transform.position - __instance.transform.position).sqrMagnitude > Range * Range) return;

            if (_seHash == 0) _seHash = StatusEffectFactory.FenringLordSpiritSE.GetStableHashCode();
            var seman = p.GetSEMan();
            if (seman == null || !seman.HaveStatusEffect(_seHash)) return;

            try
            {
                _reentering = true;
                _decreaseMethod.Invoke(__instance, new object[] { time });
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogWarning($"[BiomeLords] TamingAccelerate failed: {ex.Message}");
            }
            finally
            {
                _reentering = false;
            }
        }
    }
}
