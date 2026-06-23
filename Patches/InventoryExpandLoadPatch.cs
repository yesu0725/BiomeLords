using HarmonyLib;
using BiomeLords.Util;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Featherweight inventory persistence — load half.
    ///
    /// Valheim saves inventory items with their grid positions but NOT the
    /// inventory's dimensions, so the player inventory is always reconstructed at
    /// the vanilla 8×4. If the player had the Featherweight rows expanded and left
    /// items in them, those items load with y ≥ 4 and Inventory.AddItem would
    /// compact them into the base grid — or destroy them outright if the base grid
    /// is full. To prevent that, we pre-grow the player inventory to a safe ceiling
    /// BEFORE the items are read. BlessingPersistencePatch (Player.OnSpawned) then
    /// reconciles the height down to the correct value for the player's blessing.
    /// </summary>
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.Load))]
    public static class Inventory_Load_FeatherweightExpand
    {
        [HarmonyPrefix]
        public static void Prefix(Inventory __instance)
        {
            if (FeatherweightInventory.IsPlayerInventory(__instance))
                FeatherweightInventory.GrowForLoad(__instance);
        }
    }
}
