using System.Reflection;
using HarmonyLib;
using BiomeLords.Config;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Debug helper: when LordConfig.ShowCropGrowTimes is true, append the
    /// remaining/total grow time to a planted crop's hover text. Hover text
    /// is recomputed every frame while you're looking at the plant, so the
    /// "remaining" portion ticks down in real time.
    ///
    /// Toggled via the `biomelords_crop_times` console command.
    /// </summary>
    [HarmonyPatch(typeof(Plant), nameof(Plant.GetHoverText))]
    public static class Plant_GetHoverText_DebugTimes
    {
        private static readonly MethodInfo MGrowTime         = AccessTools.Method(typeof(Plant), "GetGrowTime");
        private static readonly MethodInfo MTimeSincePlanted = AccessTools.Method(typeof(Plant), "TimeSincePlanted");
        private static int _logCounter;

        [HarmonyPostfix]
        public static void Postfix(Plant __instance, ref string __result)
        {
            if (__instance == null) return;
            if (LordConfig.ShowCropGrowTimes == null || !LordConfig.ShowCropGrowTimes.Value) return;

            float grow      = TryInvoke(MGrowTime,         __instance);
            float elapsed   = TryInvoke(MTimeSincePlanted, __instance);
            float remaining = System.Math.Max(0f, grow - elapsed);

            int rm = (int)(remaining / 60), rs = (int)(remaining % 60);
            int tm = (int)(grow      / 60), ts = (int)(grow      % 60);

            // Bright cyan so it pops against the vanilla hover panel.
            __result += $"\n<color=#80ddff><b>Growth:</b> {rm:D2}:{rs:D2} / {tm:D2}:{ts:D2}</color>";

            if (LordConfig.DebugLogging != null && LordConfig.DebugLogging.Value
                && (_logCounter++ % 30) == 0)
            {
                Jotunn.Logger.LogInfo(
                    $"[BiomeLords] Plant hover (grow={grow:F1}s elapsed={elapsed:F1}s remaining={remaining:F1}s) " +
                    $"appended to '{__instance.gameObject.name}'.");
            }
        }

        private static float TryInvoke(MethodInfo mi, Plant target)
        {
            if (mi == null) return 0f;
            try
            {
                var v = mi.Invoke(target, null);
                if (v is float f)  return f;
                if (v is double d) return (float)d;
                if (v is int i)    return i;
                return 0f;
            }
            catch { return 0f; }
        }
    }
}
