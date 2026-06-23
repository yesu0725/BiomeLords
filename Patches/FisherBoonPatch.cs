using HarmonyLib;
using UnityEngine;
using BiomeLords.Config;
using BiomeLords.Phase1C;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Fisher's Boon — Neck Lord blessing. Two effects, gated on the local
    /// player having SE_NeckLordSpirit active:
    ///   • Bait save: chance that a fishing cast doesn't consume bait.
    ///   • Bonus fish: chance for an extra fish copy when picked up.
    /// </summary>
    internal static class FisherBoonUtil
    {
        private static int _seHash;

        public static bool HasBlessing(Player p)
        {
            if (p == null || p != Player.m_localPlayer) return false;
            if (_seHash == 0) _seHash = StatusEffectFactory.NeckLordSpiritSE.GetStableHashCode();
            var seman = p.GetSEMan();
            return seman != null && seman.HaveStatusEffect(_seHash);
        }
    }

    /// <summary>FishingFloat.Setup runs immediately after the cast — we refund
    /// 1 bait of the same type with configured chance.</summary>
    [HarmonyPatch(typeof(FishingFloat), "Setup")]
    public static class FishingFloat_Setup_FisherBoon
    {
        [HarmonyPostfix]
        public static void Postfix(Character owner, ItemDrop.ItemData ammo)
        {
            if (!(owner is Player p) || !FisherBoonUtil.HasBlessing(p)) return;
            if (ammo == null || ammo.m_dropPrefab == null) return;
            if (Random.value >= LordConfig.FisherBoonBaitSaveChance.Value) return;

            var inv = p.GetInventory();
            if (inv == null) return;
            inv.AddItem(ammo.m_dropPrefab, 1);
            p.Message(MessageHud.MessageType.TopLeft, "Fisher's Boon — bait spared.");

            if (LordConfig.DebugLogging.Value)
                Jotunn.Logger.LogInfo($"[BiomeLords] FisherBoon bait refunded: {ammo.m_dropPrefab.name}");
        }
    }

    /// <summary>Fish.Pickup → if it succeeded, roll for a bonus fish copy.</summary>
    [HarmonyPatch(typeof(Fish), nameof(Fish.Pickup))]
    public static class Fish_Pickup_FisherBoon
    {
        [HarmonyPostfix]
        public static void Postfix(Fish __instance, Humanoid character, bool __result)
        {
            if (!__result) return;
            if (!(character is Player p) || !FisherBoonUtil.HasBlessing(p)) return;
            if (__instance == null || __instance.m_pickupItem == null) return;
            if (Random.value >= LordConfig.FisherBoonBonusFishChance.Value) return;

            var inv = p.GetInventory();
            if (inv == null) return;
            inv.AddItem(__instance.m_pickupItem, 1);
            p.Message(MessageHud.MessageType.TopLeft, "Fisher's Boon — a second catch!");

            if (LordConfig.DebugLogging.Value)
                Jotunn.Logger.LogInfo($"[BiomeLords] FisherBoon bonus fish: {__instance.m_pickupItem.name}");
        }
    }
}
