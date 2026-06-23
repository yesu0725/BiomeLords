using HarmonyLib;
using UnityEngine;
using BiomeLords.Config;
using BiomeLords.Phase1C;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Refiner's Touch — Seeker Lord blessing. When a Smelter / Blast Furnace /
    /// Spinning Wheel / Eitr Refinery spawns a finished product, and the local
    /// player is nearby AND has SE_SeekerLordSpirit active, roll for a bonus
    /// copy of the same item.
    ///
    /// Hooks Smelter.Spawn (the method that drops the produced item). All four
    /// stations share that base type so one patch covers them all.
    ///
    /// "Nearby" = within 30 m of the smelter — keeps the proc local to the
    /// player who's actually working the station, not whoever owns the chunk.
    /// </summary>
    [HarmonyPatch(typeof(Smelter), "Spawn")]
    public static class Smelter_Spawn_RefinersTouch
    {
        private const float Range = 30f;
        private static int _seHash;

        // Vanilla signature: void Smelter.Spawn(string ore, int stack).
        // Parameter name must match exactly — Harmony binds by name.
        [HarmonyPostfix]
        public static void Postfix(Smelter __instance, string ore, int stack)
        {
            if (__instance == null || string.IsNullOrEmpty(ore)) return;

            var p = Player.m_localPlayer;
            if (p == null || p.IsDead()) return;
            if ((p.transform.position - __instance.transform.position).sqrMagnitude >
                Range * Range) return;

            if (_seHash == 0) _seHash = StatusEffectFactory.SeekerLordSpiritSE.GetStableHashCode();
            var seman = p.GetSEMan();
            if (seman == null || !seman.HaveStatusEffect(_seHash)) return;

            if (Random.value >= LordConfig.RefinersTouchChance.Value) return;

            // IMPORTANT: `ore` is the INPUT item name (the conversion's m_from),
            // NOT the produced item. Vanilla Smelter.Spawn looks up the matching
            // ItemConversion and instantiates m_to. Mirror that so we drop a real
            // copy of the refined OUTPUT (e.g. Copper), not the raw input ore.
            ItemDrop output = null;
            foreach (var conv in __instance.m_conversion)
            {
                if (conv?.m_from != null && conv.m_from.gameObject.name == ore)
                {
                    output = conv.m_to;
                    break;
                }
            }
            if (output == null) return;

            // Spawn at the station's output transform, exactly like vanilla, and
            // run ItemDrop.OnCreateNew so the bonus drop is a properly registered,
            // networked item (stack-aware) rather than an uninitialised ghost.
            Transform outPoint = __instance.m_outputPoint != null
                ? __instance.m_outputPoint : __instance.transform;
            var go = Object.Instantiate(output.gameObject, outPoint.position, outPoint.rotation);
            var drop = go.GetComponent<ItemDrop>();
            if (drop != null)
            {
                drop.m_itemData.m_stack = stack;
                ItemDrop.OnCreateNew(drop);
            }

            p.Message(MessageHud.MessageType.TopLeft, "Refiner's Touch — extra output.");
            if (LordConfig.DebugLogging.Value)
                Jotunn.Logger.LogInfo($"[BiomeLords] RefinersTouch bonus: {output.gameObject.name} x{stack} from {__instance.name}");
        }
    }
}
