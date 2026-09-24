using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using BiomeLords.Config;
using BiomeLords.Phase1C;

namespace BiomeLords.Util
{
    /// <summary>
    /// Featherweight (Fallen Valkyrie Lord blessing) inventory expansion.
    ///
    /// While the blessing is active the player's inventory gains
    /// LordConfig.FallerValkyrieExtraRows extra rows (8 slots each), stacked BELOW
    /// whatever base grid the player already has. Valheim does NOT persist inventory
    /// dimensions (only item grid positions), so:
    ///   • On load, InventoryExpandLoadPatch pre-grows the player inventory (to a
    ///     safe ceiling) BEFORE items are read, so m_height agrees with where the
    ///     saved extra-row items actually sit. (Under 1.0 the load itself preserves
    ///     their grid positions either way — Inventory.Load passes
    ///     skipValidPositionCheck: true — so the pre-grow is consistency, not
    ///     rescue; what would otherwise destroy them is the DropInvalidItems call
    ///     later in the same spawn. See FeatherweightInventorySizePatch.)
    ///   • On spawn, BlessingPersistencePatch re-applies the saved blessing and
    ///     calls Reconcile(), which sets the final height (expanded if Featherweight
    ///     is active, base otherwise) and crates any items left beyond it.
    ///   • On a deliberate blessing switch, Collapse() moves the extra-row items
    ///     into a CargoCrate dropped at the player's feet, mirroring how a broken
    ///     cart/ship spills its storage (Container.DropAllItems).
    ///
    /// <b>Valheim 1.0 purchased rows.</b> Vanilla now sells extra inventory rows at
    /// Haldor: the count lives in the player unique key <c>invrows</c>
    /// (Player.InventoryRowsKey, 4..9) and Player.SetInventorySize applies it — on
    /// every spawn, on every purchase and from the <c>inventorysize</c> console
    /// command — by setting the grid height to exactly that count and then calling
    /// Humanoid.DropInvalidItems, which throws every item beyond it on the ground.
    /// The base height is therefore no longer a constant: <see cref="BaseHeight"/>
    /// reads the player's purchased count (floored at the vanilla 4, or 5 under
    /// ComfyQuickSlots) and Featherweight's rows sit below THOSE, so buying a row
    /// simply moves the boundary down one row with no item shuffling. See
    /// FeatherweightInventorySizePatch for how the vanilla resize is intercepted so
    /// it can never drop the Featherweight rows on the floor.
    /// </summary>
    public static class FeatherweightInventory
    {
        /// <summary>Vanilla player inventory grid width and default (nothing bought,
        /// un-blessed) height: 8 wide × 4 tall (Humanoid).</summary>
        public const int BaseWidth     = 8;
        public const int VanillaHeight = 4;

        /// <summary>Largest row count vanilla will ever set on the player grid —
        /// Player.SetInventorySize clamps the purchased <c>invrows</c> to this.</summary>
        public const int VanillaMaxRows = 9;

        /// <summary>ComfyQuickSlots plugin GUID. When that mod is installed it forces
        /// the player inventory to 5 rows and claims grid row index 4 (the 5th row)
        /// for armor + quickslot items, and renames the inventory. BiomeLords must
        /// then treat 5 — not 4 — as the un-blessed base so it never strips or crates
        /// that row, and Featherweight rows stack ABOVE it (grid y ≥ 5).</summary>
        private const string ComfyQuickSlotsGuid = "com.bruce.valheim.comfyquickslots";

        /// <summary>ComfyQuickSlots renames the player inventory to this. We accept it
        /// alongside the vanilla "Inventory" name when identifying the player grid.</summary>
        private const string ComfyQuickSlotsInvName = "ComfyQuickSlotsInventory";

