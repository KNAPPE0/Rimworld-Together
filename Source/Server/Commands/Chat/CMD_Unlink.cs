using GameServer.PacketManager;
using Shared;
using TCPNetwork.Files.Client;

namespace GameServer.Commands.Chat
{
    public class CMD_Unlink : CMD_Base
    {
        public CMD_Unlink()
        {
            Prefix = "/unlink";
            Description = "Remove your Discord account link";
            IsChatCommand = true;
        }

        public override void Action()
        {
            if (PM_Chat.TargetClient == null) return;

            UserFile user = PM_Chat.TargetClient.UserFile;
            if (user == null) return;

            if (string.IsNullOrEmpty(user.DiscordId))
            {
                PM_Chat.SendConsoleMessage(PM_Chat.TargetClient, "No Discord account is linked.");
                return;
            }

            string oldName = user.DiscordUsername ?? user.DiscordId;
            user.DiscordId = null;
            user.DiscordUsername = null;
            user.DiscordLinkToken = null;
            user.DiscordLinkTokenExpiry = 0;
            user.SaveUserFile();

            PM_Chat.SendConsoleMessage(PM_Chat.TargetClient, $"Discord account '{oldName}' has been unlinked.");
        }
    }
}
