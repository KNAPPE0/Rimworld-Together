using GameClient.Dialogs;
using Shared;
using Shared.Files;
using Shared.Files.Configs.Mods;
using Shared.Misc;
using System.Collections.Generic;
using System.Linq;
using TCPNetwork.Packets;
using Verse;
using static Shared.CommonEnumerators;
using static Shared.Files.Configs.Mods.ModsConfigFile;

namespace GameClient.PacketManagers
{
    public static class PM_Mods
    {
        [HandlesPacket(PacketHeader.ModManager)]
        private static void ParsePacket(byte[] bytes)
        {
            ModConfigData data = Serializer.ConvertBytesToObject<ModConfigData>(bytes);

            if (data._isOptionsProfileChunk || data._noOptionsProfileAvailable)
            {
                OptionsProfileSessionManager.ReceiveOptionsProfilePacket(data);
                return;
            }

            switch (data._stepMode)
            {
                case ModConfigStepMode.Ask:
                    if (!SessionHandler.IsAdmin)
                    {
                        DLG_Base.PushNewDialog(new DLG_Message("Mod Manager", new[] { "Admin only." }));
                        return;
                    }

                    OpenModManagerMenu();
                    break;
            }
        }

        public static void OpenModManagerMenu(bool isFirstEdit = false)
        {
            Action toDo = delegate
            {
                GameParameterManager.SendCurrentModConfigs(false);
                if (isFirstEdit) GameParameterManager.SetFirstTimeSetup();
            };

            List<string> modNames = new List<string>();
            foreach (ModConfig config in ModManagerH.GetRunningModList().ModConfigs)
                modNames.Add(config.FileName);

            string[] keys = modNames.ToArray();
            string[] values = new string[] { "Required", "Optional", "Forbidden" };

            DLG_ListingWithTuple dialog = new DLG_ListingWithTuple(
                "Mod Manager",
                "Manage mods for the server",
                keys,
                values,
                null,
                toDo);

            DLG_Base.PushNewDialog(dialog);
        }

        public static void ReceiveModConfigs(ServerGlobalData data)
        {
            SessionHandler.CurrentModConfig = data._modConfigs ?? new ModsConfigFile();

            OptionsProfileSessionManager.OnServerEnforcementReceived();

            if (!SessionHandler.CurrentModConfig.IsEnforced) return;

            Printer.Warning("Receiving enforced mod configs from server", LogImportanceMode.Verbose);
        }
    }

    public static class ModManagerH
    {
        public static ModsConfigFile GetRunningModList()
        {
            ModsConfigFile configFile = new ModsConfigFile();

            ModContentPack[] runningMods = LoadedModManager.RunningMods.ToArray();
            foreach (ModContentPack mod in runningMods)
            {
                ModConfig newConfig = new ModConfig();
                newConfig.FileName = mod.Name.Replace("steam_", "");
                configFile.ModConfigs.Add(newConfig);
            }

            return configFile;
        }

        public static void GetConflictingMods(LoginData data)
        {
            DLG_Base.PushNewDialog(new DLG_Listing(
                "Mod Conflicts",
                "The following mods are conflicting with the server",
                data._extraDetails.ToArray()));
        }

        public static ModsConfigFile SortModsIntoCategories(string[] modNames, int[] categoryIndexes)
        {
            ModsConfigFile configFile = new ModsConfigFile();

            for (int i = 0; i < modNames.Length; i++)
            {
                ModConfig newConfig = new ModConfig();
                newConfig.FileName = modNames[i].Replace("steam_", "");
                newConfig.Type = (ModType)categoryIndexes[i];

                configFile.ModConfigs.Add(newConfig);
            }

            return configFile;
        }
    }
}