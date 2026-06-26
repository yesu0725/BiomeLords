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
    /// The only thing that doesn't auto-grow is the player panel's backdrop image,
    /// so this patch stretches the panel (and its background) to cover the added
    /// rows whenever the inventory is opened.
    ///
    /// Defensive throughout: if the panel layout differs from what we expect it
    /// degrades to "rows extend a little past the frame" rather than throwing.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
    public static class InventoryGui_Show_FeatherweightPanel
    {
        private static float _basePanelHeight = float.NaN;
        private static float _baseBkgHeight   = float.NaN;

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

            // Under ComfyQuickSlots the backdrop is its own "ExtInvGrid" image, which
            // it re-sizes one frame after Show — clobbering anything we set here. Leave
            // the panel alone; InventoryGrid_UpdateInventory_FeatherweightCqsBackdrop
            // extends CQS's backdrop after CQS runs instead.
            if (FeatherweightInventory.ComfyQuickSlotsLoaded)
                return;

            int rows = player.GetInventory().GetHeight();
            int extraRows = rows - FeatherweightInventory.BaseHeight;
            if (extraRows < 0) extraRows = 0;

            float space = gui.m_playerGrid.m_elementSpace;
            float delta = extraRows * space;

            // Cache the vanilla base sizes the first time so repeated opens (and
            // toggling the blessing on/off) always compute from the same baseline.
            var panel = gui.m_player;
            if (float.IsNaN(_basePanelHeight)) _basePanelHeight = panel.sizeDelta.y;
            SetHeight(panel, _basePanelHeight + delta);

            var bkg = FindBackdrop(panel);
            if (bkg != null)
            {
                if (float.IsNaN(_baseBkgHeight)) _baseBkgHeight = bkg.sizeDelta.y;
                SetHeight(bkg, _baseBkgHeight + delta);
            }
        }

        private static void SetHeight(RectTransform rt, float height)
        {
            var size = rt.sizeDelta;
            size.y = height;
            rt.sizeDelta = size;
        }

        /// <summary>Find the panel backdrop: the largest UI Image directly under the
        /// player panel (the dark/parchment frame behind the grid). Uses a
        /// string-based GetComponent so we don't reference UnityEngine.UI.</summary>
        private static RectTransform FindBackdrop(RectTransform panel)
        {
            RectTransform best = null;
            float bestArea = 0f;
            for (int i = 0; i < panel.childCount; i++)
            {
                var child = panel.GetChild(i) as RectTransform;
                if (child == null || child.GetComponent("Image") == null) continue;
                float area = child.rect.width * child.rect.height;
                if (area > bestArea) { bestArea = area; best = child; }
            }
            return best;
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
