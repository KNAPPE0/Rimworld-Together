using GameServer.Core;
using GameServer.Managers;
using GameServer.Misc;
using Shared;
using Shared.Files.Configs.Mods;
using Shared.Misc;
using System.Collections.Generic;
using System.Linq;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static Shared.CommonEnumerators;

namespace GameServer.PacketManager
{
    public static class PM_Mods
    {
        static PM_Mods()
        {
            OptionsProfileManager.Initialize();
        }

        [HandlesPacket(PacketHeader.ModManager)]
        private static void ParsePacket(ServerClient client, byte[] bytes, PacketHeader header)
        {
            ModConfigData data = Serializer.ConvertBytesToObject<ModConfigData>(bytes);

            if (data._requestOptionsProfile)
            {
                OptionsProfileManager.HandleClientRequest(client);
                return;
            }

            if (data._uploadOptionsProfile)
            {
                if (!client.UserFile.IsAdmin)
                {
                    UserManager.BanPlayerFromName(client.UserFile.Username);
                    Printer.Warning($"[OptionsProfile] Player {client.UserFile.Username} tried to upload profile without admin.");
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
            if (Master.WorldValues != null && !client.UserFile.IsAdmin)
            {
                UserManager.BanPlayerFromName(client.UserFile.Username);
                Printer.Warning($"Player {client.UserFile.Username} tried to change mod config without being admin");
            }
            else
            {
                Master.ModConfig = file;
                ModsConfigFile.Save(ModsConfigFile.SavePath, file);
                InformationDisplayer.DisplaySetMods(client);
            }
        }

        public static bool CheckIfModConflict(ServerClient client, LoginData loginData)
        {
            List<string> conflictingModNames = new List<string>();

            foreach (ModConfig config in Master.ModConfig.ModConfigs.Where(fetch => fetch.Type == ModsConfigFile.ModType.Required))
            {
                ModConfig toFind = loginData._runningMods.ModConfigs.Find(fetch => fetch.FileName == config.FileName);
                if (toFind == null)
                {
                    conflictingModNames.Add($"[Required] > {config.FileName}");
                    continue;
                }
            }

            foreach (ModConfig config in loginData._runningMods.ModConfigs)
            {
                ModConfig toFind = Master.ModConfig.ModConfigs.Find(fetch => fetch.FileName == config.FileName
                    && (fetch.Type == ModsConfigFile.ModType.Required || fetch.Type == ModsConfigFile.ModType.Optional));

                if (toFind == null)
                {
                    conflictingModNames.Add($"[Disallowed] > {config.FileName}");
                    continue;
                }
            }

            if (conflictingModNames.Count == 0)
            {
                OptionsProfileManager.TryPushProfile(client);
                return false;
            }
            else
            {
                if (client.UserFile.IsAdmin)
                {
                    InformationDisplayer.DisplayModBypass(client.UserFile.Username);
                    return false;
                }
                else
                {
                    InformationDisplayer.DisplayModMismatch(client.UserFile.Username);
                    PM_Logins.LoginManagerH.DenyConnectionWithReason(client, LoginResponse.Mods, conflictingModNames);
                    return true;
                }
            }
        }
    }
}