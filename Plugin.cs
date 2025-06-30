using BepInEx;
using Jotunn;
using Jotunn.Managers;
using Jotunn.Configs;
using UnityEngine;

namespace BiomeLords
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGUID = "com.taeguk.BiomeLords";
        public const string ModName = "BiomeLords";
        public const string ModVersion = "4.0.0";

        private void Awake()
        {
            Logger.LogInfo("Biome Lords Mod loaded");

            // Add your hooks, bosses, items, crafting etc here
            PrefabManager.OnVanillaPrefabsAvailable += RegisterCustomContent;
        }

        private void RegisterCustomContent()
        {
            Logger.LogInfo("Registering Biome Lord content...");
        }
    }
}