        /// <summary>GUIDs of inventory-expansion mods that already add extra slot rows to the
        /// player inventory. When any of these is installed, Featherweight's extra-row buff is
        /// suppressed (ExtraRows → 0) so it doesn't fight another mod over the grid height —
        /// only the carry-weight side of the blessing remains. ComfyQuickSlots is deliberately
        /// NOT in this list: it claims a single fixed row and Featherweight stacks cleanly
        /// above it (see BaseHeight / grid y ≥ 5 handling).</summary>
        private static readonly string[] IncompatibleSlotModGuids =
        {
            "shudnal.ExtraSlots",                 // Shudnal ExtraSlots
            "Azumatt.AzuExtendedPlayerInventory", // AzuExtendedPlayerInventory
        };

        private static int _incompatibleSlotMod = -1;

        /// <summary>True if a slot-expansion mod incompatible with Featherweight's extra rows
        /// is installed (cached on first query). Detected by BepInEx plugin GUID.</summary>
        public static bool IncompatibleSlotModLoaded
        {
            get
            {
                if (_incompatibleSlotMod < 0)
                {
                    var plugins = BepInEx.Bootstrap.Chainloader.PluginInfos;
                    _incompatibleSlotMod =
                        IncompatibleSlotModGuids.Any(plugins.ContainsKey) ? 1 : 0;
                }
                return _incompatibleSlotMod == 1;
            }
        }

        private static int _minBaseHeight = -1;

        /// <summary>True if ComfyQuickSlots is loaded (cached on first query).</summary>
        public static bool ComfyQuickSlotsLoaded =>
            MinBaseHeight == VanillaHeight + 1;

        /// <summary>Smallest base height the player grid can have: the vanilla 4, or 5
        /// when ComfyQuickSlots owns a 5th (armor/quickslot) row. The real base is
        /// <see cref="BaseHeight"/>, which also counts rows bought from Haldor.</summary>
        public static int MinBaseHeight
        {
            get
            {
                if (_minBaseHeight < 0)
                    _minBaseHeight = BepInEx.Bootstrap.Chainloader.PluginInfos
                        .ContainsKey(ComfyQuickSlotsGuid)
                        ? VanillaHeight + 1
                        : VanillaHeight;
                return _minBaseHeight;
            }
        }

        // Purchased-row cache. Player.TryGetUniqueKeyValue walks and splits every
        // unique key on each call, and BaseHeight is consulted from the capacity
        // prefixes that run per item per frame during auto-pickup, so the value is
        // cached per Player instance. The only writers of `invrows` are
        // Player.SetInventorySize (which always ends in DropInvalidItems — patched to
        // refresh this) and Player.Load (followed by OnSpawned — also patched).
        private static Player _baseHeightPlayer;
        private static int    _baseHeightCached = -1;

        /// <summary>Forget the cached purchased-row count so the next
        /// <see cref="BaseHeight"/> query re-reads the player's <c>invrows</c> key.</summary>
        public static void InvalidateBaseHeight()
        {
            _baseHeightPlayer = null;
            _baseHeightCached = -1;
        }

        /// <summary>Rows the player has bought from Haldor (the vanilla <c>invrows</c>
        /// unique key), clamped exactly as Player.SetInventorySize clamps it. A player
        /// who has never spawned under 1.0 has no key yet — vanilla treats that as 4.</summary>
        public static int PurchasedRows(Player p)
        {
            if (p != null &&
                p.TryGetUniqueKeyValue(Player.InventoryRowsKey, out var raw) &&
                int.TryParse(raw, out var rows))
                return Mathf.Clamp(rows, 0, VanillaMaxRows);
            return VanillaHeight;
        }

        /// <summary>Player-inventory height with no Featherweight rows: the rows
        /// vanilla itself wants — the purchased count, floored at
        /// <see cref="MinBaseHeight"/>. Featherweight rows start at this grid y.</summary>
        public static int BaseHeight(Player p)
        {
            if (p == null) return MinBaseHeight;
            if (!ReferenceEquals(p, _baseHeightPlayer) || _baseHeightCached < 0)
            {
                _baseHeightCached = Mathf.Max(MinBaseHeight, PurchasedRows(p));
                _baseHeightPlayer = p;
            }
            return _baseHeightCached;
        }

