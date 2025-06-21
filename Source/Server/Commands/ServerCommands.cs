using Shared; 
using static GameServer.Commands.ConsoleCommands;
using static Shared.CommonEnumerators;
using static GameServer.Commands.ConsoleCommandActions;
using GameServer.Core;
using GameServer.Files;
using GameServer.Managers;
using GameServer.Misc;
using GameServer.TCP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Text.RegularExpressions;

namespace GameServer.Commands
{
    public static class ConsoleCommands
    {
        private static readonly CommandBase HelpCommand = new CommandBase("help", 0,
            "Shows a list of all available commands to use",
            HelpCommandAction);

        public static readonly CommandBase BackupCommand = new CommandBase("backup", 0,
            "Backup the server.",
            BackupCommandAction);

        public static readonly CommandBase BackupUserCommand = new CommandBase("backupuser", 1,
            "Backup the data of a specific user",
            BackupUserCommandAction);

        public static readonly CommandBase ListCommand = new CommandBase("list", 0,
            "Shows all connected players",
            ListCommandAction);

        public static readonly CommandBase OpCommand = new CommandBase("op", 1,
            "Gives admin privileges to the selected player",
            OpCommandAction);

        public static readonly CommandBase DeopCommand = new CommandBase("deop", 1,
            "Removes admin privileges from the selected player",
            DeopCommandAction);

        public static readonly CommandBase KickCommand = new CommandBase("kick", 1,
            "Kicks the selected player from the server",
            KickCommandAction);

        public static readonly CommandBase BanCommand = new CommandBase("ban", 1,
            "Bans the selected player from the server",
            BanCommandAction);

        public static readonly CommandBase PardonCommand = new CommandBase("pardon", 1,
            "Pardons the selected player from the server",
            PardonCommandAction);

        public static readonly CommandBase DeepListCommand = new CommandBase("deeplist", 0,
            "Shows a list of all server players",
            DeepListCommandAction);

        public static readonly CommandBase BanListCommand = new CommandBase("banlist", 0,
            "Shows a list of all banned server players",
            BanListCommandAction);

        public static readonly CommandBase ReloadCommand = new CommandBase("reload", 0,
            "Reloads all server resources",
            ReloadCommandAction);

        public static readonly CommandBase ModListCommand = new CommandBase("modlist", 0,
            "Shows all currently loaded mods",
            ModListCommandAction);

        public static readonly CommandBase DoSiteRewards = new CommandBase("dositerewards", 0,
            "Forces site rewards to run",
            DoSiteRewardsCommandAction);

        public static readonly CommandBase EventCommand = new CommandBase("event", 2,
            "Sends an event to the selected player",
            EventCommandAction);

        public static readonly CommandBase EventAllCommand = new CommandBase("eventall", 1,
            "Sends an event to all connected players",
            EventAllCommandAction);

        public static readonly CommandBase EventListCommand = new CommandBase("eventlist", 0,
            "Shows a list of all available events to use",
            EventListCommandAction);

        public static readonly CommandBase BroadcastCommand = new CommandBase("broadcast", -1,
            "Broadcast a message to all connected players",
            BroadcastCommandAction);

        public static readonly CommandBase ServerMessageCommand = new CommandBase("chat", -1,
            "Send a message in chat from the Server",
            ServerMessageCommandAction);

        public static readonly CommandBase WhitelistCommand = new CommandBase("whitelist", 0,
            "Shows all whitelisted players",
            WhitelistCommandAction);

        public static readonly CommandBase WhitelistAddCommand = new CommandBase("whitelistadd", 1,
            "Adds a player to the whitelist",
            WhitelistAddCommandAction);

        public static readonly CommandBase WhitelistRemoveCommand = new CommandBase("whitelistremove", 1,
            "Removes a player from the whitelist",
            WhitelistRemoveCommandAction);

        public static readonly CommandBase ForceSaveCommand = new CommandBase("forcesave", 1,
            "Forces a player to sync their save",
            ForceSaveCommandAction);

        public static readonly CommandBase ResetPlayerCommand = new CommandBase("resetplayer", 1,
            "Resets a player profile from the server",
            ResetPlayerCommandAction);

        public static readonly CommandBase PortforwardCommand = new CommandBase("portforward", 0,
            "Will use UPnP to portforward the server",
            PortForwardCommandAction);

        public static readonly CommandBase ResetWorldCommand = new CommandBase("resetworld", 0,
            "Resets all the world related data and stores a backup of it",
            ResetWorldCommandAction);

        public static readonly CommandBase QuitCommand = new CommandBase("quit", 0,
            "Saves all player data and then closes the server",
            QuitCommandAction);

