using GameServer.Core;
using GameServer.Managers;
using GameServer.Misc;
using Shared;
using Shared.Files.Configs.Mods;
using Shared.Misc;
using System.Collections.Generic;
using System.Linq;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static Shared.CommonEnumerators;

namespace GameServer.PacketManager
{
    public class PM_Mods : PM_Base
    {
        static PM_Mods()
        {
            OptionsProfileManager.Initialize();
        }

        [HandlesPacket(PacketHeader.ModManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_ModConfig data = Serializer.ConvertBytesToObject<PKT_ModConfig>(bytes);
            if (client == null || data == null) return;

            if (data._requestOptionsProfile)
            {
                OptionsProfileManager.HandleClientRequest(client);
                return;
            }

            if (data._uploadOptionsProfile)
            {
                if (client.UserFile == null || !client.UserFile.IsAdmin)
                {
                    string username = client.UserFile?.Username ?? "(unknown)";
                    Printer.Warning($"[OptionsProfile] Non-admin {username} attempted to upload an options profile chunk.");
                    ResponseShortcutManager.SendIllegalPacket(client, "Only admins can upload server options profiles.");
                    return;
                }

                OptionsProfileManager.HandleAdminUploadChunk(client, data);
                return;
            }

            switch (data._stepMode)
            {
                case ModConfigStepMode.Send:
                    SaveModConfig(client, data._configFile);
                    break;
            }
        }

        private static void SaveModConfig(ServerClient client, ModsConfigFile file)
        {
            if (client == null) return;

            if (Master.WorldValues != null && (client.UserFile == null || !client.UserFile.IsAdmin))
            {
                string username = client.UserFile?.Username ?? "(unknown)";
                Printer.Warning($"User {username} attempted to change mod config without admin permissions.");
                ResponseShortcutManager.SendIllegalPacket(client, "Only admins can change the server mod config.");
                return;
            }

            if (file == null)
            {
                ResponseShortcutManager.SendIllegalPacket(client, "Received an invalid mod config file.");
                return;
            }

            if (file.ModConfigs == null)
                file.ModConfigs = new List<ModConfig>();

            Master.ModConfig = file;
            ModsConfigFile.Save(ModsConfigFile.SavePath, file);
            InformationDisplayer.DisplaySetMods(client);
        }

        public static bool CheckIfModConflict(ServerClient client, PKT_Login loginData)
        {
            List<string> conflictingModNames = new List<string>();

            if (Master.ModConfig == null || Master.ModConfig.ModConfigs == null)
            {
                OptionsProfileManager.TryPushProfile(client);
                return false;
            }

            if (loginData == null || loginData._runningMods == null || loginData._runningMods.ModConfigs == null)
            {
                conflictingModNames.Add("[Error] > Client mod list was missing");
            }
            else
            {
                List<ModConfig> serverMods = Master.ModConfig.ModConfigs;
                List<ModConfig> clientMods = loginData._runningMods.ModConfigs;

                foreach (ModConfig config in serverMods.Where(x => x.Type == ModsConfigFile.ModType.Required))
                {
                    ModConfig match = clientMods.Find(x => x.FileName == config.FileName);
                    if (match == null)
                        conflictingModNames.Add($"[Required Missing] > {config.FileName}");
                }

                foreach (ModConfig config in clientMods)
                {
                    ModConfig allowed = serverMods.Find(x =>
                        x.FileName == config.FileName &&
                        (x.Type == ModsConfigFile.ModType.Required || x.Type == ModsConfigFile.ModType.Optional));

                    if (allowed == null)
                        conflictingModNames.Add($"[Disallowed Present] > {config.FileName}");
                }
            }

            conflictingModNames = conflictingModNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            if (conflictingModNames.Count == 0)
            {
                OptionsProfileManager.TryPushProfile(client);
                return false;
            }

            if (client != null && client.UserFile != null && client.UserFile.IsAdmin)
            {
                InformationDisplayer.DisplayModBypass(client.UserFile.Username);
                return false;
            }

            string username = client?.UserFile?.Username ?? "(unknown)";
            InformationDisplayer.DisplayModMismatch(username);
            LoginManagerH.DenyConnectionWithReason(client, LoginResponse.Mods, conflictingModNames);
            return true;
        }
    }
}