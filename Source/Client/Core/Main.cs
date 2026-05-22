using GameClient.Files;
using GameClient.Hooks.Shared;
using GameClient.Misc;
using Shared;
using Shared.Misc;
using System.IO;
using System.Linq;
using TCPNetwork;
using TCPNetwork.PacketManagers;
using UnityEngine;
using Verse;

namespace GameClient.Core
{
    public static class Main_
    {
        [StaticConstructorOnStartup]
        public static class RimworldTogether
        {
            static RimworldTogether()
            {
                ClientPrinter.CreateLogger();
                CultureHandler.SetCulture();
                PreparePaths();

                MethodGatherer.CacheAllMethods();
                PM_Base.CacheAllPackets(PM_Base.AssemblyType.Client);

                CreateUnityDispatcher();
                HarmonyHandler.EnableStartPatches();

                // KMH: Initialize options profile enforcement system
                GameClient.Managers.OptionsProfileSessionManager.Bootstrap();
                PersistentSettings.SetFilePath(Path.Combine(Master.AppdataRTPath, "PersistentSettings" + CommonValues.DefaultSaveFormat));
            }
        }

        private static void PreparePaths()
        {
            Master.SavesFolderPath = GenFilePaths.SavedGamesFolderPath;

            Master.AppdataPath = GenFilePaths.SaveDataFolderPath;
            Master.AppdataRTPath = Path.Combine(Master.AppdataPath, "RimWorld Together");
            Master.AppdataTempPath = Path.Combine(Master.AppdataRTPath, "Temp");
            Master.AppdataVersionPath = Path.Combine(Master.AppdataTempPath, "Version");
            // LocalServer install dir (see LocalServerHandler).
            Master.AppdataLocalServerPath = Path.Combine(Master.AppdataRTPath, "LocalServer");

            var modContent = LoadedModManager.RunningMods.FirstOrDefault(m => (m.PackageId == Master.ModPackageID || m.PackageId == Master.ModPackageID + "_steam") 
                && ModLister.GetActiveModWithIdentifier(m.PackageId) != null);

            if (modContent == null)
            {
                // Fallback: try matching just the package ID without the ActiveMod check
                modContent = LoadedModManager.RunningMods.FirstOrDefault(m => 
                    m.PackageId == Master.ModPackageID || m.PackageId == Master.ModPackageID + "_steam");
            }

            string mod = modContent?.RootDir ?? string.Empty;

            Master.ModMainPath = mod;
            Master.ModScriptsPath = Path.Combine(Master.ModMainPath, "Scripts");
            Master.ModAssemblyPath = Path.Combine(Master.ModMainPath, "Current", "Assemblies");

            if (!Directory.Exists(Master.AppdataRTPath)) Directory.CreateDirectory(Master.AppdataRTPath);

            if (Directory.Exists(Master.AppdataTempPath)) Directory.Delete(Master.AppdataTempPath, true);
            Directory.CreateDirectory(Master.AppdataTempPath);
            Directory.CreateDirectory(Master.AppdataVersionPath);
        }

        private static void CreateUnityDispatcher()
        {
            if (MainThreadHandler.Instance == null)
            {
                GameObject go = UnityEngine.Object.Instantiate(new GameObject());
                go.AddComponent(typeof(MainThreadHandler));
            }
        }
    }
}