        public static readonly CommandBase ForceQuitCommand = new CommandBase("forcequit", 0,
            "Closes the server without saving player data",
            ForceQuitCommandAction);

        public static readonly CommandBase ClearCommand = new CommandBase("clear", 0,
            "Clears the console output",
            ClearCommandAction);

        // ─────────── Our new “leaderboard” command ───────────
        public static readonly CommandBase LeaderboardCommand = new CommandBase("leaderboard", 0,
            "Shows the top–10 richest players (use 'leaderboard N' for a custom count)",
            LeaderboardCommandAction);

        public static List<CommandBase> Commands = new List<CommandBase>
        {
            BackupCommand,
            BackupUserCommand,
            BanCommand,
            BanListCommand,
            BroadcastCommand,
            ClearCommand,
            DeepListCommand,
            DeopCommand,
            DoSiteRewards,
            EventAllCommand,
            EventCommand,
            EventListCommand,
            ForceQuitCommand,
            ForceSaveCommand,
            HelpCommand,
            KickCommand,
            ListCommand,
            ModListCommand,
            OpCommand,
            PardonCommand,
            PortforwardCommand,
            QuitCommand,
            ReloadCommand,
            ResetPlayerCommand,
            ResetWorldCommand,
            ServerMessageCommand,
            WhitelistAddCommand,
            WhitelistCommand,
            WhitelistRemoveCommand,
            LeaderboardCommand
        };
    }

    public static class ConsoleCommandActions
    {
        public static void HelpCommandAction()
        {
            Printer.Title($"List of available commands: [{ConsoleCommands.Commands.Count}]");
            Printer.Title("----------------------------------------");

            foreach (var command in ConsoleCommands.Commands.OrderBy(c => c.Prefix))
            {
                Printer.Warning($"{command.Prefix} - {command.Description}");
            }

            Printer.Title("----------------------------------------");
        }

        public static void BackupCommandAction()
        {
            BackupManager.BackupServer();
        }

        public static void BackupUserCommandAction()
        {
            var userFile = UserManagerH.GetUserFileFromName(ConsoleManager.commandParameters[0]);
            if (userFile == null)
            {
                ThrowUserNotFoundError();
                return;
            }

            Printer.Warning("Do you want this backup to be persistent? (Will not be automatically deleted)");
            Printer.Warning("Please type 'YES' or 'NO'");

        DeleteUser:
            string response = Console.ReadLine();
            if (response == "NO") BackupManager.BackupUser(userFile.Uid);
            else if (response == "YES") BackupManager.BackupUser(userFile.Uid, true);
            else
            {
                Printer.Error($"{response} is not a valid option; The options must be capitalized");
                goto DeleteUser;
            }
        }

        public static void ListCommandAction()
        {
            Printer.Title($"Connected players: [{NetworkHelper.GetConnectedClientsSafe().Count()}]");
            Printer.Title("----------------------------------------");
            foreach (var client in NetworkHelper.GetConnectedClientsSafe())
            {
                Printer.Warning($"{client.UserFile.SavedIP} - {client.UserFile.Label} - {client.UserFile.Uid}");
            }
            Printer.Title("----------------------------------------");
        }

        public static void DeepListCommandAction()
        {
            var userFiles = UserManagerH.GetAllUserFiles();
            Printer.Title($"Server players: [{userFiles.Length}]");
            Printer.Title("----------------------------------------");
            foreach (var user in userFiles)
            {
                Printer.Warning($"{user.Label} - {user.Uid}");
            }
            Printer.Title("----------------------------------------");
        }

        public static void OpCommandAction()
        {
            var toFind = UserManagerH.GetAllUserFiles()
                .FirstOrDefault(u => u.Uid == ConsoleManager.commandParameters[0]);
            if (toFind == null)
            {
                ThrowUserNotFoundError();
                return;
            }

            if (CheckIfIsAlready(toFind)) return;

            toFind.UpdateAdmin(true);

            var client = NetworkHelper.GetConnectedClientFromUid(toFind.Uid);
            if (client != null)
            {
                var commandData = new CommandData
                {
                    _commandMode = CommandMode.Op
                };
                client.UserFile.UpdateAdmin(true);
                client.Listener.EnqueuePacket(PacketHeader.ConsoleManager, commandData);
            }
            UserManagerH.SaveUserFile(toFind);
            Printer.Warning($"User '{toFind.Label}' has now admin privileges");
            bool CheckIfIsAlready(UserFile userFile)
            {
                if (userFile.IsAdmin)
                {
                    Printer.Warning($"User '{userFile.Label}' was already an admin");
                    return true;
                }
                return false;
            }
        }

