using GameServer.Hooks.TCPNetwork;
using GameServer.Managers;
using GameServer.PacketManager;
using Shared;
using TCPNetwork.Files.Client;
using static TCPNetwork.Packets.PKT_Chat;
using System;

namespace GameServer.Commands.Chat
{
    public class CMD_Link : CMD_Base
    {
        public CMD_Link()
        {
            Prefix = "/link";
            Description = "Generate a token to link your Discord account. Usage: /link";
            IsChatCommand = true;
        }

        private static readonly System.Collections.Generic.Dictionary<string, long> LastLinkRequest = 
            new System.Collections.Generic.Dictionary<string, long>();

        public override void Action()
        {
            if (PM_Chat.TargetClient == null) return;

            ServerClient client = PM_Chat.TargetClient;
            UserFile user = client.UserFile;
            if (user == null) return;

            // Check if already linked
            if (!string.IsNullOrEmpty(user.DiscordId))
            {
                string dname = user.DiscordUsername ?? user.DiscordId;
                PM_Chat.SendConsoleMessage(client, $"Already linked to Discord: {dname}. Use /unlink first.");
                return;
            }

            // Rate limit: 30 seconds between /link requests
            string username = user.Username;
            long nowTicks = DateTime.UtcNow.Ticks;
            if (LastLinkRequest.ContainsKey(username))
            {
                double secondsSinceLast = (nowTicks - LastLinkRequest[username]) / (double)TimeSpan.TicksPerSecond;
                if (secondsSinceLast < 30)
                {
                    int wait = (int)(30 - secondsSinceLast);
                    PM_Chat.SendConsoleMessage(client, $"Wait {wait}s before requesting another token.");
                    return;
                }
            }
            LastLinkRequest[username] = nowTicks;

            // Invalidate any existing token first
            if (!string.IsNullOrEmpty(user.DiscordLinkToken))
            {
                user.DiscordLinkToken = null;
                user.DiscordLinkTokenExpiry = 0;
            }

            // Generate unique token
            string token = "RWT-" + GenerateToken(6);

            // Store with 10-minute expiry
            user.DiscordLinkToken = token;
            user.DiscordLinkTokenExpiry = DateTime.UtcNow.AddMinutes(10).Ticks;
            user.SaveUserFile();

            PM_Chat.SendConsoleMessage(client, $"Your link token: {token}");
            PM_Chat.SendConsoleMessage(client, "Send to Discord bot within 10 min: !link " + token);
        }

        private static string GenerateToken(int length)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            char[] result = new char[length];
            Random rng = new Random();
            for (int i = 0; i < length; i++)
                result[i] = chars[rng.Next(chars.Length)];
            return new string(result);
        }
    }
}
