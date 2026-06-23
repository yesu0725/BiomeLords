using HarmonyLib;
using BiomeLords.Phase1C;

namespace BiomeLords.Patches
{
    /// <summary>
    /// PedestalFactory needs the Yagluth altar prefab, which isn't in
    /// PrefabManager at OnVanillaPrefabsAvailable. Defer registration to
    /// ZNetScene.Awake when every prefab (including world-structure ones
    /// like the altar) is loaded.
    ///
    /// PedestalFactory.RegisterAll is idempotent — safe to call multiple
    /// times; only the first call actually registers.
    /// </summary>
    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    public static class ZNetScene_Awake_RegisterPedestal
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                PedestalFactory.RegisterAll();
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogError($"[BiomeLords] Deferred pedestal registration failed: {ex}");
            }
        }
    }
}
