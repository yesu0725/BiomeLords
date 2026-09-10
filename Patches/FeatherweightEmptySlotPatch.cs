using System.Reflection;
using HarmonyLib;
using UnityEngine;
using BiomeLords.Util;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Featherweight's extra rows must also be REACHABLE, not just counted.
    ///
    /// FeatherweightCapacityPatches keeps `m_height` correct, which is enough for
    /// vanilla: `Inventory.FindEmptySlot` walks `m_height` and so finds the extra
    /// rows on its own. But a slot-finder that hardcodes its row count sees only
    /// the base grid, and then "is there room?" and "where does it go?" disagree —
    /// capacity says yes, the slot-finder returns (-1, -1), and the player gets
    /// "inventory full" with two visibly empty rows on screen.
    ///
    /// ComfyQuickSlots does exactly that: it prefixes `Inventory.FindEmptySlot`
    /// with `return false` and answers from `QuickSlotsManager.GetEmptyInventorySlot`,
    /// which loops `y &lt; 5` — the vanilla 4 rows plus its own armor/quickslot row.
    /// Its `CanAddItem` / `HasEmptyNonEquipmentSlot` replacements meanwhile compute
    /// `m_width * m_height - 5`, so they DO count the Featherweight rows. Auto-pickup
    /// passed the capacity gate and then failed inside `AddItem`.
    ///
    /// Both patches here are postfixes that fire only when the slot-finder came back
    /// empty-handed, and both can only ever return a slot at y ≥ BaseHeight — rows
    /// that exist solely because Featherweight is active. They never touch the base
    /// grid, so no other mod's reserved row is at risk.
    /// </summary>
    public static class FeatherweightEmptySlotPatches
    {
        /// <summary>
        /// Vanilla `Inventory.FindEmptySlot`. A no-op on its own (vanilla already
        /// scans the full height), but it is the mod-agnostic safety net: it runs
        /// after ANY prefix that replaced the search, regardless of plugin load
        /// order, so a truncated result still gets an extra row when one is free.
        /// </summary>
        [HarmonyPatch(typeof(Inventory), "FindEmptySlot")]
        public static class Inventory_FindEmptySlot_Featherweight
        {
            [HarmonyPostfix]
            public static void Postfix(Inventory __instance, bool topFirst, ref Vector2i __result)
            {
                if (__result.x >= 0) return;
                try
                {
                    var slot = FeatherweightInventory.FindExtraRowSlot(__instance, topFirst);
                    if (slot.x >= 0) __result = slot;
                }
                catch { /* leave the un-corrected result — same as before the fix */ }
            }
        }

        /// <summary>
        /// ComfyQuickSlots' `QuickSlotsManager.GetEmptyInventorySlot` itself. Patching
        /// the source rather than only `Inventory.FindEmptySlot` covers CQS's OTHER
        /// caller too: `Humanoid.UnequipItem`, which checks `HasEmptyNonEquipmentSlot`
        /// (counts our rows, says yes) and then moves the armour to whatever this
        /// returns — (-1, -1) with the base grid full, which would strand the piece.
        ///
        /// `Prepare` returning false skips the class entirely when CQS is absent, so
        /// there is no missing-target error for the vast majority of installs.
        /// </summary>
        [HarmonyPatch]
        public static class ComfyQuickSlots_GetEmptyInventorySlot_Featherweight
        {
            private const string ManagerType = "ComfyQuickSlots.QuickSlotsManager";

            private static MethodBase _target;

            public static bool Prepare()
            {
                if (!FeatherweightInventory.ComfyQuickSlotsLoaded) return false;

                var type = AccessTools.TypeByName(ManagerType);
                _target = type == null
                    ? null
                    : AccessTools.Method(type, "GetEmptyInventorySlot",
                        new System.Type[] { typeof(Inventory), typeof(bool) });

                if (_target == null)
                    Jotunn.Logger.LogWarning(
                        "[BiomeLords] ComfyQuickSlots is installed but its GetEmptyInventorySlot " +
                        "could not be found — Featherweight's extra rows may not accept picked-up " +
                        "items. Vanilla FindEmptySlot is still patched as a fallback.");

                return _target != null;
            }

            public static MethodBase TargetMethod() => _target;

            [HarmonyPostfix]
            public static void Postfix(Inventory inventory, bool topFirst, ref Vector2i __result)
            {
                if (__result.x >= 0) return;
                try
                {
                    var slot = FeatherweightInventory.FindExtraRowSlot(inventory, topFirst);
                    if (slot.x >= 0) __result = slot;
                }
                catch { /* leave the un-corrected result */ }
            }
        }
    }
}
