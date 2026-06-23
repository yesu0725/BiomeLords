using HarmonyLib;
using BiomeLords.Config;
using BiomeLords.Phase1B;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Two-purpose patch on ItemDrop.ItemData.GetTooltip:
    ///   1. When DebugLogging is on, log the full tooltip text for the
    ///      Lord's Horn once per session so we can see WHERE "Utility" appears.
    ///   2. Backstop find/replace: if the rendered tooltip still contains the
    ///      word "Utility" for our horn, swap it for "Consumable". This is a
    ///      belt-and-braces fix in case Valheim labels items by something
    ///      other than m_itemType.
    /// </summary>
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip),
                  new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int) })]
    public static class ItemData_GetTooltip_Patch
    {
        private static bool _loggedOnce;

        [HarmonyPostfix]
        public static void Postfix(ItemDrop.ItemData item, ref string __result)
        {
            if (item?.m_dropPrefab == null) return;
            if (item.m_dropPrefab.name != ItemFactory.LordsHornPrefab) return;
            if (string.IsNullOrEmpty(__result)) return;

            if (!_loggedOnce && LordConfig.DebugLogging != null && LordConfig.DebugLogging.Value)
            {
                _loggedOnce = true;
                Jotunn.Logger.LogInfo($"[BiomeLords] Raw LordsHorn tooltip:\n----\n{__result}\n----");
            }

            // Backstop: ensure no "Utility" word leaks through, regardless of source.
            // Replace the word boundary form so we don't mangle unrelated text.
            __result = __result.Replace("Utility", "Consumable")
                               .Replace("utility", "consumable");
        }
    }
}
