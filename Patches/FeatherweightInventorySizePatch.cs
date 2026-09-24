using HarmonyLib;
using BiomeLords.Util;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Featherweight vs. Valheim 1.0's purchasable inventory rows.
    ///
    /// Vanilla 1.0 sells extra inventory rows at Haldor. The purchased count is the
    /// player unique key <c>invrows</c>, and Player.SetInventorySize applies it:
    ///
    ///   rows = Clamp(rows, 0, 9);
    ///   m_inventory.SetHeight(rows);          // grid becomes EXACTLY the bought rows
    ///   AddUniqueKeyValue("invrows", rows);
    ///   InventoryGui.instance.SetInventorySize(rows);
    ///   DropInvalidItems();                   // every item with y >= rows → ground
    ///
    /// It runs on every spawn (Player.OnSpawned), on every Haldor purchase
    /// (StoreGui.OnBuyItem) and from the <c>inventorysize</c> console command. Left
    /// alone it is fatal to Featherweight: on login the load patch has just
    /// restored the extra rows and their items, vanilla shrinks the grid back to
    /// the bought count and Humanoid.DropInvalidItems throws everything in the
    /// Featherweight rows onto the floor — before our OnSpawned postfix gets to run.
    /// Buying a row while blessed did the same in Haldor's camp.
    ///
    /// The fix hooks the destructive step. Before DropInvalidItems scans the grid we
    /// run FeatherweightInventory.Reconcile on the local player, which re-reads the
    /// (just-updated) purchased count and sets the height to purchased + extra rows.
    /// Any items genuinely beyond that (only possible if the ExtraRows config was
    /// lowered between sessions) are crated at the player's feet the way a blessing
    /// switch crates them — never ground-dropped. By the time vanilla scans, nothing
    /// is out of bounds. Without the blessing the prefix stays out of the way, so an
    /// un-blessed player sees exactly vanilla's behaviour.
    ///
    /// Because Featherweight rows always sit BELOW the purchased rows, buying a row
    /// just moves the boundary down by one: an item that was in the first
    /// Featherweight row is now in the last purchased row, in the same slot. No item
    /// moves, nothing is lost.
    ///
    /// Hooking DropInvalidItems rather than SetInventorySize also covers the
    /// <c>inventoryclean</c> console command, which calls it directly.
    ///
    /// The blessing SE must already be present for this to know the rows are
    /// wanted — BlessingPersistencePatch re-applies it in an OnSpawned PREFIX for
    /// exactly that reason.
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.DropInvalidItems))]
    public static class Humanoid_DropInvalidItems_Featherweight
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static void Prefix(Humanoid __instance)
        {
            var p = Player.m_localPlayer;
            if (p == null || !ReferenceEquals(__instance, p)) return;

            // Never let a failure here block the vanilla call — worst case vanilla
            // drops what it would have dropped anyway.
            try
            {
                // The purchased count may have just changed (SetInventorySize writes
                // the key right before calling us) — re-read it.
                FeatherweightInventory.InvalidateBaseHeight();

                // Only step in when the grid is ours to manage: the blessing is active
                // (rows to re-assert), or vanilla just set a height below our floor
                // (ComfyQuickSlots' 5th row). An un-blessed player on a plain grid gets
                // untouched vanilla behaviour.
                var inv = p.GetInventory();
                if (inv == null) return;
                if (!FeatherweightInventory.HasBlessing(p) &&
                    inv.GetHeight() >= FeatherweightInventory.BaseHeight(p))
                    return;

                FeatherweightInventory.Reconcile(p);
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogWarning(
                    $"[BiomeLords] Featherweight reconcile before DropInvalidItems skipped: {ex.Message}");
            }
        }
    }
}
