using HarmonyLib;
using BiomeLords.Phase1B;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Intercepts use of the Lord's Horn item. If the summon succeeds, consume
    /// one horn from the player's inventory and skip vanilla "drink" behaviour.
    /// On failure, suppress vanilla use too (we already messaged the player).
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
    public static class Humanoid_UseItem_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Humanoid __instance, ItemDrop.ItemData item)
        {
            if (item?.m_dropPrefab == null) return true;
            if (item.m_dropPrefab.name != ItemFactory.LordsHornPrefab) return true;

            var player = __instance as Player;
            if (player == null) return true;

            if (SummonService.TryUseHorn(player))
            {
                player.GetInventory().RemoveOneItem(item);
            }
            return false; // suppress vanilla use either way
        }
    }
}
