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
        private static float _basePanelHeight  = float.NaN;
        private static float _baseBkgHeight    = float.NaN;
        private static float _basePanelPosY    = float.NaN;
        private static float _baseBkgPosY      = float.NaN;
        private static float _baseContainerPosY = float.NaN;

        /// <summary>Extra downward clearance (px) added to the chest shift on top of the
        /// exact row-height delta. The container window docks flush against the player
        /// grid's last row (especially under ComfyQuickSlots), so an exact row-height
        /// shift leaves the chest's header bar just grazing the bottom extra row. This
        /// small gap separates them cleanly. Tunable if it looks too tight / too loose.</summary>
        private const float ContainerClearancePx = 22f;

        // HarmonyAfter CQS so that when ComfyQuickSlots is installed we run after its own
        // InventoryGui.Show postfix (which pins the container grid). Harmless otherwise.
        [HarmonyAfter("com.bruce.valheim.comfyquickslots")]
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
            // otherwise we'd stretch the panel by (GetHeight() - BaseHeight) rows and leave a
            // large empty backdrop below the real slots.
            if (FeatherweightInventory.IncompatibleSlotModLoaded)
                return;

            int rows = player.GetInventory().GetHeight();
            int extraRows = rows - FeatherweightInventory.BaseHeight;
            if (extraRows < 0) extraRows = 0;

            float space = gui.m_playerGrid.m_elementSpace;
            float delta = extraRows * space;

            // The player grid lays its cells out downward from the top, so the extra
            // Featherweight rows extend DOWN past the base grid's bottom. Grow the player
            // panel + backdrop downward (top edge fixed) to frame those rows.
            //
            // SKIP this under ComfyQuickSlots: there the player backdrop is CQS's own
            // "ExtInvGrid" image, which CQS re-sizes one frame after Show (clobbering
            // anything we set) — InventoryGrid_UpdateInventory_FeatherweightCqsBackdrop
            // extends that backdrop after CQS runs instead.
            if (!FeatherweightInventory.ComfyQuickSlotsLoaded)
            {
                // Cache the vanilla base sizes/positions the first time so repeated opens
                // (and toggling the blessing on/off) always compute from the same baseline.
                var panel = gui.m_player;
                if (float.IsNaN(_basePanelHeight))
                {
                    _basePanelHeight = panel.sizeDelta.y;
                    _basePanelPosY   = panel.anchoredPosition.y;
                }
                GrowDownward(panel, _basePanelHeight, _basePanelPosY, delta);

                var bkg = FindBackdrop(panel);
                if (bkg != null)
                {
                    if (float.IsNaN(_baseBkgHeight))
                    {
                        _baseBkgHeight = bkg.sizeDelta.y;
                        _baseBkgPosY   = bkg.anchoredPosition.y;
                    }
                    GrowDownward(bkg, _baseBkgHeight, _baseBkgPosY, delta);
                }
            }

            // Push the whole container (chest) panel DOWN by the same delta so it sits
            // below the extra rows instead of covering them ("the additional rows are
            // underneath the chest UI"). This applies in BOTH the vanilla and CQS cases:
            //   • Neither vanilla `UpdateContainer` nor CQS ever moves `m_container` itself
            //     — vanilla only toggles its active state, and CQS only repositions the
            //     container GRID root (a child of m_container) to a fixed config point.
            //   • Because the CQS-positioned grid root is a child of m_container, moving
            //     m_container moves the whole chest (backdrop + header + grid) together and
            //     the grid keeps its CQS-relative offset. So a single m_container shift is
            //     correct with or without CQS.
            // The shift persists for the life of the window since nothing else writes
            // m_container.anchoredPosition.
            var container = gui.m_container;
            if (container != null)
            {
                if (float.IsNaN(_baseContainerPosY))
                    _baseContainerPosY = container.anchoredPosition.y;
                // Add a small clearance gap on top of the exact row-height delta so the
                // chest's header bar doesn't graze the bottom extra row. Only when the
                // blessing is actually adding rows — with delta == 0 the chest returns to
                // exactly its base position (no stray gap when un-blessed).
                float gap = delta > 0f ? ContainerClearancePx : 0f;
                var cpos = container.anchoredPosition;
                cpos.y = _baseContainerPosY - delta - gap;   // -y = down in anchored space
                container.anchoredPosition = cpos;
            }
        }

        /// <summary>Grows a RectTransform's height from <paramref name="baseHeight"/> by
        /// <paramref name="delta"/> while keeping its TOP edge fixed in place — i.e. all
        /// growth extends downward, matching the player grid (which lays its cells out
        /// downward from the top). Reads `rt.pivot.y` at runtime so it's correct regardless
        /// of how the panel is actually pivoted, instead of assuming a direction.</summary>
        private static void GrowDownward(RectTransform rt, float baseHeight, float basePosY, float delta)
        {
            var size = rt.sizeDelta;
            size.y = baseHeight + delta;
            rt.sizeDelta = size;

            var pos = rt.anchoredPosition;
            pos.y = basePosY - (1f - rt.pivot.y) * delta;
            rt.anchoredPosition = pos;
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
