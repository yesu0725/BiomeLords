using HarmonyLib;
using BiomeLords.Phase1C;
using BiomeLords.Config;

namespace BiomeLords.Patches
{
    /// <summary>
    /// Belt-and-braces: after ObjectDB is ready, make sure every BiomeLords-
    /// registered StatusEffect is actually present in ObjectDB.m_StatusEffects
    /// AND in its hash table. Jotunn normally handles this, but in some
    /// versions/timings the GP-name lookup via Player.SetGuardianPower fails
    /// with "Missing stat for guardian power".
    /// </summary>
    [HarmonyPatch(typeof(ObjectDB), "Awake")]
    public static class ObjectDB_Awake_BiomeLordsInject
    {
        [HarmonyPostfix]
        public static void Postfix(ObjectDB __instance)
        {
            InjectAll(__instance);
        }

        /// <summary>Explicit inject — call after all SE factories have registered.</summary>
        public static void InjectAll(ObjectDB db = null)
        {
            db = db ?? ObjectDB.instance;
            if (db == null || db.m_StatusEffects == null) return;
            int added = 0;
            added += Inject(db, GuardianPowerFactory.ByName);
            added += Inject(db, StatusEffectFactory.ByName);
            added += Inject(db, SubEffectFactory.ByName);
            Traverse.Create(db).Method("UpdateRegisters").GetValue();
            Jotunn.Logger.LogInfo($"[BiomeLords] ObjectDB inject pass: {added} SEs added.");

            // Now that ObjectDB has items, assign relevant vanilla item icons
            // to every BiomeLords SE for clear HUD display + countdown.
            BiomeLords.Util.IconAssignment.AssignAll();
        }

        private static int Inject(ObjectDB db, System.Collections.Generic.Dictionary<string, StatusEffect> map)
        {
            if (map == null) return 0;
            int added = 0;
            foreach (var kv in map)
            {
                var se = kv.Value;
                if (se == null) continue;
                if (!db.m_StatusEffects.Contains(se))
                {
                    db.m_StatusEffects.Add(se);
                    added++;
                }
            }
            return added;
        }
    }

    /// <summary>
    /// One-time migration: if the player has an obsolete BiomeLords GP name set
    /// (renamed during development), upgrade them to the current equivalent.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
    public static class Player_OnSpawned_GPMigration
    {
        [HarmonyPostfix]
        public static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;
            var current = __instance.GetGuardianPowerName();
            if (string.IsNullOrEmpty(current)) return;

            // GP_VerdantWrath was the Greydwarf GP prior to the Forest's Embrace
            // rework. Re-equip the new SE if the saved name is the old one.
            if (current == "GP_VerdantWrath")
            {
                __instance.SetGuardianPower(GuardianPowerFactory.GreydwarfLordGP);
                __instance.Message(MessageHud.MessageType.Center,
                    "Your Greydwarf Shaman Lord's spirit has been renewed.");
                Jotunn.Logger.LogInfo("[BiomeLords] Migrated GP_VerdantWrath → " + GuardianPowerFactory.GreydwarfLordGP);
            }
        }
    }
}
