using HarmonyLib;
using BiomeLords.Config;
using BiomeLords.Phase1C;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Hearth Master — Lox Lord blessing. Each food buff you eat lasts
    /// HearthMasterMultiplier × longer. Implemented as a postfix on
    /// Player.EatFood that reaches into the just-added Food entry and
    /// scales its remaining time.
    ///
    /// Vanilla Player.EatFood adds a new Food to m_foods (or refreshes the
    /// existing one of the same item type). Either way the entry's m_time
    /// is the freshly-set burn timer — multiplying it here extends the buff
    /// without changing the shared item asset.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.EatFood))]
    public static class Player_EatFood_HearthMaster
    {
        private static int _seHash;

        [HarmonyPostfix]
        public static void Postfix(Player __instance, ItemDrop.ItemData item, bool __result)
        {
            if (!__result) return;
            if (__instance == null || __instance != Player.m_localPlayer) return;
            if (item == null) return;
            if (_seHash == 0) _seHash = StatusEffectFactory.LoxLordSpiritSE.GetStableHashCode();
            var seman = __instance.GetSEMan();
            if (seman == null || !seman.HaveStatusEffect(_seHash)) return;

            float mult = LordConfig.HearthMasterMultiplier.Value;
            if (mult <= 1f) return;

            // Find the matching food entry. The just-eaten food matches by item.
            var foods = __instance.GetFoods();
            if (foods == null) return;
            for (int i = 0; i < foods.Count; i++)
            {
                var f = foods[i];
                if (f == null || f.m_item == null) continue;
                if (f.m_item.m_shared != item.m_shared) continue;
                // Bump current remaining time AND the max-time tag so the HUD
                // bar reflects the longer duration.
                f.m_time *= mult;
                break;
            }
        }
    }
}
