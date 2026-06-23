using System.Collections.Generic;
using UnityEngine;
using BiomeLords.Config;
using BiomeLords.Util;

namespace BiomeLords.Phase1C
{
    /// <summary>
    /// Lord's Pedestal blessing system (rewrite for permanent-blessing + charges model).
    ///
    /// Rules:
    ///   • One blessing active at a time. Applying a new one removes any previous.
    ///   • Blessings are permanent — the active one is stored in Player.m_customData
    ///     and re-applied on every spawn (BlessingPersistencePatch), so it survives
    ///     logout and death; it only changes when the player applies a different one.
    ///   • Each mounted trophy has a charges pool (configurable, default 5). Each
    ///     tap-E uses one charge. On the LAST use (charges decrement to 0), the
    ///     trophy crumbles to dust with FX + message.
    ///   • Charges are stored on the pedestal's ZDO (key "biomelords.charges").
    ///   • Charges are RE-initialised to max whenever a fresh trophy is mounted.
    /// </summary>
    public static class BlessingSystem
    {
        private const string ChargesZDOKey  = "biomelords.charges";
        private const string CooldownZDOKey = "biomelords.bless_next"; // world-time (double) when next blessing is allowed
        public  const double CooldownSeconds = 10.0;

        /// <summary>Player.m_customData key holding the currently-active blessing SE
        /// name. m_customData is serialized with the character, so the blessing
        /// survives logout (and death) — BlessingPersistencePatch re-applies it on
        /// spawn.</summary>
        public const string ActiveBlessingKey = "biomelords.blessing";

        private static readonly Dictionary<string, (string lordId, string seName, string gpName)> ByTrophy =
            new Dictionary<string, (string, string, string)>
            {
                { TrophyFactory.NeckLordTrophy,
                  ("neck_lord", StatusEffectFactory.NeckLordSpiritSE, GuardianPowerFactory.NeckLordGP) },
                { TrophyFactory.GreydwarfLordTrophy,
                  ("greydwarf_lord", StatusEffectFactory.GreydwarfLordSpiritSE, GuardianPowerFactory.GreydwarfLordGP) },
                { TrophyFactory.DraugrLordTrophy,
                  ("draugr_lord", StatusEffectFactory.DraugrLordSpiritSE, GuardianPowerFactory.DraugrLordGP) },
                { TrophyFactory.FenringLordTrophy,
                  ("fenring_lord", StatusEffectFactory.FenringLordSpiritSE, GuardianPowerFactory.FenringLordGP) },
                { TrophyFactory.LoxLordTrophy,
                  ("lox_lord", StatusEffectFactory.LoxLordSpiritSE, GuardianPowerFactory.LoxLordGP) },
                { TrophyFactory.SeekerLordTrophy,
                  ("seeker_lord", StatusEffectFactory.SeekerLordSpiritSE, GuardianPowerFactory.SeekerLordGP) },
                { TrophyFactory.FallerValkyrieLordTrophy,
                  ("faller_valkyrie_lord", StatusEffectFactory.FallerValkyrieLordSpiritSE, GuardianPowerFactory.FallerValkyrieLordGP) },
            };

        public static bool TryGetGuardianPower(string attached, out string gpName)
        {
            gpName = null;
            if (string.IsNullOrEmpty(attached)) return false;
            if (ByTrophy.TryGetValue(attached, out var v)) { gpName = v.gpName; return true; }
            var stripped = attached.TrimStart('$');
            if (ByTrophy.TryGetValue(stripped, out v)) { gpName = v.gpName; return true; }
            return false;
        }

        public static bool TryResolve(string attached, out string lordId, out string seName)
        {
            lordId = seName = null;
            if (string.IsNullOrEmpty(attached)) return false;
            if (ByTrophy.TryGetValue(attached, out var v)) { lordId = v.lordId; seName = v.seName; return true; }
            var stripped = attached.TrimStart('$');
            if (ByTrophy.TryGetValue(stripped, out v)) { lordId = v.lordId; seName = v.seName; return true; }
            return false;
        }

        private static ZDO Zdo(ItemStand stand)
        {
            if (stand == null) return null;
            var nv = stand.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return null;
            return nv.GetZDO();
        }

        /// <summary>Reads remaining charges on a pedestal, initialising if absent.</summary>
        public static int GetCharges(ItemStand stand)
        {
            var z = Zdo(stand);
            if (z == null) return 0;
            return z.GetInt(ChargesZDOKey, LordConfig.BlessingChargesPerTrophy.Value);
        }

        /// <summary>
        /// Reset to max — only if the pedestal is fresh (no charges key yet)
        /// or depleted (≤ 0). Crucially this guards against the mount-hooks
        /// firing during ongoing use (vanilla SetVisualItem / UseItem can be
        /// re-invoked on RPC sync, refresh, etc.). Without this gate, every
        /// blessing tap would bounce charges back to max.
        /// </summary>
        public static void ResetCharges(ItemStand stand)
        {
            var z = Zdo(stand);
            if (z == null) return;
            int current = z.GetInt(ChargesZDOKey, -1);
            if (current > 0) return;
            z.Set(ChargesZDOKey, LordConfig.BlessingChargesPerTrophy.Value);
        }

        public static void SetCharges(ItemStand stand, int value)
        {
            Zdo(stand)?.Set(ChargesZDOKey, value);
        }

