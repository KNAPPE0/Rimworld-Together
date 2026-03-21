using GameServer.Commands;
using GameServer.Core;
using GameServer.Hooks.TCPNetwork;
using GameServer.Integrations.Discord;
using GameServer.Misc;
using Shared;
using Shared.Misc;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static Shared.CommonEnumerators;

namespace GameServer.PacketManager
{
    public class PM_Chat : PM_Base
    {
        private static readonly Semaphore LogSemaphore = new Semaphore(1, 1);
        private static readonly Semaphore CommandSemaphore = new Semaphore(1, 1);

        private static string SystemName { get; set; } = "CONSOLE";
        private static string NotificationName { get; set; } = "SERVER";

        public static string[] DefaultJoinMessages { get; set; } = new string[]
        {
            "Welcome to the global chat!",
            "Please be considerate with others and have fun!",
            "Use '/help' to check all the available commands."
        };

        public static string[] DefaultTextTools { get; set; } = new string[]
        {
            "List of available text tools:",
            "'b' inside brackets - Followed by the text you want to turn [b]bold",
            "'i' inside brackets - Followed by the text you want to turn [i]cursive",
            "HTML color inside brackets - Followed by the text you want to [ff0000]change color"
        };

        [HandlesPacket(PacketHeader.ChatManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            if (client == null || bytes == null || bytes.Length == 0)
                return;

            PKT_Chat data = Serializer.ConvertBytesToObject<PKT_Chat>(bytes);
            if (data == null)
                return;

            string msg = data._message ?? string.Empty;
            if (string.IsNullOrWhiteSpace(msg))
                return;

            if (msg.StartsWith("/"))
                ExecuteChatCommand(client, msg.Split(' '));
            else
                BroadcastChatMessage(client, msg);
        }

        private static void ExecuteChatCommand(ServerClient client, string[] command)
        {
            if (client == null || command == null || command.Length == 0)
                return;

            CommandSemaphore.WaitOne();

            try
            {
                CommandBase toFind = ChatManagerHelper.GetCommandFromName(command[0]);
                if (toFind == null)
                {
                    SendConsoleMessage(client, "Command was not found.");
                }
                else
                {
                    ChatCommandActions.TargetClient = client;
                    ChatCommandActions.Command = command;
                    toFind.CommandAction.Invoke();
                }

                string chatCommand = string.Join(" ", command).Trim();
                ChatManagerHelper.ShowChatInConsole(client.UserFile?.Username ?? "Unknown", chatCommand);
            }
            finally
            {
                CommandSemaphore.Release();
            }
        }

        private static void BroadcastChatMessage(ServerClient client, string message)
        {
            if (client == null || client.UserFile == null || string.IsNullOrWhiteSpace(message))
                return;

            PKT_Chat chatData = new PKT_Chat();
            chatData._username = client.UserFile.Username;
            chatData._message = message;
            chatData._usernameColor = client.UserFile.IsAdmin ? ChatColor.Admin : ChatColor.Normal;
            chatData._messageColor = ChatColor.Normal;

            ServerNetwork.SendPacketToAllClients(PacketHeader.ChatManager, chatData);

            WriteToLogs(client.UserFile.Username, message);
            ChatManagerHelper.ShowChatInConsole(client.UserFile.Username, message);

            DiscordBridge.TryRelayGameChatToDiscord(chatData._username, chatData._message);
        }

        public static void BroadcastDiscordMessage(string client, string message)
        {
            if (string.IsNullOrWhiteSpace(client) || string.IsNullOrWhiteSpace(message))
                return;

            PKT_Chat chatData = new PKT_Chat();
            chatData._username = client;
            chatData._message = message;
            chatData._usernameColor = ChatColor.Discord;
            chatData._messageColor = ChatColor.Discord;

            ServerNetwork.SendPacketToAllClients(PacketHeader.ChatManager, chatData);

            WriteToLogs(client, message);
            ChatManagerHelper.ShowChatInConsole(client, message, true);
        }

        public static void BroadcastConsoleMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            PKT_Chat chatData = new PKT_Chat();
            chatData._username = SystemName;
            chatData._message = message;
            chatData._usernameColor = ChatColor.Console;
            chatData._messageColor = ChatColor.Console;

            ServerNetwork.SendPacketToAllClients(PacketHeader.ChatManager, chatData);

            WriteToLogs(chatData._username, message);
            ChatManagerHelper.ShowChatInConsole(chatData._username, message);
        }

        public static void BroadcastServerNotification(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            PKT_Chat chatData = new PKT_Chat();
            chatData._username = NotificationName;
            chatData._message = message;
            chatData._usernameColor = ChatColor.Server;
            chatData._messageColor = ChatColor.Server;

            ServerNetwork.SendPacketToAllClients(PacketHeader.ChatManager, chatData);

            WriteToLogs(chatData._username, message);
            ChatManagerHelper.ShowChatInConsole(chatData._username, message);
        }

        public static void SendConsoleMessage(ServerClient client, string message)
        {
            if (client == null || string.IsNullOrWhiteSpace(message))
                return;

            PKT_Chat chatData = new PKT_Chat();
            chatData._username = SystemName;
            chatData._message = message;
            chatData._usernameColor = ChatColor.Console;
            chatData._messageColor = ChatColor.Console;

            client.Listener.EnqueuePacket(PacketHeader.ChatManager, chatData);
        }

        public static void SendServerMessage(ServerClient client, string message)
        {
            if (client == null || string.IsNullOrWhiteSpace(message))
                return;

            PKT_Chat chatData = new PKT_Chat();
            chatData._username = NotificationName;
            chatData._message = message;
            chatData._usernameColor = ChatColor.Server;
            chatData._messageColor = ChatColor.Server;

            client.Listener.EnqueuePacket(PacketHeader.ChatManager, chatData);
        }

        private static void WriteToLogs(string username, string message)
        {
            LogSemaphore.WaitOne();

            try
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append($"[{DateTime.Now:HH:mm:ss}] | [{username}]: {message}");
                stringBuilder.Append(Environment.NewLine);

                DateTime dateTime = DateTime.Now.Date;
                string nowFileName = $"{dateTime.Year}-{dateTime.Month:D2}-{dateTime.Day:D2}";
                string nowFullPath = Path.Combine(Master.ChatLogsPath, nowFileName + ".txt");

                File.AppendAllText(nowFullPath, stringBuilder.ToString());
            }
            finally
            {
                LogSemaphore.Release();
            }
        }
    }

    public class ChatManagerHelper
    {
        public static ServerClient GetUserFromName(string username)
        {
            return ServerNetwork.GetConnectedClientFromUsername(username);
        }

        public static CommandBase GetCommandFromName(string commandName)
        {
            return ChatCommands.commands.FirstOrDefault(x => x.Prefix == commandName);
        }

        public static string GetUsernameFromMention(string mention)
        {
            return string.IsNullOrWhiteSpace(mention) ? string.Empty : mention.Replace("@", "");
        }

        public static void ShowChatInConsole(string username, string message, bool fromDiscord = false)
        {
            if (!Master.ServerConfig.DisplayChatInConsole)
                return;

            if (fromDiscord)
                Printer.Message($"[Discord] > {username} > {message}");
            else
                InformationDisplayer.DisplayChatMap(username, message);
        }
    }
}