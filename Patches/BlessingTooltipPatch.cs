using System.Collections.Generic;
using HarmonyLib;
using BiomeLords.Config;
using BiomeLords.Phase1C;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Keeps blessing / Forsaken Power descriptions in step with the admin config.
    ///
    /// The English tooltip strings in ItemFactory carry placeholders such as
    /// <c>{weight_cap}</c> or <c>{hearth_pct}</c> instead of hard-coded numbers.
    /// Every place Valheim shows an SE description (compendium "Active Effects"
    /// tab, HUD hover, pedestal/guardian-power tooltips) goes through
    /// <c>StatusEffect.GetTooltipString()</c> and then <c>Localization.Localize()</c>,
    /// so this postfix localises the token itself and fills the placeholders
    /// with the LIVE config values. Reading the config at display time means
    /// server-synced values and mid-session edits show up without a restart,
    /// and the SE instances SEMan clones per player need no bookkeeping.
    ///
    /// The later vanilla Localize() call is a no-op on already-localised text.
    /// </summary>
    public static class BlessingTooltipValues
    {
        private static readonly HashSet<string> OurTooltips = new HashSet<string>();

        /// <summary>True for tooltip tokens belonging to our blessings and powers.</summary>
        public static bool IsOurs(string tooltipToken)
        {
            if (string.IsNullOrEmpty(tooltipToken)) return false;
            if (OurTooltips.Count == 0)
            {
                foreach (var se in StatusEffectFactory.ByName.Values)
                    if (se != null && !string.IsNullOrEmpty(se.m_tooltip)) OurTooltips.Add(se.m_tooltip);
                foreach (var se in GuardianPowerFactory.ByName.Values)
                    if (se != null && !string.IsNullOrEmpty(se.m_tooltip)) OurTooltips.Add(se.m_tooltip);
            }
            return OurTooltips.Contains(tooltipToken);
        }

        /// <summary>Replace every placeholder in an already-localised tooltip.</summary>
        public static string Fill(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;

            int rows = LordConfig.FallerValkyrieExtraRows?.Value ?? 2;
            return text
                .Replace("{bait_save_pct}",  Pct(LordConfig.FisherBoonBaitSaveChance?.Value  ?? 0.5f))
                .Replace("{bonus_fish_pct}", Pct(LordConfig.FisherBoonBonusFishChance?.Value ?? 0.25f))
                .Replace("{hearth_pct}",     Pct((LordConfig.HearthMasterMultiplier?.Value ?? 2f) - 1f))
                .Replace("{refiner_pct}",    Pct(LordConfig.RefinersTouchChance?.Value ?? 0.5f))
                .Replace("{weight_cap}",     (LordConfig.FallerValkyrieWeightCap?.Value ?? 1000f).ToString("0"))
                .Replace("{extra_rows_text}", rows == 1 ? "+1 inventory row" : $"+{rows} inventory rows")
                .Replace("{rally_radius}",   (LordConfig.ValkyrieRallyRadius?.Value ?? 20f).ToString("0.#"))
                .Replace("{rally_rested}",   Duration(LordConfig.ValkyrieRallyRestedSeconds?.Value ?? 1200f))
                .Replace("{snow_shed_radius}", (LordConfig.FimbulHideSnowShedRadius?.Value ?? 30f).ToString("0.#"))
                .Replace("{petrify_seconds}",  (LordConfig.PetrifyDuration?.Value ?? 6f).ToString("0.#"))
                .Replace("{shatter_radius}",   (LordConfig.PetrifyShatterRadius?.Value ?? 8f).ToString("0.#"))
                .Replace("{shatter_damage}",   (LordConfig.PetrifyShatterDamage?.Value ?? 80f).ToString("0"));
        }

        private static string Pct(float fraction) => (fraction * 100f).ToString("0.#");

        /// <summary>"20-minute" for whole minutes, otherwise "90-second".</summary>
        private static string Duration(float seconds)
        {
            if (seconds >= 60f && seconds % 60f == 0f) return $"{seconds / 60f:0}-minute";
            return $"{seconds:0}-second";
        }

        public static void Apply(StatusEffect se, ref string result)
        {
            if (se == null || !IsOurs(se.m_tooltip)) return;
            if (string.IsNullOrEmpty(result)) return;
            result = Fill(Localization.instance.Localize(result));
        }
    }

    // GetTooltipString is virtual and SE_Stats (every SE we register) overrides
    // it, so both the base and the override are patched — Harmony patches the
    // concrete method, not the virtual slot.
    [HarmonyPatch(typeof(StatusEffect), nameof(StatusEffect.GetTooltipString))]
    public static class StatusEffect_GetTooltipString_BlessingValues
    {
        [HarmonyPostfix]
        public static void Postfix(StatusEffect __instance, ref string __result)
            => BlessingTooltipValues.Apply(__instance, ref __result);
    }

    [HarmonyPatch(typeof(SE_Stats), nameof(SE_Stats.GetTooltipString))]
    public static class SE_Stats_GetTooltipString_BlessingValues
    {
        [HarmonyPostfix]
        public static void Postfix(SE_Stats __instance, ref string __result)
            => BlessingTooltipValues.Apply(__instance, ref __result);
    }
}
