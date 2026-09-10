using System.Collections.Generic;
using System.Reflection;
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
    [HarmonyPatch]
    public static class Inventory_Load_FeatherweightExpand
    {
        /// <summary>
        /// Valheim 1.0 added a second Load overload — Load(ZPackage, bool) alongside
        /// Load(ZPackage) — which made a name-only patch target ambiguous. Both have
        /// the same body, so hook every Load overload rather than betting on which
        /// one a given save path (or another mod) routes through.
        /// </summary>
        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var m in AccessTools.GetDeclaredMethods(typeof(Inventory)))
                if (m.Name == nameof(Inventory.Load))
                    yield return m;
        }

        [HarmonyPrefix]
        public static void Prefix(Inventory __instance)
        {
            if (FeatherweightInventory.IsPlayerInventory(__instance))
                FeatherweightInventory.GrowForLoad(__instance);
        }
    }
}
