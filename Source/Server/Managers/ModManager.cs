using Shared;
using static Shared.CommonEnumerators;

namespace GameServer
{
    public static class ModManager
    {
        public static void LoadMods()
        {
            Master.loadedRequiredMods.Clear();
            LoadModsFromDirectory(Master.requiredModsPath, Master.loadedRequiredMods);

            Master.loadedOptionalMods.Clear();
            LoadModsFromDirectory(Master.optionalModsPath, Master.loadedOptionalMods);

            Master.loadedForbiddenMods.Clear();
            LoadModsFromDirectory(Master.forbiddenModsPath, Master.loadedForbiddenMods);
        }

        private static void LoadModsFromDirectory(string directoryPath, List<string> modList)
        {
            string[] modsToLoad = Directory.GetDirectories(directoryPath);
            foreach (string modPath in modsToLoad)
            {
                try
                {
                    string aboutFile = Directory.GetFiles(modPath, "About.xml", new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = true })[0];
                    foreach (string str in XmlParser.ChildContentFromParent(aboutFile, "packageId", "ModMetaData"))
                    {
                        if (!modList.Contains(str))
                        {
                            Logger.Warning($"Loaded > '{modPath}'");
                            modList.Add(str);
                        }
                    }
                }
                catch
                {
                    Logger.Error($"Failed to load About.xml of mod at '{modPath}'");
                }
            }
        }

        public static bool CheckIfModConflict(ServerClient client, LoginData loginData)
        {
            List<string> conflictingMods = new List<string>();
            List<string> conflictingNames = new List<string>();

            CheckRequiredMods(client, loginData, conflictingMods, conflictingNames);
            CheckForbiddenMods(client, loginData, conflictingMods, conflictingNames);

            if (conflictingMods.Count == 0)
            {
                client.userFile.UpdateMods(loginData.RunningMods);
                return false;
            }
            else
            {
                if (client.userFile.IsAdmin)
                {
                    Logger.Warning($"[Mod bypass] > {client.userFile.Username}");
                    client.userFile.UpdateMods(loginData.RunningMods);
                    return false;
                }

                Logger.Warning($"[Mod Mismatch] > {client.userFile.Username}");
                UserManager.SendLoginResponse(client, LoginResponse.WrongMods, conflictingMods);
                return true;
            }
        }

        private static void CheckRequiredMods(ServerClient client, LoginData loginData, List<string> conflictingMods, List<string> conflictingNames)
        {
            foreach (string mod in Master.loadedRequiredMods)
            {
                if (!loginData.RunningMods.Contains(mod))
                {
                    conflictingMods.Add($"[Required] > {mod}");
                    conflictingNames.Add(mod);
                }
            }

            foreach (string mod in loginData.RunningMods)
            {
                if (!Master.loadedRequiredMods.Contains(mod) && !Master.loadedOptionalMods.Contains(mod))
                {
                    conflictingMods.Add($"[Disallowed] > {mod}");
                    conflictingNames.Add(mod);
                }
            }
        }

        private static void CheckForbiddenMods(ServerClient client, LoginData loginData, List<string> conflictingMods, List<string> conflictingNames)
        {
            foreach (string mod in Master.loadedForbiddenMods)
            {
                if (loginData.RunningMods.Contains(mod))
                {
                    conflictingMods.Add($"[Forbidden] > {mod}");
                    conflictingNames.Add(mod);
                }
            }
        }
    }
}