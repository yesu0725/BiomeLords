using HarmonyLib;
using BiomeLords.Phase1C;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Lord's Pedestal cannot be removed while a trophy is mounted. Player
    /// must consume all charges (trophy crumbles) before the pedestal can
    /// be Hammer-destroyed.
    /// </summary>
    [HarmonyPatch(typeof(Player), "CheckCanRemovePiece")]
    public static class Player_CheckCanRemovePiece_LockOccupied
    {
        [HarmonyPostfix]
        public static void Postfix(Player __instance, Piece piece, ref bool __result)
        {
            if (!__result) return;
            if (piece == null) return;
            var stand = piece.GetComponent<ItemStand>();
            if (stand == null) return;
            if (stand.GetComponent<LordsPedestalTag>() == null) return;
            if (!stand.HaveAttachment()) return;

            __result = false;
            if (__instance != null)
                __instance.Message(MessageHud.MessageType.Center,
                    "$biomelords_pedestal_destroy_locked");
        }
    }

    /// <summary>Defence in depth — vanilla WearNTear.CanBeRemoved is also gated.</summary>
    [HarmonyPatch(typeof(WearNTear), "CanBeRemoved")]
    public static class WearNTear_CanBeRemoved_LockOccupied
    {
        [HarmonyPostfix]
        public static void Postfix(WearNTear __instance, ref bool __result)
        {
            if (!__result || __instance == null) return;
            var stand = __instance.GetComponent<ItemStand>();
            if (stand == null) return;
            if (stand.GetComponent<LordsPedestalTag>() == null) return;
            if (stand.HaveAttachment()) __result = false;
        }
    }
}