        public static void DeopCommandAction()
        {
            var toFind = UserManagerH.GetAllUserFiles()
                .FirstOrDefault(u => u.Uid == ConsoleManager.commandParameters[0]);
            if (toFind == null)
            {
                ThrowUserNotFoundError();
                return;
            }

            if (CheckIfIsAlready(toFind)) return;

            toFind.UpdateAdmin(false);

            var client = NetworkHelper.GetConnectedClientFromUid(toFind.Uid);
            if (client != null)
            {
                var commandData = new CommandData
                {
                    _commandMode = CommandMode.Deop
                };
                client.UserFile.UpdateAdmin(false);
                client.Listener.EnqueuePacket(PacketHeader.ConsoleManager, commandData);
            }
            UserManagerH.SaveUserFile(toFind);
            Printer.Warning($"User '{toFind.Label}' is no longer an admin");
            bool CheckIfIsAlready(UserFile userFile)
            {
                if (!userFile.IsAdmin)
                {
                    Printer.Warning($"User '{userFile.Label}' was not an admin");
                    return true;
                }
                return false;
            }
        }

        public static void KickCommandAction()
        {
            var toFind = NetworkHelper.GetConnectedClientFromUid(ConsoleManager.commandParameters[0]);
            if (toFind == null)
            {
                ThrowUserNotFoundError();
                return;
            }
            toFind.Listener.DisconnectFlag = true;
            Printer.Warning($"User '{toFind.UserFile.Label}' has been kicked from the server");
        }

        public static void BanListCommandAction()
        {
            var bannedUsers = UserManagerH.GetAllUserFiles().Where(u => u.IsBanned).ToList();
            Printer.Title($"Banned players: [{bannedUsers.Count}]");
            Printer.Title("----------------------------------------");
            foreach (var user in bannedUsers)
            {
                Printer.Warning($"{user.Label} - {user.SavedIP}");
            }
            Printer.Title("----------------------------------------");
        }

        public static void BanCommandAction()
        {
            UserManager.BanPlayerFromName(ConsoleManager.commandParameters[0]);
        }

        public static void PardonCommandAction()
        {
            UserManager.PardonPlayerFromName(ConsoleManager.commandParameters[0]);
        }

        public static void ReloadCommandAction()
        {
            Main_.LoadResources();
        }

        public static void ModListCommandAction()
        {
            Printer.Title($"Required Mods: [{Master.ModConfig.RequiredMods.Length}]");
            Printer.Title("----------------------------------------");
            foreach (var mod in Master.ModConfig.RequiredMods)
                Printer.Warning(mod);
            Printer.Title("----------------------------------------");

            Printer.Title($"Optional Mods: [{Master.ModConfig.OptionalMods.Length}]");
            Printer.Title("----------------------------------------");
            foreach (var mod in Master.ModConfig.OptionalMods)
                Printer.Warning(mod);
            Printer.Title("----------------------------------------");

            Printer.Title($"Forbidden Mods: [{Master.ModConfig.ForbiddenMods.Length}]");
            Printer.Title("----------------------------------------");
            foreach (var mod in Master.ModConfig.ForbiddenMods)
                Printer.Warning(mod);
            Printer.Title("----------------------------------------");
        }

        public static void DoSiteRewardsCommandAction()
        {
            Printer.Title("Forced site rewards");
            SiteManager.SiteRewardTick();
        }

        public static void EventCommandAction()
        {
            var client = NetworkHelper.GetConnectedClientFromUid(ConsoleManager.commandParameters[0]);
            if (client == null)
            {
                Printer.Warning($"User '{ConsoleManager.commandParameters[0]}' was not found");
                return;
            }

            var toFind = EventManagerHelper.loadedEvents
                .FirstOrDefault(ev => ev.DefName == ConsoleManager.commandParameters[1]);
            if (toFind == null)
            {
                Printer.Warning($"Event '{ConsoleManager.commandParameters[1]}' was not found");
                return;
            }

            var eventData = new EventData
            {
                _stepMode = EventStepMode.Receive,
                _eventFile = toFind,
                _toTile = -1 // let client fall at any settlement
            };

            client.Listener.EnqueuePacket(PacketHeader.EventManager, eventData);
            Printer.Title($"Sent event '{ConsoleManager.commandParameters[1]}' to '{ConsoleManager.commandParameters[0]}'");
        }

