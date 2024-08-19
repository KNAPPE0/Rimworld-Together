using Shared;
using static Shared.CommonEnumerators;

namespace GameServer
{
    public static class UserRegister
    {
        public static void TryRegisterUser(ServerClient client, Packet packet)
        {
            // Deserialize the login data from the packet
            var loginData = Serializer.ConvertBytesToObject<LoginData>(packet.Contents);
            if (loginData == null)
            {
                Logger.Warning("[Register] > Failed to deserialize login data.");
                UserManager.SendLoginResponse(client, LoginResponse.RegisterError);
                return;
            }

            // Perform necessary checks for registration
            if (!UserManager.CheckIfUserUpdated(client, loginData) ||
                !UserManager.CheckLoginData(client, loginData, LoginMode.Register) ||
                UserManager.CheckIfUserExists(client, loginData, LoginMode.Register))
            {
                return;
            }

            try
            {
                // Set login details and save the user file
                client.UserFile.SetLoginDetails(loginData);
                client.UserFile.SaveUserFile();

                // Attempt to login the user after successful registration
                UserLogin.TryLoginUser(client, packet);

                Logger.Message($"[Registered] > {client.UserFile.Username}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[Register] > Exception occurred: {ex.Message}");
                UserManager.SendLoginResponse(client, LoginResponse.RegisterError);
            }
        }
    }
}