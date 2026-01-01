using GameClient.Core.Configs;
using GameClient.Files;
using GameClient.Managers;
using GameClient.Misc;
using HarmonyLib;
using Shared;
using Shared.Misc;
using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using static Shared.CommonEnumerators;

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

                PrepareCulture();
                PreparePaths();

                Directory.CreateDirectory(Master.AppdataRTPath);
                PersistentSettings.SetFilePath(Path.Combine(Master.AppdataRTPath, "PersistentSettings" + CommonValues.DefaultSaveFormat));

                OptionsProfileSessionManager.Bootstrap();

                HarmonyHandler.EnableStartPatches();

                CreateUnityDispatcher();
                MethodGatherer.CacheAllMethods(MethodGatherer.AssemblyType.Client);
            }
        }

        private static void PrepareCulture()
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US", false);
            CultureInfo.CurrentUICulture = new CultureInfo("en-US", false);
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US", false);
            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US", false);
        }

        private static void PreparePaths()
        {
            Master.SavesFolderPath = GenFilePaths.SavedGamesFolderPath;

            Master.AppdataPath = GenFilePaths.SaveDataFolderPath;
            Master.AppdataRTPath = Path.Combine(Master.AppdataPath, "RimWorld Together");
            Master.AppdataTempPath = Path.Combine(Master.AppdataRTPath, "Temp");
            Master.AppdataVersionPath = Path.Combine(Master.AppdataTempPath, "Version");

            string modRoot = TryGetModRootFromActiveList();
            if (string.IsNullOrEmpty(modRoot))
                modRoot = TryGetModRootFromAssemblyLocation();

            if (string.IsNullOrEmpty(modRoot))
            {
                Printer.Warning("[RWT] Failed to resolve mod root directory. Some features may not work.", LogImportanceMode.Verbose);
                modRoot = string.Empty;
            }

            Master.ModMainPath = modRoot;
            Master.ModScriptsPath = string.IsNullOrEmpty(modRoot) ? string.Empty : Path.Combine(Master.ModMainPath, "Scripts");
            Master.ModAssemblyPath = string.IsNullOrEmpty(modRoot) ? string.Empty : Path.Combine(Master.ModMainPath, "Current", "Assemblies");

            if (!Directory.Exists(Master.AppdataRTPath))
                Directory.CreateDirectory(Master.AppdataRTPath);

            if (Directory.Exists(Master.AppdataTempPath))
                Directory.Delete(Master.AppdataTempPath, true);

            Directory.CreateDirectory(Master.AppdataTempPath);
            Directory.CreateDirectory(Master.AppdataVersionPath);
        }

        private static string TryGetModRootFromActiveList()
        {
            try
            {
                string idA = Master.ModPackageID ?? string.Empty;
                string idB = string.IsNullOrEmpty(idA) ? string.Empty : idA + "_steam";

                var match = LoadedModManager.RunningMods.FirstOrDefault(m =>
                    !string.IsNullOrEmpty(m.PackageId) &&
                    (string.Equals(m.PackageId, idA, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(m.PackageId, idB, StringComparison.OrdinalIgnoreCase)));

                return match?.RootDir;
            }
            catch
            {
                return null;
            }
        }

        private static string TryGetModRootFromAssemblyLocation()
        {
            try
            {
                string asmPath = typeof(Main_).Assembly.Location;
                if (string.IsNullOrEmpty(asmPath))
                    return null;

                string dir = Path.GetDirectoryName(asmPath);
                if (string.IsNullOrEmpty(dir))
                    return null;

                DirectoryInfo di = new DirectoryInfo(dir);
                DirectoryInfo current = di.Parent;
                DirectoryInfo modRoot = current?.Parent;

                if (modRoot != null && modRoot.Exists)
                    return modRoot.FullName;

                return null;
            }
            catch
            {
                return null;
            }
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