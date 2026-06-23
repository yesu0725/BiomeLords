using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using BiomeLords.Config;
using BiomeLords.Phase1C;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Iron Vein — Draugr Elite Lord blessing. When the local player has the
    /// SE active and a DropTable rolls Iron or IronScrap, each such drop has
    /// a chance (config) to be duplicated. Patches DropTable.GetDropList
    /// postfix, the single chokepoint for swamp ore drops (MudPile, scrap
    /// rocks, iron deposits in crypts).
    /// </summary>
    [HarmonyPatch(typeof(DropTable), nameof(DropTable.GetDropList), new System.Type[0])]
    public static class DropTable_GetDropList_IronVein
    {
        private const float Chance = 0.30f;

        private static int _seHash;

        [HarmonyPostfix]
        public static void Postfix(List<GameObject> __result)
        {
            if (__result == null || __result.Count == 0) return;
            var p = Player.m_localPlayer;
            if (p == null || p.IsDead()) return;
            if (_seHash == 0) _seHash = StatusEffectFactory.DraugrLordSpiritSE.GetStableHashCode();
            var seman = p.GetSEMan();
            if (seman == null || !seman.HaveStatusEffect(_seHash)) return;

            int original = __result.Count;
            for (int i = 0; i < original; i++)
            {
                var go = __result[i];
                if (go == null) continue;
                var name = go.name;
                if (name != "Iron" && name != "IronScrap") continue;
                if (Random.value < Chance) __result.Add(go);
            }
        }
    }
}
