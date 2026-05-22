using GameServer.Core;
using GameServer.Hooks.TCPNetwork;
using GameServer.Misc;
using Shared;
using Shared.Misc;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;
using static TCPNetwork.Packets.PKT_Chat;

namespace GameServer.PacketManager
{
    public class PM_Chat : PM_Base
    {
        private static Semaphore LogSemaphore { get; set; } = new Semaphore(1, 1);

        private static Semaphore CommandSemaphore { get; set; } = new Semaphore(1, 1);

        private static string SystemName { get; set; } = "CONSOLE";

        private static string NotificationName { get; set; } = "SERVER";

        public static ServerClient TargetClient { get; set; } = null;

        public static string[] LatestCommand { get; set; } = null;

        public static string[] DefaultJoinMessages { get; set; } = new string[]
        {
            "Welcome to the global chat!",
            "Use '/help' to check all the available commands."
        };

        // Rate limit chat — flood would DoS the broadcast + per-message disk writes.
        // 200ms = 5 msg/sec, above human typing.
        private const int ChatRateGapMs = 200;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> LastChatTicks =
            new System.Collections.Concurrent.ConcurrentDictionary<string, long>(System.StringComparer.OrdinalIgnoreCase);

        [HandlesPacket(PacketHeader.ChatManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_Chat data = Serializer.ConvertBytesToObject<PKT_Chat>(bytes);

            // KMH: Sanitize input - prevent empty/oversized messages
            if (data == null || string.IsNullOrWhiteSpace(data.Message)) return;
            if (data.Message.Length > 512) data.Message = data.Message.Substring(0, 512);

            // Commands stay unthrottled — CommandSemaphore + admin use cases need rapid sequences.
            if (!data.IsCommand)
            {
                string u = client?.UserFile?.Username;
                if (!string.IsNullOrEmpty(u))
                {
                    long now = System.DateTime.UtcNow.Ticks;
                    if (LastChatTicks.TryGetValue(u, out long last)
                        && (now - last) / System.TimeSpan.TicksPerMillisecond < ChatRateGapMs)
                    {
                        // Silent drop — feedback would just amplify the spam.
                        return;
                    }
                    LastChatTicks[u] = now;
                }
            }

            if (data.IsCommand) ExecuteChatCommand(client, data.Message.Split(' '));
            else BroadcastChatMessage(client, data.Message);
        }

        public static void SendConsoleMessage(ServerClient client, string message)
        {
            PKT_Chat chatData = new PKT_Chat();
            chatData.Username = SystemName;
            chatData.Message = message;
            chatData.UsernameColor = ChatColor.Console;
            chatData.MessageColor = ChatColor.Console;

            client.Listener.EnqueuePacket(PacketHeader.ChatManager, chatData);
        }

        public static void SendServerMessage(ServerClient client, string message)
        {
            PKT_Chat chatData = new PKT_Chat();
            chatData.Username = NotificationName;
            chatData.Message = message;
            chatData.UsernameColor = ChatColor.Server;
            chatData.MessageColor = ChatColor.Server;

            client.Listener.EnqueuePacket(PacketHeader.ChatManager, chatData);
        }

        private static void BroadcastChatMessage(ServerClient client, string message)
        {
            PKT_Chat chatData = new PKT_Chat();
            chatData.Username = client.UserFile.Username;
            chatData.Message = message;
            chatData.UsernameColor = client.UserFile.IsAdmin ? ChatColor.Admin : ChatColor.Normal;
            chatData.MessageColor = ChatColor.Normal;

            ServerNetwork.SendPacketToAllClients(PacketHeader.ChatManager, chatData);
            PM_Chat.WriteChatInConsole(client.UserFile.Username, message);
            // KMH: Relay in-game chat to Discord
            GameServer.Integrations.Discord.DiscordBridge.TryRelayGameChatToDiscord(client.UserFile.Username, message);
            WriteToLogs(client.UserFile.Username, message);
        }

        public static void BroadcastConsoleMessage(string message)
        {
            PKT_Chat chatData = new PKT_Chat();
            chatData.Username = SystemName;
            chatData.Message = message;
            chatData.UsernameColor = ChatColor.Console;
            chatData.MessageColor = ChatColor.Console;

            ServerNetwork.SendPacketToAllClients(PacketHeader.ChatManager, chatData);
            PM_Chat.WriteChatInConsole(chatData.Username, message);
            WriteToLogs(chatData.Username, message);
        }

        public static void BroadcastServerNotification(string message)
        {
            PKT_Chat chatData = new PKT_Chat();
            chatData.Username = NotificationName;
            chatData.Message = message;
            chatData.UsernameColor = ChatColor.Server;
            chatData.MessageColor = ChatColor.Server;

            ServerNetwork.SendPacketToAllClients(PacketHeader.ChatManager, chatData);
            PM_Chat.WriteChatInConsole(chatData.Username, message);
            WriteToLogs(chatData.Username, message);
        }

        private static void ExecuteChatCommand(ServerClient client, string[] command)
        {
            CommandSemaphore.WaitOne();

            try
            {
                CMD_Base toFind = CMD_Base.ChatCommands.FirstOrDefault(fetch => fetch.Prefix == command[0]);
                if (toFind == null) SendConsoleMessage(client, "Command was not found.");
                else
                {
                    TargetClient = client;
                    LatestCommand = command;
                    toFind.Action();
                }

                string chatCommand = string.Join(" ", command);

                PM_Chat.WriteChatInConsole(client.UserFile.Username, chatCommand);
            }
            catch (Exception ex) { Printer.Error(ex); }

            CommandSemaphore.Release();
        }

        private static void WriteToLogs(string username, string message)
        {
            LogSemaphore.WaitOne();

            try
            {
                DateTime now = DateTime.Now;
                string line = $"[{now:HH:mm:ss}] | [{username}]: {message}{Environment.NewLine}";
                string path = Path.Combine(Master.ChatLogsPath, $"{now:yyyy-MM-dd}.txt");
                File.AppendAllText(path, line);
            }
            catch (Exception ex) { Printer.Error(ex); }

            LogSemaphore.Release();
        }

        // KMH: Broadcast Discord messages with Discord color
        public static void BroadcastDiscordMessage(string discordName, string message)
        {
            PKT_Chat chatData = new PKT_Chat();
            chatData.Username = $"[Discord] {discordName}";
            chatData.Message = message;
            chatData.UsernameColor = ChatColor.Discord;
            chatData.MessageColor = ChatColor.Normal;

            ServerNetwork.SendPacketToAllClients(PacketHeader.ChatManager, chatData);
            PM_Chat.WriteChatInConsole(discordName, message, fromDiscord: true);
            WriteToLogs($"[Discord] {discordName}", message);
        }

        public static void WriteChatInConsole(string username, string message, bool fromDiscord = false)
        {
            if (!Master.ServerConfig.DisplayChatInConsole) return;
            else
            {
                if (fromDiscord) Printer.Message($"[Discord] > {username} > {message}");
                else InformationDisplayer.DisplayChatMap(username, message);
            }
        }

        public static void SendLoginChatMessages(ServerClient client)
        {
            foreach (string str in PM_Chat.DefaultJoinMessages) PM_Chat.SendConsoleMessage(client, str);

            if (Master.ChatConfig.EnableMoTD) PM_Chat.SendServerMessage(client, $"MoTD > {Master.ChatConfig.MessageOfTheDay}");

            if (Master.ChatConfig.LoginNotifications) PM_Chat.BroadcastServerNotification($"{client.UserFile.Username} has joined the server!");
        }
    }
}

