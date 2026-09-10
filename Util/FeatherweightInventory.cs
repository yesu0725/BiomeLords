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
    /// LordConfig.FallerValkyrieExtraRows extra rows (8 slots each). Valheim does
    /// NOT persist inventory dimensions (only item grid positions), so:
    ///   • On load, InventoryExpandLoadPatch pre-grows the player inventory (to a
    ///     safe ceiling) BEFORE items are read, so saved items in the extra rows
    ///     land in their slots instead of being compacted away — or destroyed if
    ///     the base grid is full (Inventory.AddItem during load).
    ///   • On spawn, BlessingPersistencePatch re-applies the saved blessing and
    ///     calls Reconcile(), which sets the final height (expanded if Featherweight
    ///     is active, base otherwise) and crates any items left beyond it.
    ///   • On a deliberate blessing switch, Collapse() moves the extra-row items
    ///     into a CargoCrate dropped at the player's feet, mirroring how a broken
    ///     cart/ship spills its storage (Container.DropAllItems).
    /// </summary>
    public static class FeatherweightInventory
    {
        /// <summary>Vanilla player inventory grid width and base (un-blessed) height:
        /// 8 wide × 4 tall (Humanoid).</summary>
        public const int BaseWidth     = 8;
        public const int VanillaHeight = 4;

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

        private static int _baseHeight = -1;

        /// <summary>True if ComfyQuickSlots is loaded (cached on first query).</summary>
        public static bool ComfyQuickSlotsLoaded =>
            BaseHeight == VanillaHeight + 1;

        /// <summary>Base player-inventory height with no Featherweight rows: the vanilla
        /// 4, or 5 when ComfyQuickSlots owns a 5th (armor/quickslot) row.</summary>
        public static int BaseHeight
        {
            get
            {
                if (_baseHeight < 0)
                    _baseHeight = BepInEx.Bootstrap.Chainloader.PluginInfos
                        .ContainsKey(ComfyQuickSlotsGuid)
                        ? VanillaHeight + 1
                        : VanillaHeight;
                return _baseHeight;
            }
        }

        private static int _seHash;

        private static readonly AccessTools.FieldRef<Inventory, int> HeightRef =
            AccessTools.FieldRefAccess<Inventory, int>("m_height");

        public static int ExtraRows =>
            IncompatibleSlotModLoaded
                ? 0
                : Mathf.Max(0, LordConfig.FallerValkyrieExtraRows?.Value ?? 0);

        public static int ExpandedHeight => BaseHeight + ExtraRows;

        /// <summary>Height used by the load patch — generous enough that lowering
        /// the ExtraRows config between sessions never strands saved items at load.</summary>
        public static int LoadCeiling => BaseHeight + Mathf.Max(ExtraRows, 4);

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
        /// when ExtraRows is 0 so items saved in extra rows under a previous config
        /// are still loaded (Reconcile then crates them back to the player).</summary>
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

            int target = ExpandedHeight;
            // Cheap path first: already tall enough (the overwhelmingly common case, and
            // these patches sit on per-frame auto-pickup checks). Skips the owner and
            // status-effect lookups entirely.
            if (HeightRef(inv) >= target) return;

            var p = Player.m_localPlayer;
            if (p == null || p.GetInventory() != inv) return;
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
            int first  = BaseHeight;
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
        /// blessing state: expanded while Featherweight is active, base otherwise.
        /// Any items beyond the target height are crated.</summary>
        public static void Reconcile(Player p)
        {
            if (p == null) return;
            int target = HasBlessing(p) && ExtraRows > 0 ? ExpandedHeight : BaseHeight;
            SetHeight(p, target);
        }

        /// <summary>Collapse to base height (used when switching away from
        /// Featherweight) — extra-row items spill into a CargoCrate.</summary>
        public static void Collapse(Player p) => SetHeight(p, BaseHeight);

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
