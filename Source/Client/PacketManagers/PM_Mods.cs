using GameClient.Dialogs;
using GameClient.Managers;
using GameClient.Misc;
using Shared;
using Shared.Files;
using Shared.Files.Configs.Mods;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using Verse;
using static Shared.CommonEnumerators;
using static Shared.Files.Configs.Mods.ModsConfigFile;

namespace GameClient.PacketManagers
{
    public class PM_Mods : PM_Base
    {
        [HandlesPacket(PacketHeader.ModManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_ModConfig data = null;

            try
            {
                data = Serializer.ConvertBytesToObject<PKT_ModConfig>(bytes);
            }
            catch (Exception e)
            {
                Printer.Warning($"[Mods] Failed to deserialize mod packet: {e}");
                return;
            }

            if (data == null)
                return;

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
                        DLG_Base.PushNewDialog(new DLG_Message("Mod Manager", new[]
                        {
                            "Admin only.",
                            "Only admins can edit the server mod manager."
                        }));
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

                if (isFirstEdit)
                    GameParameterManager.SetFirstTimeSetup();
            };

            List<string> modNames = new List<string>();
            foreach (ModConfig config in ModManagerH.GetRunningModList().ModConfigs)
                modNames.Add(config.FileName);

            string[] keys = modNames.ToArray();
            string[] values = new string[] { "Required", "Optional", "Forbidden" };

            DLG_ListingWithTuple dialog = new DLG_ListingWithTuple(
                "Mod Manager",
                SessionHandler.CurrentModConfig != null && SessionHandler.CurrentModConfig.IsEnforced
                    ? "Manage mods for the server. This server currently has enforced mod rules."
                    : "Manage mods for the server.",
                keys,
                values,
                null,
                toDo);

            DLG_Base.PushNewDialog(dialog);
        }

        public static void ReceiveModConfigs(PKT_ServerGlobalData data)
        {
            SessionHandler.CurrentModConfig = data._modConfigs ?? new ModsConfigFile();

            OptionsProfileSessionManager.OnServerEnforcementReceived();

            if (!SessionHandler.CurrentModConfig.IsEnforced)
                return;

            Printer.Warning("Receiving enforced mod configs from server", LogImportanceMode.Verbose);
        }
    }

    public class ModManagerH
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

        public static void ShowConflictingModsDialog(PKT_Login data)
        {
            List<string> lines = new List<string>();

            try
            {
                if (data != null && data._extraDetails != null)
                {
                    foreach (string str in data._extraDetails)
                    {
                        if (!string.IsNullOrWhiteSpace(str))
                            lines.Add(str);
                    }
                }
            }
            catch
            {
            }

            if (lines.Count == 0)
            {
                lines.Add("The server reported a mod conflict, but no details were provided.");
                lines.Add("Try reopening the game and checking your mod list order and server-required mods.");
            }

            try
            {
                if (DLG_Wait.Instance != null)
                    DLG_Wait.Instance.Close();
            }
            catch
            {
            }

            DLG_Base.PushNewDialog(new DLG_Listing(
                "Mod Conflicts",
                "Your current mod list does not match the server. Fix the items below, then reconnect.",
                lines.ToArray()));
        }

        public static ModsConfigFile SortModsIntoCategories(string[] modNames, int[] categoryIndexes)
        {
            ModsConfigFile configFile = new ModsConfigFile();

            if (modNames == null || categoryIndexes == null)
                return configFile;

            int count = Math.Min(modNames.Length, categoryIndexes.Length);

            for (int i = 0; i < count; i++)
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