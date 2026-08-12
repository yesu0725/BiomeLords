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

        /// <summary>How much of the window must stay inside the canvas, in canvas units.
        /// Stops a stray drag (or a wild config value) from parking it off-screen where
        /// it can't be grabbed back.</summary>
        private const float MinVisible = 80f;

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
            container.anchoredPosition = Clamp(container, new Vector2(
                _base.x + LordConfig.StorageUiOffsetColumns.Value * cell,
                _base.y - LordConfig.StorageUiOffsetRows.Value * cell));
        }

        /// <summary>Move the window to a dragged position without touching config —
        /// called every frame while the player is dragging.</summary>
        internal static void SetDragged(RectTransform container, Vector2 anchored)
        {
            if (container == null) return;
            Track(container);
            container.anchoredPosition = Clamp(container, anchored);
        }

        /// <summary>Convert the window's current position back into cell offsets and save
        /// them — called once when the player lets go.</summary>
        internal static void CommitDragged(InventoryGui gui, RectTransform container)
        {
            if (container == null) return;
            Track(container);

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
                gui.m_container.anchoredPosition = Clamp(gui.m_container, new Vector2(
                    _base.x + LordConfig.StorageUiOffsetColumns.Value * cell,
                    _base.y - LordConfig.StorageUiOffsetRows.Value * cell));
            }

            LordConfig.StorageUiOffsetColumns.SettingChanged += OnChanged;
            LordConfig.StorageUiOffsetRows.SettingChanged    += OnChanged;
        }

        /// <summary>Nudge <paramref name="anchored"/> back until at least
        /// <see cref="MinVisible"/> units of the window overlap its parent on both axes.
        /// Works off world corners so it's correct for any anchor/pivot setup.</summary>
        private static Vector2 Clamp(RectTransform rt, Vector2 anchored)
        {
            var parent = rt.parent as RectTransform;
            if (parent == null) return anchored;

            // Set first: GetWorldCorners reflects the pending position immediately, and
            // the caller assigns the clamped result right after.
            rt.anchoredPosition = anchored;

            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            var min = parent.InverseTransformPoint(corners[0]); // bottom-left
            var max = parent.InverseTransformPoint(corners[2]); // top-right
            var bounds = parent.rect;

            float dx = 0f, dy = 0f;
            if (min.x > bounds.xMax - MinVisible)      dx = (bounds.xMax - MinVisible) - min.x;
            else if (max.x < bounds.xMin + MinVisible) dx = (bounds.xMin + MinVisible) - max.x;
            if (min.y > bounds.yMax - MinVisible)      dy = (bounds.yMax - MinVisible) - min.y;
            else if (max.y < bounds.yMin + MinVisible) dy = (bounds.yMin + MinVisible) - max.y;

            return anchored + new Vector2(dx, dy);
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
