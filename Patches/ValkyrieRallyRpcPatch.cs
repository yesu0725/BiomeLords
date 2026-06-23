using HarmonyLib;
using BiomeLords.Phase1D;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Registers the Valkyrie's Rally routed RPC on every client once the game
    /// session is up. ZRoutedRpc.instance is created per world-join, so we
    /// (re)register on each Game.Start; the service guards against duplicate
    /// registration on the same instance.
    /// </summary>
    [HarmonyPatch(typeof(Game), "Start")]
    public static class Game_Start_ValkyrieRallyRpc
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            ValkyrieRallyService.RegisterRpc();
        }
    }
}
