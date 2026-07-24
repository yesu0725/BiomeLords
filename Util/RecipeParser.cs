using System.Collections.Generic;
using Jotunn.Configs;

namespace BiomeLords.Util
{
    /// <summary>
    /// Parses a "Item:Amount,Item:Amount,..." config string into Jotunn
    /// RequirementConfig entries. Silently skips malformed pairs and
    /// non-positive amounts. Falls back to the given defaults if the
    /// string is empty or nothing parses.
    /// </summary>
    public static class RecipeParser
    {
        public static RequirementConfig[] Parse(string csv, RequirementConfig[] fallback, bool recover, string logContext)
        {
            var list = new List<RequirementConfig>();
            if (!string.IsNullOrWhiteSpace(csv))
            {
                foreach (var part in csv.Split(','))
                {
                    var trimmed = part.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;
                    var kv = trimmed.Split(':');
                    if (kv.Length != 2) continue;
                    var item = kv[0].Trim();
                    if (string.IsNullOrEmpty(item)) continue;
                    if (!int.TryParse(kv[1].Trim(), out var amount)) continue;
                    if (amount <= 0) continue;
                    list.Add(new RequirementConfig { Item = item, Amount = amount, Recover = recover });
                }
            }

            if (list.Count == 0)
            {
                Jotunn.Logger.LogWarning($"[BiomeLords] {logContext} recipe empty / unparseable — falling back to defaults.");
                return fallback;
            }

            return list.ToArray();
        }
    }
}
