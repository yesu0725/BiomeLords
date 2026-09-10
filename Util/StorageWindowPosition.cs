using UnityEngine;
using UnityEngine.EventSystems;
using BiomeLords.Config;

namespace BiomeLords.Util
{
    /// <summary>
    /// Placement of the chest/storage window (InventoryGui.m_container).
    ///
    /// Two ways to move it, both writing the same pair of config values:
    ///   • Config — LordConfig.StorageUiOffsetColumns / StorageUiOffsetRows, an offset
    ///     from the window's vanilla position measured in inventory CELLS (the grid's
    ///     m_elementSpace), so a given setting looks the same at any UI scale.
    ///   • Drag — StorageWindowDragger lets the player grab the window anywhere except
    ///     the item slots and drop it wherever they like; the resulting position is
    ///     converted back to cells and saved.
    ///
    /// No mod detection anywhere in here. Vanilla never writes m_container.anchoredPosition
    /// (it only toggles the object active), and mods that reposition the container GRID do
    /// so on a child of m_container — so moving m_container moves the whole window, header
    /// and backdrop and grid together, and any other mod's grid-relative offset survives.
    /// </summary>
    public static class StorageWindowPosition
    {
        /// <summary>Fallback cell size if the player grid isn't readable for some reason.
        /// Only used to keep the offsets meaningful; real value comes from m_elementSpace.</summary>
        private const float FallbackCellSize = 70f;

        /// <summary>Keep at least this many SCREEN PIXELS of the window on screen. Stops a
        /// stray drag (or a wild config value) from parking it where it can't be grabbed
        /// back.</summary>
        private const float ScreenMargin = 48f;

        /// <summary>The container RectTransform we captured <see cref="_base"/> from. The
        /// GUI is rebuilt on scene loads, so re-capture whenever the instance changes —
        /// otherwise we'd treat an already-moved window as the vanilla baseline.</summary>
        private static RectTransform _tracked;
        private static Vector2 _base;

        private static bool _hooked;

        /// <summary>Apply the configured position and make sure the window is draggable.
        /// Safe to call on every InventoryGui.Show.</summary>
        public static void Apply(InventoryGui gui)
        {
            var container = gui?.m_container;
            if (container == null) return;

            Track(container);
            EnsureDragger(gui, container);
            HookConfig();

            float cell = CellSize(gui);
            container.anchoredPosition = new Vector2(
                _base.x + LordConfig.StorageUiOffsetColumns.Value * cell,
                _base.y - LordConfig.StorageUiOffsetRows.Value * cell);
            ClampToScreen(container);
        }

        /// <summary>Move the window to a dragged position without touching config —
        /// called every frame while the player is dragging.
        ///
        /// Deliberately does NOT clamp: while the pointer is down the player is in
        /// charge. See <see cref="ClampToScreen"/>.</summary>
        internal static void SetDragged(RectTransform container, Vector2 anchored)
        {
            if (container == null) return;
            Track(container);
            container.anchoredPosition = anchored;
        }

        /// <summary>Convert the window's current position back into cell offsets and save
        /// them — called once when the player lets go.</summary>
        internal static void CommitDragged(InventoryGui gui, RectTransform container)
        {
            if (container == null) return;
            Track(container);

            // The one place a drag gets corrected, so what we save is what the player
            // is looking at.
            ClampToScreen(container);

            float cell = CellSize(gui);
            if (cell <= 0f) return;

            var pos = container.anchoredPosition;
            LordConfig.StorageUiOffsetColumns.Value = (pos.x - _base.x) / cell;
            LordConfig.StorageUiOffsetRows.Value    = (_base.y - pos.y) / cell;
        }

        /// <summary>Remember the untouched position of this container instance so all
        /// offsets are measured from the vanilla spot rather than from wherever we last
        /// left it.</summary>
        private static void Track(RectTransform container)
        {
            if (_tracked == container) return;
            _tracked = container;
            _base    = container.anchoredPosition;
        }

        /// <summary>Cell pitch of the inventory grid — the unit both offsets are measured
        /// in. The player grid uses one m_elementSpace for both axes.</summary>
        private static float CellSize(InventoryGui gui)
        {
            float space = gui != null && gui.m_playerGrid != null ? gui.m_playerGrid.m_elementSpace : 0f;
            return space > 0f ? space : FallbackCellSize;
        }

        private static void EnsureDragger(InventoryGui gui, RectTransform container)
        {
            var dragger = container.GetComponent<StorageWindowDragger>()
                          ?? container.gameObject.AddComponent<StorageWindowDragger>();
            dragger.Gui = gui;
            // Drags that start on an item slot belong to the inventory, not the window.
            dragger.SlotArea = gui.ContainerGrid != null
                ? gui.ContainerGrid.transform as RectTransform
                : null;
        }

        /// <summary>Re-apply live when the values are edited in a config manager, so the
        /// window doesn't sit wrong until the next time it's opened.</summary>
        private static void HookConfig()
        {
            if (_hooked) return;
            _hooked = true;

            void OnChanged(object _, System.EventArgs __)
            {
                var gui = InventoryGui.instance;
                if (gui == null || gui.m_container == null) return;
                float cell = CellSize(gui);
                gui.m_container.anchoredPosition = new Vector2(
                    _base.x + LordConfig.StorageUiOffsetColumns.Value * cell,
                    _base.y - LordConfig.StorageUiOffsetRows.Value * cell);
                ClampToScreen(gui.m_container);
            }

            LordConfig.StorageUiOffsetColumns.SettingChanged += OnChanged;
            LordConfig.StorageUiOffsetRows.SettingChanged    += OnChanged;
        }

