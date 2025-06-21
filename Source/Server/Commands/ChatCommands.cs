using Shared;
using System;
using System.Collections.Generic;
using Shared.Packets.Data;
using static Shared.CommonEnumerators;
using static GameServer.Commands.ChatCommandActions;
using GameServer.Managers;
using GameServer.TCP;

namespace GameServer.Commands
{
    public static class ChatCommands
    {
        private static readonly CommandBase HelpCommand = new CommandBase("/help", 0,
            "Shows a list of all available commands", HelpCommandAction);

        private static readonly CommandBase ToolsCommand = new CommandBase("/tools", 0,
            "Shows a list of all available chat tools", ToolsCommandAction);

        private static readonly CommandBase PingCommand = new CommandBase("/ping", 0,
            "Checks if the connection to the server is working", PingCommandAction);

        private static readonly CommandBase DisconnectCommand = new CommandBase("/dc", 0,
            "Forcefully disconnects you from the server", DisconnectCommandAction);

        private static readonly CommandBase PMCommand = new CommandBase("/w", 0,
            "Sends a private message to a specific user", PrivateMessageCommandAction);

        // ─────── Chat leaderboard command ───────
        private static readonly CommandBase LeaderboardCommand = new CommandBase("/leaderboard", 0,
            "Shows the top-10 players by stats (use '/leaderboard N' for custom count)",
            LeaderboardCommandAction);

        public static readonly CommandBase[] commands =
        {
            HelpCommand,
            ToolsCommand,
            PingCommand,
            DisconnectCommand,
            PMCommand,
            LeaderboardCommand
        };
    }

    public static class ChatCommandActions
    {
        public static ServerClient? TargetClient { get; set; }
        public static string[]?     Command      { get; set; }

        public static void HelpCommandAction()
        {
            if (TargetClient == null) return;

            var lines = new List<string> { "Available commands:" };
            foreach (var cmd in ChatCommands.commands)
                lines.Add($"{cmd.Prefix} - {cmd.Description}");

            foreach (var line in lines)
                ChatManager.SendConsoleMessage(TargetClient, line);
        }

        public static void ToolsCommandAction()
        {
            if (TargetClient == null) return;
            foreach (var s in ChatManager.defaultTextTools)
                ChatManager.SendConsoleMessage(TargetClient, s);
        }

        public static void PingCommandAction()
        {
            if (TargetClient == null) return;
            ChatManager.SendConsoleMessage(TargetClient, "Pong!");
        }

        public static void DisconnectCommandAction()
        {
            if (TargetClient == null) return;
            TargetClient.Listener.DisconnectFlag = true;
        }

        public static void PrivateMessageCommandAction()
        {
            if (TargetClient == null || Command is not { Length: > 2 }) return;

            string msg = string.Join(' ', Command, 2, Command.Length - 2);
            if (string.IsNullOrWhiteSpace(msg))
            {
                ChatManager.SendConsoleMessage(TargetClient, "Message was empty.");
                return;
            }

            string mention = Command[1].TrimStart('@');
            var recipient = ChatManagerHelper.GetUserFromName(mention);
            if (recipient == null)
            {
                ChatManager.SendConsoleMessage(TargetClient, "User not found.");
                return;
            }

            if (recipient == TargetClient)
            {
                ChatManager.SendConsoleMessage(TargetClient, "Cannot whisper to yourself.");
                return;
            }

            // Send to sender
            var data = new ChatData { _username = $">> {recipient.UserFile.Label}", _message = msg, _usernameColor = UserColor.Private, _messageColor = MessageColor.Private };
            TargetClient.Listener.EnqueuePacket(PacketHeader.ChatManager, data);

            // Send to recipient
            data._username = $"<< {TargetClient.UserFile.Label}";
            recipient.Listener.EnqueuePacket(PacketHeader.ChatManager, data);

            ChatManagerHelper.ShowChatInConsole(data._username, msg);
        }

        public static void LeaderboardCommandAction()
        {
            if (TargetClient == null || Command == null) return;

            int limit = 10;
            if (Command.Length > 1 && int.TryParse(Command[1], out var n) && n > 0)
                limit = n;

            // Fetch live top stats
            var topList = StatsManager.GetLiveTop(limit);
            if (topList.Count == 0)
            {
                ChatManager.SendConsoleMessage(TargetClient, "No statistics available.");
                return;
            }

            ChatManager.SendConsoleMessage(TargetClient, $"--- Top {topList.Count} Players ---");
            for (int i = 0; i < topList.Count; i++)
            {
                var s = topList[i];
                var ts = TimeSpan.FromSeconds(s._playtimeSeconds);
                string line = string.Format(
                    "{0}. {1} - Wealth: ${2:N0}, Colonists: {3}, Playtime: {4}h {5}m, Days: {6}",
                    i + 1,
                    s._uid,
                    s._wealth,
                    s._colonistCount,
                    ts.Hours,
                    ts.Minutes,
                    s._daysPassed
                );
                ChatManager.SendConsoleMessage(TargetClient, line);
            }
        }
    }
}