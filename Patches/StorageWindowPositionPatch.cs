using HarmonyLib;
using BiomeLords.Util;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Places the chest/storage window at its configured position and installs the
    /// drag handler each time the inventory opens. All the work lives in
    /// <see cref="StorageWindowPosition"/>; this is just the hook.
    ///
    /// Show is the right moment: vanilla only toggles m_container's active state and
    /// never writes its position, so a single placement per open sticks for the life
    /// of the window (the player's own drags aside).
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
    public static class InventoryGui_Show_StorageWindowPosition
    {
        [HarmonyPostfix]
        public static void Postfix(InventoryGui __instance)
        {
            try { StorageWindowPosition.Apply(__instance); }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogWarning($"[BiomeLords] Storage window placement skipped: {ex.Message}");
            }
        }
    }
}
