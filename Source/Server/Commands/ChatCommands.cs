// File: ChatCommands.cs  (Server File)
using Shared;
using System;
using System.Collections.Generic;
using static Shared.CommonEnumerators;
using static GameServer.Commands.ChatCommandActions;
using static GameServer.Commands.ChatCommands;
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

        // ─────── Newly added: in‐chat /leaderboard ───────
        private static readonly CommandBase LeaderboardCommand = new CommandBase("/leaderboard", 0,
            "Shows the top-10 richest players (use '/leaderboard N' for custom count)",
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

            var list = new List<string> { "List of available commands:" };
            foreach (var cmd in ChatCommands.commands)
                list.Add($"{cmd.Prefix} - {cmd.Description}");

            foreach (string line in list)
                ChatManager.SendConsoleMessage(TargetClient, line);
        }

        public static void ToolsCommandAction()
        {
            if (TargetClient == null) return;
            foreach (string s in ChatManager.defaultTextTools)
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

            var recipientUid = ChatManagerHelper.GetUsernameFromMention(Command[1]);
            var recipient = ChatManagerHelper.GetUserFromName(recipientUid);
            if (recipient == null)
            {
                ChatManager.SendConsoleMessage(TargetClient, "User was not found.");
                return;
            }

            if (recipient == TargetClient)
            {
                ChatManager.SendConsoleMessage(TargetClient, "Can't send a whisper to yourself.");
                return;
            }

            var data = new ChatData
            {
                _message = msg,
                _usernameColor = UserColor.Private,
                _messageColor = MessageColor.Private
            };

            // to sender
            data._username = $">> {recipient.UserFile.Label}";
            TargetClient.Listener.EnqueuePacket(PacketHeader.ChatManager, data);

            // to recipient
            data._username = $"<< {TargetClient.UserFile.Label}";
            recipient.Listener.EnqueuePacket(PacketHeader.ChatManager, data);

            ChatManagerHelper.ShowChatInConsole(data._username, msg);
        }

        // Chat leaderboard action
        public static void LeaderboardCommandAction()
        {
            if (TargetClient == null) return;

            int limit = 10;
            if (Command!.Length > 1 && int.TryParse(Command[1], out int n) && n > 0)
                limit = n;

            // Use the "username‐only" formatting here:
            string leaderboardText = WealthManager.FormatLeaderboardUsernames(limit)
                                       .Replace(Environment.NewLine, "\n");
            foreach (string line in leaderboardText.Split('\n'))
                ChatManager.SendConsoleMessage(TargetClient, line);
        }
    }
}