using GameServer.Core;
using GameServer.Hooks.TCPNetwork;
using GameServer.Integrations.Discord;
using GameServer.Managers;
using GameServer.Misc;
using Shared;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static Shared.CommonEnumerators;

namespace GameServer.PacketManager
{
    public class PM_Logins : PM_Base
    {
        [HandlesPacket(PacketHeader.LoginManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            if (client == null || bytes == null || bytes.Length == 0)
                return;

            PKT_Login data = null;

            try
            {
                data = Serializer.ConvertBytesToObject<PKT_Login>(bytes);
            }
            catch (Exception e)
            {
                Printer.Warning($"[Login] Failed to deserialize login packet: {e}");
                return;
            }

            if (data == null)
                return;

            HandleUser(client, data);
        }

        public static void HandleUser(ServerClient client, PKT_Login data)
        {
            if (client == null || data == null)
                return;

            client.UserFile = new UserFile();
            client.UserFile.UpdateLoginDetails(data);

            if (UserManagerH.CheckIfUserExists(client, data))
            {
                string result = TryLoginUser(client, data);
                if (!string.IsNullOrEmpty(result))
                    Printer.Error($"Error during login: {result}");
            }
            else
            {
                RegisterUser(client, data);
            }
        }

        public static string TryLoginUser(ServerClient client, PKT_Login data)
        {
            if (!UserManagerH.CheckIfUserAuthCorrect(client, data))
                return $"Login details do not match for {data._username}";

            client.LoadUserFromFile(client);

            if (UserManagerH.CheckIfUserBanned(client))
                return $"{data._username} is banned";

            if (!UserManagerH.CheckWhitelist(client))
                return $"{data._username} is not whitelisted";

            if (PM_World.CheckIfWorldExists() && PM_Mods.CheckIfModConflict(client, data))
                return $"{data._username} has a mod conflict";

            LoginManagerH.RemoveOldClientSessions(client);

            InformationDisplayer.DisplayLogin(client);
            PostLogin(client);
            return string.Empty;
        }

        public static void RegisterUser(ServerClient client, PKT_Login data)
        {
            client.UserFile.UpdateHash();

            InformationDisplayer.DisplayRegister(client);

            string error = TryLoginUser(client, data);
            if (!string.IsNullOrEmpty(error))
                Printer.Error($"Error during login: {error}");
        }

        private static void PostLogin(ServerClient client)
        {
            PM_Sites.SetSiteInfoForClient(client);

            UserManager.SendPlayerRecount();
            GlobalDataManager.SendServerGlobalData(client);

            foreach (string str in PM_Chat.DefaultJoinMessages)
                PM_Chat.SendConsoleMessage(client, str);

            if (Master.ChatConfig.EnableMoTD)
                PM_Chat.SendServerMessage(client, $"MoTD > {Master.ChatConfig.MessageOfTheDay}");

            if (Master.ChatConfig.LoginNotifications)
                PM_Chat.BroadcastServerNotification($"{client.UserFile.Username} has joined the server!");

            DiscordPlayerAnnouncer.AnnounceFullyJoined(client.UserFile.Username);

            if (PM_World.CheckIfWorldExists())
            {
                if (PM_Saves.CheckIfUserHasSave(client))
                    PM_Saves.SendSaveToClient(client);
                else
                    PM_World.SendWorld(client);
            }
            else
            {
                Printer.Warning($"Giving first join admin permission to {client.UserFile.Username}");

                client.UserFile.UpdateAdmin(true);

                PKT_Command commandData = new PKT_Command();
                commandData._commandMode = CommandMode.Op;
                client.Listener.EnqueuePacket(PacketHeader.ConsoleManager, commandData);

                PM_World.RequireWorldFile(client);
            }
        }
    }

    public static class LoginManagerH
    {
        public static void RemoveOldClientSessions(ServerClient client)
        {
            foreach (ServerClient toFind in ServerNetwork.GetConnectedClients())
            {
                if (toFind == client)
                    continue;

                if (toFind.UserFile != null &&
                    client.UserFile != null &&
                    string.Equals(toFind.UserFile.Username, client.UserFile.Username, StringComparison.OrdinalIgnoreCase))
                {
                    DenyConnectionWithReason(toFind, LoginResponse.Duplicate);
                }
            }
        }

        public static void DenyConnectionWithReason(ServerClient client, LoginResponse response, object extraDetails = null)
        {
            if (client == null || client.Listener == null)
                return;

            PKT_Login loginData = new PKT_Login
            {
                _tryResponse = response
            };

            if (response == LoginResponse.Mods)
            {
                if (extraDetails is List<string> list)
                    loginData._extraDetails = new List<string>(list);
                else
                    loginData._extraDetails = new List<string>();
            }
            else if (response == LoginResponse.Version)
            {
                loginData._extraDetails = new List<string> { CommonValues.ExecutableVersion };
            }

            try
            {
                client.Listener.EnqueuePacket(PacketHeader.LoginManager, loginData);
            }
            catch (Exception e)
            {
                Printer.Warning($"[Login] Failed to enqueue denial packet: {e}");
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(250);
                }
                catch
                {
                }

                try
                {
                    client.Listener.Disconnect();
                }
                catch
                {
                }
            });
        }
    }
}