        private static int _seHash;

        private static readonly AccessTools.FieldRef<Inventory, int> HeightRef =
            AccessTools.FieldRefAccess<Inventory, int>("m_height");

        public static int ExtraRows =>
            IncompatibleSlotModLoaded
                ? 0
                : Mathf.Max(0, LordConfig.FallerValkyrieExtraRows?.Value ?? 0);

        public static int ExpandedHeight(Player p) => BaseHeight(p) + ExtraRows;

        /// <summary>Height used by the load patch. Items are read before the player's
        /// unique keys and custom data (Player.Load order), so neither the purchased
        /// row count nor the blessing is known yet — assume the vanilla maximum, and
        /// be generous with the extra rows so lowering the ExtraRows config between
        /// sessions never strands saved items at load. Reconcile trims it right after.</summary>
        public static int LoadCeiling =>
            Mathf.Max(VanillaMaxRows, MinBaseHeight) + Mathf.Max(ExtraRows, 4);

        public static int SeHash
        {
            get
            {
                if (_seHash == 0)
                    _seHash = StatusEffectFactory.FallerValkyrieLordSpiritSE.GetStableHashCode();
                return _seHash;
            }
        }

        public static bool HasBlessing(Player p)
        {
            if (p == null) return false;
            var seman = p.GetSEMan();
            return seman != null && seman.HaveStatusEffect(SeHash);
        }

        /// <summary>True if this is the vanilla player inventory (name + base width),
        /// used by the load patch to identify it without an owner reference.</summary>
        public static bool IsPlayerInventory(Inventory inv)
        {
            if (inv == null || inv.GetWidth() != BaseWidth)
                return false;
            var name = inv.GetName();
            // ComfyQuickSlots renames the player inventory, so accept its name too —
            // otherwise the load pre-grow never fires under CQS and saved
            // Featherweight rows get compacted/destroyed on load.
            return name == "Inventory" || name == ComfyQuickSlotsInvName;
        }

        /// <summary>Pre-grow an inventory to the load ceiling (only ever raises).
        /// Called from the Inventory.Load prefix before items are read. Grows even
        /// when ExtraRows is 0 so the height still covers items saved in extra rows
        /// under a previous config (Reconcile then crates them back to the player).</summary>
        public static void GrowForLoad(Inventory inv)
        {
            if (inv == null) return;
            // An incompatible slot-expansion mod owns the player grid height — never
            // pre-grow it, or we'd fight that mod over the inventory dimensions.
            if (IncompatibleSlotModLoaded) return;
            if (HeightRef(inv) < LoadCeiling)
                HeightRef(inv) = LoadCeiling;
        }

        /// <summary>
        /// Raise the local player's inventory to its blessed height if it has drifted
        /// below it. Called from the capacity-check patches so the extra rows are real
        /// capacity, not just visible slots.
        ///
        /// Vanilla decides "is there room?" purely from `m_width * m_height`
        /// (Inventory.CanAddItem / HaveEmptySlot / GetEmptySlots, and FindEmptySlot
        /// inside AddItem). Placing an item at an explicit grid position instead only
        /// checks GetItemAt(x, y) — which is why dragging into the extra rows always
        /// worked even when the height had reverted, while auto-pickup reported a full
        /// inventory. Reconcile() alone can't hold the invariant: it runs on spawn and
        /// on a blessing change, so anything that rebuilds the inventory afterwards
        /// leaves the height stale until the next spawn.
        ///
        /// Deliberately one-directional — it only ever RAISES, never lowers. Lowering
        /// is Reconcile/Collapse's job because that path has to crate the items left
        /// beyond the new height first; a lowering call from here could silently strand
        /// or destroy them.
        /// </summary>
        public static void EnsureExpanded(Inventory inv)
        {
            if (inv == null || IncompatibleSlotModLoaded) return;

            var p = Player.m_localPlayer;
            if (p == null || p.GetInventory() != inv) return;

            int target = ExpandedHeight(p);
            // Cheap path first: already tall enough (the overwhelmingly common case, and
            // these patches sit on per-frame auto-pickup checks). BaseHeight is cached,
            // so this is an int compare; the status-effect lookup is skipped entirely.
            if (HeightRef(inv) >= target) return;

            if (!HasBlessing(p)) return;

            if (LordConfig.DebugLogging.Value)
                Jotunn.Logger.LogInfo(
                    $"[BiomeLords] Featherweight: inventory height had drifted to {HeightRef(inv)}, " +
                    $"restoring to {target} so the extra rows count as real capacity.");

            HeightRef(inv) = target;
        }

