using Shared;
using static Shared.CommonEnumerators;

namespace GameServer
{
    public static class UserLogin
    {
        public static void TryLoginUser(ServerClient client, Packet packet)
        {
            // Deserialize the login data
            var loginData = Serializer.ConvertBytesToObject<LoginData>(packet.Contents);
            if (loginData == null)
            {
                Logger.Warning("[Login] > Failed to deserialize login data.");
                return;
            }

            // Perform various checks for login
            if (!UserManager.CheckIfUserUpdated(client, loginData) ||
                !UserManager.CheckLoginData(client, loginData, LoginMode.Login) ||
                !UserManager.CheckIfUserExists(client, loginData, LoginMode.Login) ||
                !UserManager.CheckIfUserAuthCorrect(client, loginData) ||
                UserManager.CheckIfUserBanned(client) ||
                !UserManager.CheckWhitelist(client) ||
                ModManager.CheckIfModConflict(client, loginData))
            {
                return;
            }

            // Set login Details and load user file
            client.UserFile.SetLoginDetails(loginData);
            client.LoadFromUserFile();

            Logger.Message($"[Handshake] > {client.UserFile.SavedIP} | {client.UserFile.Username}");

            // Remove old client if exists
            RemoveOldClientIfAny(client);

            // Handle post-login actions
            PostLogin(client);
        }

        private static void PostLogin(ServerClient client)
        {
            // Update player recount
            UserManager.SendPlayerRecount();

            // Send global server data
            ServerGlobalDataManager.SendServerGlobalData(client);

            // Send default join Messages
            foreach (var Message in ChatManager.DefaultJoinMessages)
            {
                ChatManager.SendSystemMessage(client, Message);
            }

            // Check if world exists and send relevant data
            if (WorldManager.CheckIfWorldExists())
            {
                if (SaveManager.CheckIfUserHasSave(client))
                {
                    SaveManager.SendSavePartToClient(client);
                }
                else
                {
                    WorldManager.SendWorldFile(client);
                }
            }
            else
            {
                WorldManager.RequestWorldFile(client);
            }
        }

        private static void RemoveOldClientIfAny(ServerClient client)
        {
            foreach (var cClient in Network.connectedClients.ToArray())
            {
                if (cClient != client && cClient.UserFile.Username == client.UserFile.Username)
                {
                    UserManager.SendLoginResponse(cClient, LoginResponse.ExtraLogin);
                    cClient.Listener?.DestroyConnection(); // Ensuring the old client is disconnected safely
                }
            }
        }
    }
}