        /// <summary>The camera UI coordinates are relative to — null for a Screen Space
        /// Overlay canvas, which is what RectTransformUtility expects there.</summary>
        private static Camera UiCamera(RectTransform rt)
        {
            var canvas = rt != null ? rt.GetComponentInParent<Canvas>() : null;
            if (canvas == null) return null;
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        /// <summary>The window's rectangle in SCREEN PIXELS.
        ///
        /// The old code measured the window against its PARENT's rect. That only bounds the
        /// window to the screen if the parent happens to be the full-screen canvas — and
        /// m_container's parent is not. Every corner outside the parent read as "off screen",
        /// so the clamp pushed the window back toward the parent's bounds, which is what the
        /// "it only moves in the top half" symptom actually was.
        ///
        /// WorldToScreenPoint with the right camera is correct under every render mode and
        /// canvas scale, so no assumption about the hierarchy is left to be wrong.</summary>
        private static bool TryGetScreenRect(RectTransform rt, out Rect rect)
        {
            rect = default;
            if (rt == null) return false;

            var cam = UiCamera(rt);
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                var sp = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
                if (float.IsNaN(sp.x) || float.IsNaN(sp.y)
                    || float.IsInfinity(sp.x) || float.IsInfinity(sp.y)) return false;
                min = Vector2.Min(min, sp);
                max = Vector2.Max(max, sp);
            }

            var size = max - min;
            // A degenerate rect means we're measuring something we don't understand.
            // Refuse to clamp rather than shove the window somewhere on bad numbers.
            if (size.x < 1f || size.y < 1f) return false;

            rect = new Rect(min, size);
            return true;
        }

        /// <summary>Convert a screen-pixel delta into anchoredPosition units by asking the
        /// parent where two screen points land. Exact under every render mode and canvas
        /// scale — no scaleFactor arithmetic to get wrong.</summary>
        private static bool TryScreenDeltaToLocal(RectTransform rt, Vector2 screenDelta, out Vector2 local)
        {
            local = Vector2.zero;
            var parent = rt != null ? rt.parent as RectTransform : null;
            if (parent == null) return false;

            var cam = UiCamera(rt);
            var origin = new Vector2(Screen.width, Screen.height) * 0.5f;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, origin, cam, out var a)) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, origin + screenDelta, cam, out var b)) return false;

            local = b - a;
            return true;
        }

        /// <summary>Nudge the window back only if it is genuinely off screen.
        ///
        /// Runs when the position is applied from config and once when a drag ENDS — never
        /// per-frame and never mid-drag. While the pointer is down the player is in charge;
        /// a clamp that argues with every drag event cannot be out-dragged, which is exactly
        /// how a wrong measurement became "the window is stuck in the top half".</summary>
        private static void ClampToScreen(RectTransform rt)
        {
            if (rt == null) return;
            if (!TryGetScreenRect(rt, out var r)) return;

            // Keep this much of the window reachable on each axis — but never demand more
            // than the window actually measures, or a small window can't settle.
            float needX = Mathf.Min(ScreenMargin, r.width);
            float needY = Mathf.Min(ScreenMargin, r.height);

            float dx = 0f, dy = 0f;
            if (r.xMax < needX) dx = needX - r.xMax;
            else if (r.xMin > Screen.width - needX) dx = Screen.width - needX - r.xMin;
            if (r.yMax < needY) dy = needY - r.yMax;
            else if (r.yMin > Screen.height - needY) dy = Screen.height - needY - r.yMin;
            if (dx == 0f && dy == 0f) return;

            if (!TryScreenDeltaToLocal(rt, new Vector2(dx, dy), out var push)) return;
            rt.anchoredPosition += push;
        }
    }

    /// <summary>
    /// Click-and-drag the chest/storage window. Attached to InventoryGui.m_container by
    /// <see cref="StorageWindowPosition.Apply"/>.
    ///
    /// Grab it anywhere — backdrop, header, weight readout — except over the item slots,
    /// which keep their normal click-to-move-item behaviour. Because Unity routes drag
    /// events to the nearest ancestor that handles them, drags starting on a slot would
    /// otherwise bubble up to here and drag the window instead.
    /// </summary>
    public class StorageWindowDragger : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        internal InventoryGui Gui;
        internal RectTransform SlotArea;

        private RectTransform _rect;
        private RectTransform _parent;
        private Canvas _canvas;
        private Vector2 _grabOffset;
        private bool _dragging;

        private void Awake()
        {
            _rect   = transform as RectTransform;
            _parent = _rect != null ? _rect.parent as RectTransform : null;
            _canvas = GetComponentInParent<Canvas>();
        }

        public void OnBeginDrag(PointerEventData e)
        {
            _dragging = false;
            if (_rect == null || _parent == null) return;
            if (e.button != PointerEventData.InputButton.Left) return;
            if (StartedOnSlots(e)) return;
            if (!ToLocal(e.position, out var local)) return;

            _grabOffset = _rect.anchoredPosition - local;
            _dragging   = true;
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_dragging) return;
            if (!ToLocal(e.position, out var local)) return;
            StorageWindowPosition.SetDragged(_rect, local + _grabOffset);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!_dragging) return;
            _dragging = false;
            StorageWindowPosition.CommitDragged(Gui, _rect);
        }

        private bool StartedOnSlots(PointerEventData e)
        {
            if (SlotArea == null) return false;
            var hit = e.pointerPressRaycast.gameObject ?? e.pointerCurrentRaycast.gameObject;
            return hit != null && hit.transform.IsChildOf(SlotArea);
        }

        /// <summary>Screen point → position in the parent's local space, which shares its
        /// units with anchoredPosition, so the grab offset stays constant through the drag.</summary>
        private bool ToLocal(Vector2 screen, out Vector2 local)
        {
            var cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, screen, cam, out local);
        }
    }
}