        /// <summary>
        /// Find a free slot in the Featherweight rows only — the grid rows at or below
        /// <see cref="BaseHeight"/> are deliberately NOT searched.
        ///
        /// This is the fallback for slot-finders that cap their own search at a fixed
        /// row count and so never see our rows. Vanilla's `Inventory.FindEmptySlot`
        /// walks the real `m_height` and needs no help, but ComfyQuickSlots REPLACES
        /// it (prefix returning false) with `QuickSlotsManager.GetEmptyInventorySlot`,
        /// which hardcodes `for (y = 0; y &lt; 5; y++)`. With CQS installed the extra
        /// rows therefore rendered and accepted hand-dragged items, while auto-pickup,
        /// `Humanoid.Pickup` and craft output all failed with "inventory full" the
        /// moment rows 0-4 were full — CQS's own CanAddItem counts `m_width*m_height`
        /// and reported room that its slot-finder then refused to hand out.
        ///
        /// Returns (-1, -1) when there is no free extra-row slot (including whenever
        /// the blessing is inactive, since the height is then just BaseHeight and the
        /// scan range is empty). Never returns a slot in the base rows, so it can't
        /// hand back a row another mod has reserved — CQS's armor/quickslot row is at
        /// y = 4, below our BaseHeight of 5 under CQS.
        /// </summary>
        public static Vector2i FindExtraRowSlot(Inventory inv, bool topFirst)
        {
            var none = new Vector2i(-1, -1);
            if (inv == null || IncompatibleSlotModLoaded) return none;

            var p = Player.m_localPlayer;
            if (p == null || p.GetInventory() != inv) return none;

            // The height can be stale here: CQS's prefixes skip ours on the capacity
            // checks that would normally have re-asserted it.
            EnsureExpanded(inv);

            int height = inv.GetHeight();
            int width  = inv.GetWidth();
            int first  = BaseHeight(p);
            if (height <= first) return none;

            if (topFirst)
            {
                for (int y = first; y < height; y++)
                    for (int x = 0; x < width; x++)
                        if (inv.GetItemAt(x, y) == null) return new Vector2i(x, y);
            }
            else
            {
                for (int y = height - 1; y >= first; y--)
                    for (int x = 0; x < width; x++)
                        if (inv.GetItemAt(x, y) == null) return new Vector2i(x, y);
            }
            return none;
        }

        /// <summary>Set the inventory to its correct height for the player's current
        /// blessing state: expanded while Featherweight is active, base (vanilla's own
        /// purchased count) otherwise. Any items beyond the target height are crated.
        /// Re-reads the purchased count first, since this runs right after the paths
        /// that change it.</summary>
        public static void Reconcile(Player p)
        {
            if (p == null) return;
            InvalidateBaseHeight();
            int target = HasBlessing(p) && ExtraRows > 0 ? ExpandedHeight(p) : BaseHeight(p);
            SetHeight(p, target);
        }

        /// <summary>Collapse to base height (used when switching away from
        /// Featherweight) — extra-row items spill into a CargoCrate. Rows bought from
        /// Haldor are part of the base and are never collapsed.</summary>
        public static void Collapse(Player p)
        {
            if (p == null) return;
            InvalidateBaseHeight();
            SetHeight(p, BaseHeight(p));
        }

