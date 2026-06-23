using HarmonyLib;
using BiomeLords.Phase1B;

namespace BiomeLords.Patches
{
    /// <summary>
    /// RandEventSystem doesn't exist yet when OnVanillaPrefabsAvailable fires,
    /// so we register our custom events on its Awake postfix instead.
    /// </summary>
    [HarmonyPatch(typeof(RandEventSystem), "Awake")]
    public static class RandEventSystem_Awake_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                EventFactory.RegisterAll();
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogError($"[BiomeLords] Event registration failed: {ex}");
            }
        }
    }
}
