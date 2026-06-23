using HarmonyLib;
using BiomeLords.Phase1C;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Lord's Pedestal input routing — blessings only.
    ///   • Tap E   → blessing (BlessingSystem)
    ///   • Hold E  → BLOCKED while a trophy is mounted (trophy is bound to the
    ///              pedestal until its charges are spent).
    ///   • Shift+E → vanilla
    /// </summary>
    [HarmonyPatch(typeof(ItemStand), nameof(ItemStand.Interact))]
    public static class ItemStand_Interact_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(ItemStand __instance, Humanoid user, bool hold, bool alt, ref bool __result)
        {
            if (__instance == null) return true;
            if (__instance.GetComponent<LordsPedestalTag>() == null) return true;

            var player = user as Player;

            // Block trophy removal — once mounted, the trophy stays until
            // crumbled by its last blessing use.
            if (hold && __instance.HaveAttachment())
            {
                player?.Message(MessageHud.MessageType.Center, "$biomelords_pedestal_locked");
                __result = false;
                return false;
            }

            if (hold || alt) return true; // empty pedestal + hold/alt → vanilla
            if (!__instance.HaveAttachment()) return true;
            if (player == null) return true;

            __result = BlessingSystem.TryGrant(player, __instance, __instance.GetAttachedItem());
            return false;
        }
    }

    /// <summary>
    /// Lord's Pedestal only accepts Lord trophies. Reject every other item
    /// in CanAttach so the vanilla UI shows the "cannot attach" feedback.
    /// </summary>
    [HarmonyPatch(typeof(ItemStand), "CanAttach")]
    public static class ItemStand_CanAttach_LordsOnly
    {
        [HarmonyPostfix]
        public static void Postfix(ItemStand __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (!__result) return; // vanilla already said no
            if (__instance == null || item == null) return;
            if (__instance.GetComponent<LordsPedestalTag>() == null) return;

            var prefabName = item.m_dropPrefab != null ? item.m_dropPrefab.name : null;
            // Resolve against the trophy → blessing map; if it's not a Lord
            // trophy, deny attachment.
            if (!BlessingSystem.TryResolve(prefabName, out _, out _))
                __result = false;
        }
    }

    /// <summary>
    /// Defence in depth — also block any non-Interact path that might try to
    /// drop the trophy back to the world. Our own ConsumeTrophy uses
    /// DestroyAttachment (different method) so we don't block ourselves.
    /// </summary>
    [HarmonyPatch(typeof(ItemStand), "DropItem")]
    public static class ItemStand_DropItem_LockTrophy
    {
        [HarmonyPrefix]
        public static bool Prefix(ItemStand __instance)
        {
            if (__instance == null) return true;
            if (__instance.GetComponent<LordsPedestalTag>() == null) return true;
            // Refuse — trophy is locked.
            return false;
        }
    }

    /// <summary>
    /// When a player attaches a new trophy to a Lord's Pedestal:
    ///   • Reset charges to max.
    ///   • Play a ceremonial FX + sound stack at the pedestal.
    /// </summary>
    [HarmonyPatch(typeof(ItemStand), nameof(ItemStand.UseItem))]
    public static class ItemStand_UseItem_ResetCharges
    {
        [HarmonyPostfix]
        public static void Postfix(ItemStand __instance, bool __result)
        {
            // Reset-charges still happens here as a safety net; the canonical
            // mount path runs through SetVisualItem (see below) which also
            // covers the ceremony so we don't fire it from both.
            if (!__result || __instance == null) return;
            if (__instance.GetComponent<LordsPedestalTag>() == null) return;
            var attached = __instance.GetAttachedItem();
            if (!BlessingSystem.TryResolve(attached, out _, out _)) return;
            BlessingSystem.ResetCharges(__instance);
        }

        /// <summary>Public so the SetVisualItem hook can call the same routine.</summary>
        internal static void PlayMountCeremonyPublic(UnityEngine.Vector3 pos) => PlayMountCeremony(pos);

        /// <summary>Non-green golden ceremony at the pedestal — golden lootspawn
        /// pops and a sparks ring. Deliberately omits the green effects
        /// "fx_summon_start" (summon burst) and "vfx_prespawn" (teal-green spawn
        /// swirl) so trophy placement shows no green VFX.</summary>
        private static void PlayMountCeremony(UnityEngine.Vector3 pos)
        {
            var center = pos + UnityEngine.Vector3.up * 0.6f;

            // Treasure-like golden pops — distinctly "you just bound a relic".
            BiomeLords.Util.FxLibrary.TrySpawn("vfx_lootspawn", center);
            BiomeLords.Util.FxLibrary.TrySpawn("vfx_lootspawn", center + UnityEngine.Vector3.up * 0.3f);
            BiomeLords.Util.FxLibrary.TrySpawn("vfx_lootspawn", center + UnityEngine.Vector3.up * 0.6f);

            // Sparks ring around the base for a "consecration" feel.
            for (int i = 0; i < 8; i++)
            {
                float ang = i * (360f / 8f);
                var off = UnityEngine.Quaternion.Euler(0f, ang, 0f) * UnityEngine.Vector3.forward * 1.2f;
                BiomeLords.Util.FxLibrary.TrySpawn("vfx_HitSparks", pos + off + UnityEngine.Vector3.up * 0.1f);
            }
        }
    }

    /// <summary>
    /// Canonical "trophy just got attached" hook. Fires reset-charges and the
    /// mount ceremony — but the ceremony only plays for a genuinely fresh
    /// mount, not when SetVisualItem replays on world load.
    ///
    /// Uses a ZDO-persisted flag (key = last-ceremonied trophy name): if the
    /// flag already matches the current trophy, skip the ceremony. Logging in
    /// triggers SetVisualItem with the existing trophy name → flag already
    /// matches → no ceremony.
    /// </summary>
    [HarmonyPatch(typeof(ItemStand), "SetVisualItem")]
    public static class ItemStand_SetVisualItem_MountHook
    {
        private const string CeremonyZDOKey = "biomelords.ceremony_for";

        [HarmonyPostfix]
        public static void Postfix(ItemStand __instance, string itemName)
        {
            if (__instance == null) return;
            if (__instance.GetComponent<LordsPedestalTag>() == null) return;

            var nv = __instance.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return;
            var zdo = nv.GetZDO();

            if (string.IsNullOrEmpty(itemName))
            {
                // Trophy removed/crumbled — clear so next fresh mount fires.
                zdo.Set(CeremonyZDOKey, "");
                return;
            }
            if (!BlessingSystem.TryResolve(itemName, out _, out _)) return;

            BlessingSystem.ResetCharges(__instance);

            var lastCeremonied = zdo.GetString(CeremonyZDOKey, "");
            if (lastCeremonied == itemName) return; // already played for this trophy
            zdo.Set(CeremonyZDOKey, itemName);

            ItemStand_UseItem_ResetCharges.PlayMountCeremonyPublic(__instance.transform.position);
        }
    }

    /// <summary>Pedestal hover overlay: trophy name + remaining charges.</summary>
    [HarmonyPatch(typeof(ItemStand), nameof(ItemStand.GetHoverText))]
    public static class ItemStand_GetHoverText_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ItemStand __instance, ref string __result)
        {
            if (__instance == null) return;
            if (__instance.GetComponent<LordsPedestalTag>() == null) return;
            if (!__instance.HaveAttachment()) return;

            var attached = __instance.GetAttachedItem();
            if (!BlessingSystem.TryResolve(attached, out _, out _)) return;

            int charges = BlessingSystem.GetCharges(__instance);
            int max     = BlessingSystem.MaxCharges;

            string line;
            if (charges <= 0)
            {
                line = "[<b><color=#a0a0a0>Spirit spent</color></b>]";
            }
            else
            {
                double cd = BlessingSystem.CooldownRemaining(__instance);
                if (cd > 0)
                {
                    int s = (int)System.Math.Ceiling(cd);
                    line = $"[<b><color=#ffaa66>Cooldown {s:D2}s</color></b>] <color=#a0a0a0>({charges}/{max})</color>";
                }
                else
                {
                    var useLine = Localization.instance.Localize(
                        "[<b><color=#80ddff>$KEY_Use</color></b>] $biomelords_pedestal_receive");
                    line = useLine + $" <color=#a0a0a0>({charges}/{max})</color>";
                }
            }
            // Reminder the trophy is bound until consumed.
            var lockedLine = Localization.instance.Localize("$biomelords_pedestal_lockedhint");
            __result += "\n" + line + "\n<color=#a0a0a0><i>" + lockedLine + "</i></color>";
        }
    }
}
