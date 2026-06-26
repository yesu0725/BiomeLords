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
            Mathf.Max(0, LordConfig.FallerValkyrieExtraRows?.Value ?? 0);

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
            if (HeightRef(inv) < LoadCeiling)
                HeightRef(inv) = LoadCeiling;
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