        public static void EventAllCommandAction()
        {
            var toFind = EventManagerHelper.loadedEvents
                .FirstOrDefault(ev => ev.DefName == ConsoleManager.commandParameters[0]);
            if (toFind == null)
            {
                Printer.Warning($"Event '{ConsoleManager.commandParameters[0]}' was not found");
                return;
            }

            foreach (var client in NetworkHelper.GetConnectedClientsSafe())
            {
                var eventData = new EventData
                {
                    _stepMode = EventStepMode.Receive,
                    _eventFile = toFind,
                    _toTile = -1
                };
                client.Listener.EnqueuePacket(PacketHeader.EventManager, eventData);
            }

            Printer.Title($"Sent event '{ConsoleManager.commandParameters[0]}' to every connected player");
        }

        public static void EventListCommandAction()
        {
            Printer.Title($"Available events: [{EventManagerHelper.loadedEvents.Length}]");
            Printer.Title("----------------------------------------");
            foreach (var ev in EventManagerHelper.loadedEvents)
                Printer.Warning(ev.DefName);
            Printer.Title("----------------------------------------");
        }

        public static void BroadcastCommandAction()
        {
            string fullText = string.Join(' ', ConsoleManager.commandParameters);
            var commandData = new CommandData
            {
                _commandMode = CommandMode.Broadcast,
                _details = fullText
            };
            NetworkHelper.SendPacketToAllClients(PacketHeader.ConsoleManager, commandData);
            Printer.Title($"Sent broadcast: '{fullText}'");
        }

        public static void ServerMessageCommandAction()
        {
            string fullText = string.Join(' ', ConsoleManager.commandParameters);
            ChatManager.BroadcastConsoleMessage(fullText);
            Printer.Title($"Sent chat: '{fullText}'");
        }

        public static void WhitelistCommandAction()
        {
            Printer.Title($"Whitelisted usernames: [{Master.Whitelist.WhitelistedUsers.Count}]");
            Printer.Title("----------------------------------------");
            foreach (var user in Master.Whitelist.WhitelistedUsers)
                Printer.Warning(user);
            Printer.Title("----------------------------------------");
        }

        public static void WhitelistAddCommandAction()
        {
            var userFile = UserManagerH.GetUserFileFromName(ConsoleManager.commandParameters[0]);
            if (userFile == null)
            {
                ThrowUserNotFoundError();
                return;
            }
            if (Master.Whitelist.WhitelistedUsers.Contains(userFile.Uid))
            {
                Printer.Warning($"User '{ConsoleManager.commandParameters[0]}' was already whitelisted");
                return;
            }
            WhitelistManager.AddUserToWhitelist(ConsoleManager.commandParameters[0]);
        }

        public static void WhitelistRemoveCommandAction()
        {
            var userFile = UserManagerH.GetUserFileFromName(ConsoleManager.commandParameters[0]);
            if (userFile == null)
            {
                ThrowUserNotFoundError();
                return;
            }
            if (!Master.Whitelist.WhitelistedUsers.Contains(userFile.Uid))
            {
                Printer.Warning($"User '{ConsoleManager.commandParameters[0]}' was not whitelisted");
                return;
            }
            WhitelistManager.RemoveUserFromWhitelist(ConsoleManager.commandParameters[0]);
        }

        public static void ForceSaveCommandAction()
        {
            var toFind = NetworkHelper.GetConnectedClientFromUid(ConsoleManager.commandParameters[0]);
            if (toFind == null)
            {
                ThrowUserNotFoundError();
                return;
            }

            var commandData = new CommandData
            {
                _commandMode = CommandMode.ForceSave
            };
            toFind.Listener.EnqueuePacket(PacketHeader.ConsoleManager, commandData);
            Printer.Warning($"User '{ConsoleManager.commandParameters[0]}' has been forced to save");
        }

        public static void ResetPlayerCommandAction()
        {
            var userFile = UserManagerH.GetUserFileFromName(ConsoleManager.commandParameters[0]);
            if (userFile == null)
            {
                ThrowUserNotFoundError();
                return;
            }
            var toFind = NetworkHelper.GetConnectedClientFromUid(userFile.Uid);
            SaveManager.ResetPlayerData(toFind, userFile.Uid);
        }

        public static void PortForwardCommandAction()
        {
            if (!Master.ServerConfig.UseUPnP)
            {
                Printer.Error("Cannot portforward because UPnP is disabled on the server");
                return;
            }
            _ = new UPnP();
        }

