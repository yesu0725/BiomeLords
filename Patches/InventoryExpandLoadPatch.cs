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
    /// the vanilla 8×4 and items saved in the Featherweight rows arrive with
    /// y ≥ 4 — outside the grid the height advertises. This prefix pre-grows the
    /// player inventory to a safe ceiling BEFORE the items are read;
    /// BlessingPersistencePatch (Player.OnSpawned) then reconciles the height down
    /// to the correct value for the player's blessing.
    ///
    /// What this does NOT do, as of Valheim 1.0: it is not what keeps those items
    /// alive. Inventory.Load adds each item through
    /// AddItem(int prefabHash, ItemData) whose skipValidPositionCheck defaults to
    /// TRUE, and the bounds test it reaches is
    /// `y >= m_height &amp;&amp; !skipValidPositionCheck` — so an out-of-grid row is
    /// accepted as-is and nothing is compacted or destroyed at load time. The real
    /// threat to those items comes later in the same spawn, when vanilla's
    /// Player.SetInventorySize runs Humanoid.DropInvalidItems; see
    /// FeatherweightInventorySizePatch, which is what actually rescues them.
    ///
    /// The pre-grow is kept regardless: it makes m_height agree with where the items
    /// actually sit for the window between Load and OnSpawned (anything reading the
    /// height in that window — another mod, a UI refresh — sees the true grid), and
    /// it is cheap insurance if that skipValidPositionCheck default ever flips back.
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