        public static int MaxCharges => LordConfig.BlessingChargesPerTrophy.Value;

        /// <summary>Seconds remaining on the pedestal's anti-spam cooldown. 0 if ready.</summary>
        public static double CooldownRemaining(ItemStand stand)
        {
            var z = Zdo(stand);
            if (z == null || ZNet.instance == null) return 0;
            double next = z.GetFloat(CooldownZDOKey, 0f); // stored as float — small range fits fine
            return System.Math.Max(0, next - ZNet.instance.GetTimeSeconds());
        }

        private static void StartCooldown(ItemStand stand)
        {
            var z = Zdo(stand);
            if (z == null || ZNet.instance == null) return;
            z.Set(CooldownZDOKey, (float)(ZNet.instance.GetTimeSeconds() + CooldownSeconds));
        }

        /// <summary>Try to grant a blessing. Returns true if granted.</summary>
        public static bool TryGrant(Player p, ItemStand stand, string trophyAttached)
        {
            if (p == null || stand == null) return false;
            if (!TryResolve(trophyAttached, out var lordId, out var seName))
            {
                p.Message(MessageHud.MessageType.Center, "$biomelords_pedestal_unknown_trophy");
                return false;
            }

            int charges = GetCharges(stand);
            if (charges <= 0)
            {
                p.Message(MessageHud.MessageType.Center, "$biomelords_blessing_spent");
                return false;
            }

            // Anti-spam cooldown — prevents the player from accidentally
            // burning multiple charges by mashing E.
            double cd = CooldownRemaining(stand);
            if (cd > 0)
            {
                p.Message(MessageHud.MessageType.Center,
                    $"$biomelords_blessing_cooldown ({(int)System.Math.Ceiling(cd)}s)");
                return false;
            }

            if (!StatusEffectFactory.ByName.TryGetValue(seName, out var se) || se == null)
            {
                Jotunn.Logger.LogWarning($"[BiomeLords] Blessing SE {seName} missing from registry.");
                return false;
            }

            // Mutually exclusive — clear any other blessing SE before applying the new one.
            RemoveOtherBlessings(p, se);

            // Icon is pre-assigned via IconAssignment (vanilla item icon, not the trophy).
            p.GetSEMan().AddStatusEffect(se, resetTime: true);

            // Persist the active blessing so it survives logout/death, and grant
            // the Featherweight inventory rows immediately if this is that blessing.
            SetActiveBlessing(p, seName);
            if (seName == StatusEffectFactory.FallerValkyrieLordSpiritSE)
                FeatherweightInventory.Reconcile(p);

            // Decrement charges + start cooldown.
            int newCharges = charges - 1;
            SetCharges(stand, newCharges);
            StartCooldown(stand);

            if (LordConfig.DebugLogging.Value)
                Jotunn.Logger.LogInfo($"[BiomeLords] Granted {seName}. Charges: {charges} -> {newCharges}.");

            // Last-use ceremony.
            if (newCharges == 0)
            {
                ConsumeTrophy(p, stand, lordId);
            }
            return true;
        }

        private static void RemoveOtherBlessings(Player p, StatusEffect keep)
        {
            var seman = p.GetSEMan();
            if (seman == null) return;
            int keepHash = keep.NameHash();

            // If we're switching AWAY from Featherweight, collapse its extra
            // inventory rows first (spilling any items into a CargoCrate) while the
            // SE is still present, then remove the SE.
            int featherHash = FeatherweightInventory.SeHash;
            if (keepHash != featherHash && seman.HaveStatusEffect(featherHash))
                FeatherweightInventory.Collapse(p);

            foreach (var h in StatusEffectFactory.BlessingHashes)
            {
                if (h == keepHash) continue;
                if (seman.HaveStatusEffect(h)) seman.RemoveStatusEffect(h);
            }
        }

        /// <summary>Read the persisted active-blessing SE name (or null).</summary>
        public static string GetActiveBlessing(Player p)
        {
            if (p?.m_customData != null &&
                p.m_customData.TryGetValue(ActiveBlessingKey, out var v) &&
                !string.IsNullOrEmpty(v))
                return v;
            return null;
        }

        private static void SetActiveBlessing(Player p, string seName)
        {
            if (p?.m_customData == null) return;
            p.m_customData[ActiveBlessingKey] = seName;
        }

        /// <summary>The trophy's last charge was just consumed — destroy it
        /// from the pedestal with FX + center message.</summary>
        private static void ConsumeTrophy(Player p, ItemStand stand, string lordId)
        {
            var pos = stand.transform.position + Vector3.up * 1.0f;

            // Crumble FX — gentle puff + golden lootspawn-style pop.
            FxLibrary.TrySpawn("vfx_corpse_destruction_small", pos);
            FxLibrary.TrySpawn("vfx_lootspawn",                pos);
            FxLibrary.TrySpawn("fx_redlightning_burst",        pos);

            // Destroy the mounted trophy.
            stand.DestroyAttachment();

            p.Message(MessageHud.MessageType.Center, "$biomelords_blessing_lastuse");
            if (LordConfig.DebugLogging.Value)
                Jotunn.Logger.LogInfo($"[BiomeLords] Trophy consumed (lord={lordId}). Pedestal cleared.");
        }
    }
}
