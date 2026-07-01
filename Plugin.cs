using BepInEx;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;
using BiomeLords.Config;
using BiomeLords.Data;
using BiomeLords.Phase1B;
using BiomeLords.Phase1C;

namespace BiomeLords
{
    /// <summary>
    /// BiomeLords mod entry. Requires Jotunn. Server-authoritative config:
    /// when used on a dedicated server, every client must have the same
    /// mod version, and the server's config values are pushed to clients
    /// (clients cannot override locally).
    /// </summary>
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    // Soft dependency: if ComfyQuickSlots is installed it must load first so its
    // plugin info is registered before FeatherweightInventory checks for it (and
    // before our inventory patches run). Optional — BiomeLords works fine without it.
    [BepInDependency("com.bruce.valheim.comfyquickslots", BepInDependency.DependencyFlags.SoftDependency)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGUID    = "com.taeguk.BiomeLords";
        public const string ModName    = "BiomeLords";
        public const string ModVersion = "0.6.2";

        internal static Plugin Instance;
        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;

            LordConfig.Bind(Config);

            _harmony = new Harmony(ModGUID);
            // Per-class patching so a single bad target (e.g. a vanilla method
            // renamed in a Valheim update) skips just that one patch instead of
            // aborting PatchAll mid-way and leaving the mod half-loaded.
            int ok = 0, failed = 0;
            foreach (var t in typeof(Plugin).Assembly.GetTypes())
            {
                try
                {
                    new HarmonyLib.PatchClassProcessor(_harmony, t).Patch();
                    ok++;
                }
                catch (System.Exception ex)
                {
                    failed++;
                    Logger.LogError($"[BiomeLords] Patch class {t.FullName} failed: {ex.Message}");
                }
            }
            Logger.LogInfo($"[BiomeLords] Harmony: {ok} patch classes applied, {failed} skipped.");

            PrefabManager.OnVanillaPrefabsAvailable += OnVanillaPrefabsAvailable;

            Logger.LogInfo($"{ModName} {ModVersion} loaded. {LordRegistry.All.Count} Lords registered.");
        }

        private void OnVanillaPrefabsAvailable()
        {
            try
            {
                // Order matters: trophies and status effects must exist before
                // CreatureFactory builds drops and PedestalFactory wires lookups.
                StatusEffectFactory.RegisterAll();
                GuardianPowerFactory.RegisterAll();
                SubEffectFactory.RegisterAll();
                TrophyFactory.RegisterAll();
                CreatureFactory.RegisterAll();
                ItemFactory.RegisterAll();
                PedestalFactory.RegisterAll();
                BiomeLords.Phase1D.DebugCommands.RegisterAll();

                // ObjectDB.Awake fires BEFORE OnVanillaPrefabsAvailable, so the
                // postfix injection runs with empty registries. Push them in now.
                BiomeLords.Patches.ObjectDB_Awake_BiomeLordsInject.InjectAll();
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"[BiomeLords] Registration failed: {ex}");
            }
            finally
            {
                PrefabManager.OnVanillaPrefabsAvailable -= OnVanillaPrefabsAvailable;
            }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