        /// <summary>Core: crate any items sitting at or beyond <paramref name="target"/>,
        /// then set the inventory height. Raising the height never crates anything.</summary>
        private static void SetHeight(Player p, int target)
        {
            // An incompatible slot-expansion mod (ExtraSlots / AzuExtendedPlayerInventory)
            // owns the player grid height and places items in rows we don't manage. Setting
            // the height here would crate away that mod's rows, so leave the grid untouched —
            // Featherweight contributes no extra rows in that configuration.
            if (IncompatibleSlotModLoaded) return;

            var inv = p.GetInventory();
            if (inv == null) return;

            var strays = inv.GetAllItems().Where(it => it.m_gridPos.y >= target).ToList();
            if (strays.Count > 0)
                SpillToCrate(p, inv, strays);

            HeightRef(inv) = target;
        }

        private static void SpillToCrate(Player p, Inventory inv, List<ItemDrop.ItemData> items)
        {
            var prefab = GetCratePrefab();
            Container crate = null;
            int crateCount = 0;
            int dropped = 0;

            foreach (var it in items)
            {
                bool placed = false;

                if (prefab != null)
                {
                    // Spawn the first crate lazily, and a fresh one whenever the
                    // current crate fills up — never drop on the ground just
                    // because a single CargoCrate ran out of slots. (We spawn
                    // multiple default crates rather than enlarging one: a crate's
                    // Container rebuilds its inventory from the prefab size on world
                    // reload, so an over-sized crate would lose items.)
                    if (crate == null)
                        crate = SpawnCrate(p, prefab, crateCount++);

                    placed = crate != null && crate.GetInventory().AddItem(it);
                    if (!placed && crate != null)
                    {
                        crate = SpawnCrate(p, prefab, crateCount++);
                        placed = crate != null && crate.GetInventory().AddItem(it);
                    }
                }

                if (!placed)
                {
                    // No crate prefab at all — last-resort ground drop.
                    ItemDrop.DropItem(it, it.m_stack,
                        p.transform.position + Vector3.up * 0.5f, Quaternion.identity);
                    dropped++;
                }
                inv.RemoveItem(it);
            }

            p.Message(MessageHud.MessageType.Center,
                crateCount > 0
                    ? "$biomelords_featherweight_crate"
                    : "$biomelords_featherweight_dropped");

            if (LordConfig.DebugLogging.Value)
                Jotunn.Logger.LogInfo(
                    $"[BiomeLords] Featherweight collapse: {items.Count} item(s) cleared from extra rows " +
                    $"({crateCount} crate(s), ground-dropped={dropped}).");
        }

        /// <summary>Instantiates one CargoCrate (the same crate a broken cart/ship
        /// spawns for its storage), fanned out by index so multiple crates don't
        /// stack on the same spot. Returns its Container, or null.</summary>
        private static Container SpawnCrate(Player p, GameObject prefab, int index)
        {
            if (prefab == null) return null;

            // Fan crates out in a small arc in front of the player.
            float angle = (index - 1) * 25f;
            var dir = Quaternion.Euler(0f, angle, 0f) * p.transform.forward;
            var pos = p.transform.position + dir * 1.5f + Vector3.up * 0.5f;

            var go = Object.Instantiate(prefab, pos, Quaternion.identity);
            return go != null ? go.GetComponent<Container>() : null;
        }

        private static GameObject GetCratePrefab()
        {
            var zs = ZNetScene.instance;
            if (zs == null) return null;

            var crate = zs.GetPrefab("CargoCrate");
            if (crate != null) return crate;

            // Fallback: read whatever loot container the Cart drops on destruction
            // (Container.m_destroyedLootPrefab), so we track vanilla if it renames.
            var cart = zs.GetPrefab("Cart");
            var cont = cart != null ? cart.GetComponentInChildren<Container>() : null;
            return cont != null ? cont.m_destroyedLootPrefab : null;
        }
    }
}
