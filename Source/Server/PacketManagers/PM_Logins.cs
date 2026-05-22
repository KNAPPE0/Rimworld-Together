using GameServer.Core;
using GameServer.Hooks.TCPNetwork;
using GameServer.Managers;
using GameServer.Misc;
using Shared;
using System.Text.RegularExpressions;
using Shared.Misc;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;
using static Shared.CommonEnumerators;
using static TCPNetwork.Packets.PKT_Login;

namespace GameServer.PacketManager
{
    public class PM_Logins : PM_Base
    {
        // KMH: Compiled once instead of constructed per login attempt.
        private static readonly Regex UsernameSanitizer =
            new Regex(@"[^a-zA-Z0-9_\-]", RegexOptions.Compiled);

        [HandlesPacket(PacketHeader.LoginManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_Login data = Serializer.ConvertBytesToObject<PKT_Login>(bytes);

            HandleUser(client, data);
        }

        public static void HandleUser(ServerClient client, PKT_Login data)
        {
            client.UserFile = new UserFile();

            // KMH: Sanitize username to prevent path traversal and injection
            string sanitized = data._username ?? string.Empty;
            sanitized = sanitized.Trim();
            sanitized = UsernameSanitizer.Replace(sanitized, "");
            if (sanitized.Length > 32) sanitized = sanitized.Substring(0, 32);
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                DenyConnectionWithReason(client, LoginResponse.Invalid);
                return;
            }
            data._username = sanitized;

            client.UserFile.Username = data._username;
            client.UserFile.Password = data._password;

            if (UserManagerH.CheckIfUserExists(client, data)) LoginUser(client, data);
            else RegisterUser(client, data);
        }

        public static bool LoginUser(ServerClient client, PKT_Login data)
        {
            if (!UserManagerH.CheckIfUserAuthCorrect(client, data)) return false;

            client.LoadUserFromFile(client);

            if (UserManagerH.CheckIfUserBanned(client)) return false;

            if (!UserManagerH.CheckWhitelist(client)) return false;

            if (PM_World.CheckIfWorldExists() && PM_Mods.CheckIfModConflict(client, data)) return false;

            RemoveOldClientSessions(client);

            // KMH 2.7: Register in the O(1) username→client index now that
            // we've finished authentication + loaded the user file.
            GameServer.Hooks.TCPNetwork.ServerNetwork.RegisterAuthenticatedClient(client);

            InformationDisplayer.DisplayLogin(client);
            // KMH: Announce join to Discord
            GameServer.Integrations.Discord.DiscordPlayerAnnouncer.AnnounceFullyJoined(client.UserFile?.Username);

            PostLogin(client, data);

            return true;
        }

        public static void RegisterUser(ServerClient client, PKT_Login data)
        {
            client.UserFile.UpdateHash();

            PM_Sites.SetSiteInfoForClient(client);

            InformationDisplayer.DisplayRegister(client);

            LoginUser(client, data);

            // KMH: New user file just created — drop the cache so subsequent
            // login lookups see it.
            UserManagerH.InvalidateUserCache();
        }

        private static void PostLogin(ServerClient client, PKT_Login data)
        {
            client.VerifyUser();
            UserManager.SendPlayerRecount();

            // HARD GATE:
            // If enforcement is active and this client does not already have the correct profile,
            // do not continue into save/world sync yet.
            if (OptionsProfileManager.IsClientMissingRequiredProfile(data))
            {
                OptionsProfileManager.SendRequiredProfileForJoin(client);
                return;
            }

            FinishPostLogin(client);
        }

        private static void FinishPostLogin(ServerClient client)
        {
            GlobalDataManager.SendServerGlobalData(client);
            PM_Chat.SendLoginChatMessages(client);

            // KMH: Push the linked-accounts map so dialogs render Discord names
            // for every player from the first frame.
            try { LinkedAccountsManager.SendSnapshot(client); }
            catch { }

            // KMH: surface the guild MOTD as a chat message right after login.
            try
            {
                string motd = GuildManager.GetMotdForUser(client.UserFile?.Username);
                if (!string.IsNullOrWhiteSpace(motd))
                    PM_Chat.SendServerMessage(client, $"[Guild] {motd}");
            }
            catch { }

            if (PM_World.CheckIfWorldExists())
            {
                if (PM_Saves.CheckIfUserHasSave(client)) PM_Saves.SendSaveToClient(client);
                else PM_World.SendWorld(client);
            }
            else
            {
                PM_World.RequireWorldFile(client);

                client.UserFile.UpdateAdmin(true);
                PKT_Command commandData = new PKT_Command();
                commandData._commandMode = CommandMode.Op;
                client.Listener.EnqueuePacket(PacketHeader.ConsoleManager, commandData);
                Printer.Warning($"Giving first join admin permission to {client.UserFile.Username}");
            }
        }
        public static void RemoveOldClientSessions(ServerClient client)
        {
            // KMH: Filter null UserFile/Username to avoid NRE when a sibling
            // client is mid-handshake during login race.
            string username = client?.UserFile?.Username;
            if (string.IsNullOrEmpty(username)) return;

            ServerClient[] oldClients = ServerNetwork.GetConnectedClients()
                .Where(fetch => fetch != client
                    && fetch.UserFile != null
                    && fetch.UserFile.Username == username)
                .ToArray();

            foreach (ServerClient sc in oldClients) sc.Listener.MarkForDisconnect();
        }

        public static void DenyConnectionWithReason(ServerClient client, LoginResponse response, object extraDetails = null)
        {
            PKT_Login loginData = new PKT_Login();
            loginData._tryResponse = response;

            if (response == LoginResponse.Mods) loginData._extraDetails = (List<string>)extraDetails;
            else if (response == LoginResponse.Version) loginData._extraDetails = new List<string>() { CommonValues.ExecutableVersion };

            client.Listener.EnqueuePacket(PacketHeader.LoginManager, loginData);
            client.Listener.MarkForDisconnect();
        }
    }
}
