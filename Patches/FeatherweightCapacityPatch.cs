using HarmonyLib;
using BiomeLords.Util;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Makes Featherweight's extra rows behave as real capacity rather than merely
    /// visible slots.
    ///
    /// Vanilla answers "is there room?" from `m_width * m_height` alone. If the
    /// inventory height has drifted back to the vanilla 4 — which it can, since
    /// nothing in vanilla re-applies our height after the inventory is rebuilt —
    /// then auto-pickup, `Humanoid.Pickup` ("$msg_noroom"), craft output and
    /// container take-all all report a full inventory while the extra rows sit
    /// empty on screen. Dragging an item in by hand still worked, because the
    /// explicit-position `AddItem(item, pos)` overload only checks
    /// `GetItemAt(x, y)` and never consults the height.
    ///
    /// So rather than trusting a one-shot height write, each capacity entry point
    /// re-asserts the blessed height first. `EnsureExpanded` only ever raises, and
    /// only for the local player's own inventory while the blessing is active, so
    /// it can never crate or strand items — and it short-circuits on a single int
    /// compare when the height is already correct, which is the normal case.
    ///
    /// Covered here:
    ///   • CanAddItem(ItemData, int) — Player.AutoPickup's gate. The
    ///     CanAddItem(GameObject, int) overload delegates to this one.
    ///   • AddItem(ItemData)         — Humanoid.Pickup, the "$msg_noroom" path.
    ///   • HaveEmptySlot()           — assorted "can I take this?" checks.
    ///   • GetEmptySlots()           — container take-all and UI capacity readouts.
    /// </summary>
    public static class FeatherweightCapacityPatches
    {
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.CanAddItem),
            new System.Type[] { typeof(ItemDrop.ItemData), typeof(int) })]
        public static class Inventory_CanAddItem_Featherweight
        {
            [HarmonyPrefix]
            public static void Prefix(Inventory __instance) => Guard(__instance);
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem),
            new System.Type[] { typeof(ItemDrop.ItemData) })]
        public static class Inventory_AddItem_Featherweight
        {
            [HarmonyPrefix]
            public static void Prefix(Inventory __instance) => Guard(__instance);
        }

        // Crafting: InventoryGui gates on CanAddItem(GameObject, int) — which delegates
        // to the ItemData overload above — then deposits the result through this string
        // overload, which calls FindEmptySlot itself. Without this the craft is offered
        // and then silently refused when only the extra rows are free.
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem),
            new System.Type[] {
                typeof(string), typeof(int), typeof(int), typeof(int),
                typeof(long), typeof(string), typeof(Vector2i), typeof(bool)
            })]
        public static class Inventory_AddItemNamed_Featherweight
        {
            [HarmonyPrefix]
            public static void Prefix(Inventory __instance) => Guard(__instance);
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.HaveEmptySlot))]
        public static class Inventory_HaveEmptySlot_Featherweight
        {
            [HarmonyPrefix]
            public static void Prefix(Inventory __instance) => Guard(__instance);
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetEmptySlots))]
        public static class Inventory_GetEmptySlots_Featherweight
        {
            [HarmonyPrefix]
            public static void Prefix(Inventory __instance) => Guard(__instance);
        }

        /// <summary>Never let a failure here block a vanilla inventory operation —
        /// worst case the player is back to the pre-fix behaviour, not stuck unable
        /// to pick anything up.</summary>
        private static void Guard(Inventory inv)
        {
            try { FeatherweightInventory.EnsureExpanded(inv); }
            catch { /* capacity check proceeds on the un-corrected height */ }
        }
    }
}
