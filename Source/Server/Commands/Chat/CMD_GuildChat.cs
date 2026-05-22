using GameServer.Hooks.TCPNetwork;
using GameServer.PacketManager;
using Shared;
using System;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static TCPNetwork.Packets.PKT_Chat;

namespace GameServer.Commands.Chat
{
    /// <summary>
    /// /g &lt;message&gt; — broadcast a message only to online members of the
    /// sender's guild. Tagged with the [Guild] prefix.
    /// </summary>
    public class CMD_GuildChat : CMD_Base
    {
        public CMD_GuildChat()
        {
            Prefix = "/g";
            Description = "Send a message to your guild channel only";
            IsChatCommand = true;
        }

        public override void Action()
        {
            if (PM_Chat.TargetClient == null) return;

            string senderGuild = PM_Chat.TargetClient.UserFile?.GuildName;
            if (string.IsNullOrEmpty(senderGuild))
            {
                PM_Chat.SendConsoleMessage(PM_Chat.TargetClient, "You're not in a guild.");
                return;
            }

            string message = string.Empty;
            for (int i = 1; i < PM_Chat.LatestCommand.Length; i++)
                message += PM_Chat.LatestCommand[i] + " ";

            message = message.Trim();
            if (string.IsNullOrWhiteSpace(message))
            {
                PM_Chat.SendConsoleMessage(PM_Chat.TargetClient, "Message was empty.");
                return;
            }

            string sender = PM_Chat.TargetClient.UserFile.Username;
            PKT_Chat chatData = new PKT_Chat
            {
                Message = message,
                UsernameColor = ChatColor.Private,
                MessageColor = ChatColor.Private,
                Username = $"[Guild] {sender}"
            };

            int delivered = 0;
            foreach (ServerClient sc in ServerNetwork.GetConnectedClients())
            {
                if (sc?.UserFile == null) continue;
                if (!string.Equals(sc.UserFile.GuildName, senderGuild, StringComparison.OrdinalIgnoreCase)) continue;
                sc.Listener.EnqueuePacket(PacketHeader.ChatManager, chatData);
                delivered++;
            }

            PM_Chat.WriteChatInConsole(chatData.Username, message);
            if (delivered <= 1)
                PM_Chat.SendConsoleMessage(PM_Chat.TargetClient, "(no other guild members are online)");
        }
    }
}
