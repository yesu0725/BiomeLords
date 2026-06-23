using System.Reflection;
using HarmonyLib;
using UnityEngine;
using BiomeLords.Config;
using BiomeLords.Util;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Featherweight (Fallen Valkyrie Lord blessing) — raised carry-weight cap.
    ///
    /// Vanilla gates every over-encumbrance penalty on IsEncumbered(), which is
    /// simply <c>GetTotalWeight() &gt; GetMaxCarryWeight()</c>. By raising the
    /// reported max carry weight to the configured cap (default 1000) while the
    /// blessing is active, the player gets NO penalty below the cap — they walk,
    /// run, dodge and regenerate stamina normally and show no encumbered
    /// animation, and auto-pickup keeps working up to the cap. Only at the cap
    /// does the normal encumbered state engage. No other patches needed: the
    /// engine already keys all of that off GetMaxCarryWeight().
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.GetMaxCarryWeight))]
    public static class Player_GetMaxCarryWeight_Featherweight
    {
        [HarmonyPostfix]
        public static void Postfix(Player __instance, ref float __result)
        {
            float cap = LordConfig.FallerValkyrieWeightCap?.Value ?? 0f;
            if (cap <= 0f) return;
            if (!FeatherweightInventory.HasBlessing(__instance)) return;

            // Only raise — never clamp down a legitimately higher value.
            if (__result < cap) __result = cap;
        }
    }

    /// <summary>
    /// Keep the inventory weight readout showing the player's BASE capacity
    /// (e.g. 300, or 450 with a Megingjord) rather than the raised Featherweight
    /// cap. The cap is communicated through the blessing tooltip / compendium, not
    /// the HUD number. Recomputes the natural max via SEMan.ModifyMaxCarryWeight
    /// (the Featherweight SE contributes nothing there — the cap is applied in the
    /// GetMaxCarryWeight postfix above, which this display deliberately ignores).
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), "UpdateInventoryWeight")]
    public static class InventoryGui_UpdateInventoryWeight_Featherweight
    {
        // Accessed via reflection so we don't take a compile-time dependency on
        // Unity.TextMeshPro (m_weight is a TMP_Text) just to rewrite its string.
        private static readonly FieldInfo WeightField =
            AccessTools.Field(typeof(InventoryGui), "m_weight");

        [HarmonyPostfix]
        public static void Postfix(InventoryGui __instance, Player player)
        {
            if (player == null || __instance == null) return;
            if (!FeatherweightInventory.HasBlessing(player)) return;

            var weight = WeightField?.GetValue(__instance);
            if (weight == null) return;
            var textProp = weight.GetType().GetProperty("text");
            if (textProp == null) return;

            int current = Mathf.CeilToInt(player.GetInventory().GetTotalWeight());

            float natural = player.m_maxCarryWeight;
            player.GetSEMan()?.ModifyMaxCarryWeight(natural, ref natural);
            int baseMax = Mathf.CeilToInt(natural);

            string text = (current > baseMax && Mathf.Sin(Time.time * 10f) > 0f)
                ? $"<color=red>{current}</color>/{baseMax}"
                : $"{current}/{baseMax}";

            textProp.SetValue(weight, text);
        }
    }
}