        public static void LeaderboardCommandAction()
        {
            // 1) Determine how many to print (default = 10)
            int limit = 10;
            if (ConsoleManager.commandParameters.Length > 0 &&
                int.TryParse(ConsoleManager.commandParameters[0], out int n) && n > 0)
            {
                limit = n;
            }

            // 2) Grab the raw text from WealthManager (this returns a multi-line string):
            //    e.g.:
            //      "Top 1 richest players:\n1. KNAPPE0 (KNAPPE0) – $19,807.62"
            string rawLb = WealthManager.FormatLeaderboard(limit);

            // 3) Split into individual lines
            string[] lines = rawLb.Split(new[] { '\n' }, StringSplitOptions.None);

            // 4) Start printing exactly as 'deeplist' does.
            //    First, the header (lines[0]) is printed as-is:
            //      [hh:mm:ss] | Top 1 richest players:
            if (lines.Length > 0)
            {
                Printer.Title(lines[0]);
            }

            // 5) Print the “bar” line under it
            Printer.Title("----------------------------------------");

            // 6) Prepare a regex to match "1. USERNAME (OLD-UID) – $WEALTH"
            var regex = new Regex(
                @"^(\s*\d+\.\s+)([^\s]+)\s+\([^\)]+\)\s+–\s+(.*)$",
                RegexOptions.Compiled
            );

            // 7) For each subsequent line (rank lines), re‐insert the **correct** UID:
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                var m = regex.Match(line);

                if (m.Success)
                {
                    // a) capture rank prefix, username, and wealth
                    string prefix = m.Groups[1].Value;   // e.g. "1. "
                    string uname  = m.Groups[2].Value;   // e.g. "KNAPPE0"
                    string wealth = m.Groups[3].Value;   // e.g. "$19,807.62"

                    // b) Look up the actual UID (case‐insensitive) in all user‐files
                    var userFile = UserManagerH
                        .GetAllUserFiles()
                        .FirstOrDefault(u =>
                            string.Equals(u.Label, uname, StringComparison.OrdinalIgnoreCase));

                    string correctUid = (userFile != null)
                        ? userFile.Uid
                        : "<unknown-uid>";

                    // c) Rebuild: "1. KNAPPE0 (CORRECT-UID) – $19,807.62"
                    string rebuiltLine = $"{prefix}{uname} ({correctUid}) – {wealth}";

                    // d) Print that reconstructed line, with timestamp:
                    Printer.Title(rebuiltLine);
                }
                else
                {
                    // If it doesn’t match our pattern, just print verbatim:
                    Printer.Title(line);
                }
            }

            // 8) Finally, print the closing “bar” line:
            Printer.Title("----------------------------------------");
        }    

        public static void ResetWorldCommandAction()
        {
            Printer.Warning("Are you sure you want to reset the world?");
            Printer.Warning("Please type 'YES' or 'NO'");

        DeleteWorldQuestion:
            string response = Console.ReadLine();
            if (response == "NO") return;
            if (response != "YES")
            {
                Printer.Error($"{response} is not a valid option. The answer must be capitalized");
                goto DeleteWorldQuestion;
            }

            BackupManager.BackupServer();
            Directory.Delete(Master.AssetsPath, true);
            Directory.Delete(Master.ConfigsPath, true);
            Directory.Delete(Master.TempPath, true);
            Environment.Exit(0);
        }

        public static void QuitCommandAction()
        {
            Master.IsClosing = true;
            Printer.Warning("Waiting for all saves to quit");

            foreach (var client in NetworkHelper.GetConnectedClientsSafe())
            {
                var commandData = new CommandData
                {
                    _commandMode = CommandMode.ForceSave
                };
                client.Listener.EnqueuePacket(PacketHeader.ConsoleManager, commandData);
            }

            while (NetworkHelper.GetConnectedClientsSafe().Length > 0)
                Thread.Sleep(1);

            Environment.Exit(0);
        }

        public static void ForceQuitCommandAction()
        {
            Environment.Exit(0);
        }

        public static void ClearCommandAction()
        {
            Console.Clear();
            Printer.Title("[Cleared console]");
        }

        public static void ThrowUserNotFoundError()
        {
            Printer.Warning($"User '{ConsoleManager.commandParameters[0]}' was not found");
            var allUsers = UserManagerH.GetAllUserFiles();
            if (allUsers.Any(u => u.Label == ConsoleManager.commandParameters[0]))
            {
                Printer.Warning($"Username detected. You can only use UIDs for user commands. " +
                    $"Use the command `deeplist` to get the UID of {ConsoleManager.commandParameters[0]}.");
            }

            var usersWithMatchingUsername = allUsers
                .Where(u => u.Label == ConsoleManager.commandParameters[0])
                .ToArray();
            if (usersWithMatchingUsername.Length == 1)
            {
                Printer.Warning($"Since only one person with the username {ConsoleManager.commandParameters[0]} exists, " +
                    $"we were able to fetch their UID automatically: {usersWithMatchingUsername.First().Uid}");
            }
        }
    }
}