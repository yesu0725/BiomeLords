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
}
