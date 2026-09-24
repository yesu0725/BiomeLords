using HarmonyLib;
using UnityEngine;
using BiomeLords.Util;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Featherweight inventory UI — makes the extra rows sit inside the same
    /// window as the normal inventory.
    ///
    /// The slots themselves already match vanilla exactly: InventoryGrid builds
    /// every cell from the same `m_elementPrefab` and the grid root auto-resizes,
    /// so the extra rows render with identical slot art, bindings and tooltips.
    /// The only thing that doesn't auto-grow is the player panel, and since
    /// Valheim 1.0 vanilla has its own routine for exactly that:
    /// InventoryGui.SetInventorySize(rows) sizes the panel for `rows` total rows
    /// from a baseline it captured in Awake. Vanilla calls it from
    /// Player.SetInventorySize with the PURCHASED row count only (on spawn and on a
    /// Haldor purchase), so the panel comes up framing the bought rows and not the
    /// Featherweight rows below them. This patch re-runs that same vanilla routine
    /// with the grid's TRUE height every time the inventory is opened, so the panel
    /// frames every row the player actually has. Handing the layout to vanilla's own
    /// formula is what keeps purchased rows and Featherweight rows looking identical,
    /// and avoids double-counting the purchased rows (which an independently cached
    /// "base" height would do, since vanilla has already stretched the panel for
    /// them before the first open).
    ///
    /// This patch owns the PLAYER panel only. Keeping the chest/storage window clear
    /// of the extra rows is a separate, mod-agnostic concern handled by
    /// <see cref="BiomeLords.Util.StorageWindowPosition"/> — a configured offset the
    /// player can also set by dragging the window.
    ///
    /// Defensive throughout: if the panel layout differs from what we expect it
    /// degrades to "rows extend a little past the frame" rather than throwing.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
    public static class InventoryGui_Show_FeatherweightPanel
    {
        [HarmonyPostfix]
        public static void Postfix(InventoryGui __instance)
        {
            try { Resize(__instance); }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogWarning($"[BiomeLords] Featherweight panel resize skipped: {ex.Message}");
            }
        }

        private static void Resize(InventoryGui gui)
        {
            var player = Player.m_localPlayer;
            if (gui == null || player == null || gui.m_player == null || gui.m_playerGrid == null)
                return;

            // An incompatible slot-expansion mod (ExtraSlots / AzuExtendedPlayerInventory)
            // owns the player grid height AND its own panel/backdrop layout. Featherweight
            // adds no rows in that configuration, so leave the whole inventory UI untouched —
            // otherwise we'd stretch the panel for rows that mod lays out its own way.
            if (FeatherweightInventory.IncompatibleSlotModLoaded)
                return;

            // SKIP this under ComfyQuickSlots: there the player backdrop is CQS's own
            // "ExtInvGrid" image, which CQS re-sizes one frame after Show (clobbering
            // anything we set) — InventoryGrid_UpdateInventory_FeatherweightCqsBackdrop
            // extends that backdrop after CQS runs instead.
            if (FeatherweightInventory.ComfyQuickSlotsLoaded)
                return;

            var inv = player.GetInventory();
            if (inv == null) return;

            // Vanilla's own panel sizing, fed the real row count (purchased + Featherweight).
            // Idempotent: with no Featherweight rows this is exactly the size vanilla set.
            gui.SetInventorySize(inv.GetHeight());
        }
    }

    /// <summary>
    /// Featherweight ↔ ComfyQuickSlots backdrop reconciliation.
    ///
    /// ComfyQuickSlots draws the player-inventory backdrop as its own "ExtInvGrid"
    /// image (a clone of the vanilla "Bkg"), sized to cover the vanilla 4 rows plus
    /// the 1 armor/quickslot row it adds — and it re-applies that size every time the
    /// grid refreshes, one frame after InventoryGui.Show. That clobbers any backdrop
    /// resize we do in the Show postfix, and it doesn't know about the extra
    /// Featherweight rows, so those rows would sit below the frame.
    ///
    /// CQS sizes the backdrop as height = 300 + 75·num, anchoredPos.y = -35·num,
    /// width = 590, where num is the number of rows beyond the vanilla 4 (it hardcodes
    /// num = 1). We mirror that exact formula but with the TRUE num = height − 4, so
    /// the backdrop also covers any active Featherweight rows. Marked HarmonyAfter CQS
    /// so our size is the one that sticks. No-op unless CQS is installed.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateInventory))]
    public static class InventoryGrid_UpdateInventory_FeatherweightCqsBackdrop
    {
        // Mirror of ComfyQuickSlots' ExtInvGrid sizing constants.
        private const float BaseHeightPx = 300f;
        private const float RowHeightPx  = 75f;
        private const float RowOffsetPx  = 35f;
        private const float WidthPx      = 590f;
        private const string ExtBkgName  = "ExtInvGrid";

        [HarmonyAfter("com.bruce.valheim.comfyquickslots")]
        [HarmonyPostfix]
        public static void Postfix(InventoryGrid __instance)
        {
            if (!FeatherweightInventory.ComfyQuickSlotsLoaded) return;

            try
            {
                var player = Player.m_localPlayer;
                if (player == null || __instance == null) return;

                // Only act on the player's own grid (not container/craft grids).
                if (__instance.GetInventory() != player.GetInventory()) return;

                int num = player.GetInventory().GetHeight() - FeatherweightInventory.VanillaHeight;
                if (num < 1) return; // Nothing beyond vanilla — leave CQS's sizing alone.

                var parent = __instance.transform.parent;
                if (parent == null) return;
                var ext = parent.Find(ExtBkgName) as RectTransform;
                if (ext == null) return; // CQS hasn't created it yet this frame.

                ext.anchoredPosition = new Vector2(0f, -RowOffsetPx * num);
                ext.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, WidthPx);
                ext.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, BaseHeightPx + RowHeightPx * num);
            }
            catch (System.Exception ex)
            {
                Jotunn.Logger.LogWarning($"[BiomeLords] Featherweight CQS backdrop resize skipped: {ex.Message}");
            }
        }
    }
}
