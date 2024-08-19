using Shared;
using System.Text;
using static Shared.CommonEnumerators;

namespace GameServer
{
    public static class ChatManager
    {
        // Semaphores for thread safety
        private static readonly Semaphore logSemaphore = new Semaphore(1, 1);
        private static readonly Semaphore commandSemaphore = new Semaphore(1, 1);
        
        private static readonly string systemName = "CONSOLE";

        // Default messages and text tools for the chat
        public static readonly string[] DefaultJoinMessages = new string[]
        {
            "Welcome to the global chat!",
            "Please be considerate with others and have fun!",
            "Use '/help' to check all the available commands."
        };

        public static readonly string[] DefaultTextTools = new string[]
        {
            "List of available text tools:",
            "'b' inside brackets - Followed by the text you want to turn [b]bold",
            "'i' inside brackets - Followed by the text you want to turn [i]cursive",
            "HTML color inside brackets - Followed by the text you want to [ff0000]change color"
        };

        // Parse the incoming packet and handle it accordingly
        public static void ParsePacket(ServerClient client, Packet packet)
        {
            var chatData = Serializer.ConvertBytesToObject<ChatData>(packet.Contents);
            if (chatData == null)
            {
                Logger.Warning("[ChatManager] > Failed to deserialize chat data.");
                return;
            }

            if (chatData.Message.StartsWith("/"))
                ExecuteChatCommand(client, chatData.Message.Split(' '));
            else
                BroadcastChatMessage(client, chatData.Message);
        }

        // Execute the chat command received
        private static void ExecuteChatCommand(ServerClient client, string[] command)
        {
            commandSemaphore.WaitOne();

            var chatCommand = ChatManagerHelper.GetCommandFromName(command[0]);
            if (chatCommand == null)
            {
                SendSystemMessage(client, "Command not found.");
            }
            else
            {
                ChatCommandManager.TargetClient = client;
                ChatCommandManager.Command = command;
                chatCommand.CommandAction.Invoke();
            }

            var chatCommandString = string.Join(" ", command);
            ChatManagerHelper.ShowChatInConsole(client.userFile.Username, chatCommandString);

            commandSemaphore.Release();
        }

        // Broadcast a chat message to all connected clients
        private static void BroadcastChatMessage(ServerClient client, string message)
        {
            if (Master.serverConfig == null) return;

            ChatData chatData = new ChatData();
            chatData.Username = client.userFile.Username;
            chatData.Message = message;
            chatData.UserColor = client.userFile.IsAdmin ? UserColor.Admin : UserColor.Normal;
            chatData.MessageColor = client.userFile.IsAdmin ? MessageColor.Admin : MessageColor.Normal;

            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.ChatPacket), chatData);
            foreach (ServerClient cClient in Network.connectedClients.ToArray()) cClient.Listener.EnqueuePacket(packet);

            WriteToLogs(client.userFile.Username, message);
            ChatManagerHelper.ShowChatInConsole(client.userFile.Username, message);

            if (Master.discordConfig.Enabled && Master.discordConfig.ChatChannelId != 0) DiscordManager.SendMessageToChatChannel(chatData.Username, message);
        }

        public static void BroadcastDiscordMessage(string client, string message)
        {
            ChatData chatData = new ChatData();
            chatData.Username = client;
            chatData.Message = message;
            chatData.UserColor = UserColor.Discord;
            chatData.MessageColor = MessageColor.Discord;

            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.ChatPacket), chatData);
            foreach (ServerClient cClient in Network.connectedClients.ToArray()) cClient.Listener.EnqueuePacket(packet);

            WriteToLogs(client, message);
            ChatManagerHelper.ShowChatInConsole(client, message, true);
        }

        // Broadcast a message from the server console
        public static void BroadcastServerMessage(string message)
        {
            ChatData chatData = new ChatData();
            chatData.Username = systemName;
            chatData.Message = message;
            chatData.UserColor = UserColor.Console;
            chatData.MessageColor = MessageColor.Console;

            var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.ChatPacket), chatData);
            foreach (var client in Network.connectedClients.ToArray())
                client.Listener.EnqueuePacket(packet);

            if (Master.discordConfig.Enabled && Master.discordConfig.ChatChannelId != 0) DiscordManager.SendMessageToChatChannel(chatData.Username, message);

            WriteToLogs(chatData.Username, message);
            ChatManagerHelper.ShowChatInConsole(chatData.Username, message);
        }

        // Send a system message to a specific client
        public static void SendSystemMessage(ServerClient client, string message)
        {
            ChatData chatData = new ChatData();
            chatData.Username = systemName;
            chatData.Message = message;
            chatData.UserColor = UserColor.Console;
            chatData.MessageColor = MessageColor.Console;

            var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.ChatPacket), chatData);
            client.Listener.EnqueuePacket(packet);
        }

        // Log chat messages to a file
        private static void WriteToLogs(string Username, string message)
        {
            logSemaphore.WaitOne();

            var stringBuilder = new StringBuilder();
            stringBuilder.Append($"[{DateTime.Now:HH:mm:ss}] | [{Username}]: {message}");
            stringBuilder.Append(Environment.NewLine);

            var dateTime = DateTime.Now.Date;
            var nowFileName = $"{dateTime.Year}-{dateTime.Month:D2}-{dateTime.Day:D2}.txt";
            var nowFullPath = Path.Combine(Master.chatLogsPath, nowFileName);

            File.AppendAllText(nowFullPath, stringBuilder.ToString());

            logSemaphore.Release();
        }
    }

    public static class ChatCommandManager
    {
        public static ServerClient? TargetClient { get; set; }
        public static string[]? Command { get; set; }

        private static readonly ChatCommand HelpCommand = new ChatCommand("/help", 0,
            "Shows a list of all available commands",
            ChatHelpCommandAction);

        private static readonly ChatCommand ToolsCommand = new ChatCommand("/tools", 0,
            "Shows a list of all available chat tools",
            ChatToolsCommandAction);

        private static readonly ChatCommand PingCommand = new ChatCommand("/ping", 0,
            "Checks if the connection to the server is working",
            ChatPingCommandAction);

        private static readonly ChatCommand DisconnectCommand = new ChatCommand("/dc", 0,
            "Forcefully disconnects you from the server",
            ChatDisconnectCommandAction);

        private static readonly ChatCommand StopOnlineActivityCommand = new ChatCommand("/sv", 0,
            "Forcefully disconnects you from a visit",
            ChatStopOnlineActivityCommandAction);

        private static readonly ChatCommand PrivateMessageCommand = new ChatCommand("/w", 0,
            "Sends a private message to a specific user",
            ChatPrivateMessageCommandAction);

        public static readonly ChatCommand[] ChatCommands = new ChatCommand[]
        {
            HelpCommand,
            ToolsCommand,
            PingCommand,
            DisconnectCommand,
            StopOnlineActivityCommand,
            PrivateMessageCommand
        };

        private static void ChatHelpCommandAction()
        {
            if (TargetClient == null) return;

            var messagesToSend = new List<string> { "List of available commands:" };
            foreach (var command in ChatCommands)
                messagesToSend.Add($"{command.Prefix} - {command.Description}");

            foreach (var str in messagesToSend)
                ChatManager.SendSystemMessage(TargetClient, str);
        }

        private static void ChatToolsCommandAction()
        {
            if (TargetClient == null) return;

            foreach (var str in ChatManager.DefaultTextTools)
                ChatManager.SendSystemMessage(TargetClient, str);
        }

        private static void ChatPingCommandAction()
        {
            if (TargetClient == null) return;
            ChatManager.SendSystemMessage(TargetClient, "Pong!");
        }

        private static void ChatDisconnectCommandAction()
        {
            if (TargetClient == null) return;
            TargetClient.Listener.disconnectFlag = true;
        }

        private static void ChatStopOnlineActivityCommandAction()
        {
            if (TargetClient == null) return;
            OnlineActivityManager.SendVisitStop(TargetClient);
        }

        private static void ChatPrivateMessageCommandAction()
        {
            if (TargetClient == null) return;

            var message = string.Join(" ", Command.Skip(2));
            if (string.IsNullOrWhiteSpace(message))
            {
                string Message = "";
                for (int i = 2; i < Command.Length; i++) message += Command[i] + " ";

                if (string.IsNullOrWhiteSpace(message)) ChatManager.SendSystemMessage(TargetClient, "Message was empty.");
                else
                {
                    ServerClient toFind = ChatManagerHelper.GetUserFromName(ChatManagerHelper.GetUsernameFromMention(Command[1]));
                    if (toFind == null) ChatManager.SendSystemMessage(TargetClient, "User was not found.");
                    else
                    {
                        //Don't allow players to send wispers to themselves
                        if (toFind == TargetClient) ChatManager.SendSystemMessage(TargetClient, "Can't send a whisper to yourself.");
                        else
                        {
                            ChatData chatData1 = new ChatData();
                            chatData1.Message = message;
                            chatData1.UserColor = UserColor.Private;
                            chatData1.MessageColor = MessageColor.Private;

                            //Send to sender
                            chatData1.Username = $">> {toFind.userFile.Username}";
                            Packet Packet = Packet.CreatePacketFromObject(nameof(PacketHandler.ChatPacket), chatData1);
                            TargetClient.Listener.EnqueuePacket(Packet);

                            //Send to recipient
                            chatData1.Username = $"<< {TargetClient.userFile.Username}";
                            Packet = Packet.CreatePacketFromObject(nameof(PacketHandler.ChatPacket), chatData1);
                            toFind.Listener.EnqueuePacket(Packet);

                            ChatManagerHelper.ShowChatInConsole(chatData1.Username, message);
                        }
                    }
                }
            }

            var recipient = ChatManagerHelper.GetUserFromName(ChatManagerHelper.GetUsernameFromMention(Command[1]));
            if (recipient == null)
            {
                ChatManager.SendSystemMessage(TargetClient, "User was not found.");
                return;
            }

            if (recipient == TargetClient)
            {
                ChatManager.SendSystemMessage(TargetClient, "Can't send a whisper to yourself.");
                return;
            }

            var chatData = new ChatData
            {
                Message = message,
                UserColor = UserColor.Private,
                MessageColor = MessageColor.Private
            };

            // Send to sender
            chatData.Username = $">> {recipient.userFile.Username}";
            var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.ChatPacket), chatData);
            TargetClient.Listener.EnqueuePacket(packet);

            // Send to recipient
            chatData.Username = $"<< {TargetClient.userFile.Username}";
            packet = Packet.CreatePacketFromObject(nameof(PacketHandler.ChatPacket), chatData);
            recipient.Listener.EnqueuePacket(packet);

            ChatManagerHelper.ShowChatInConsole(chatData.Username, message);
        }
    }

    public static class ChatManagerHelper
    {
        public static ServerClient GetUserFromName(string Username)
        {
            return Network.connectedClients.ToArray()
                .FirstOrDefault(client => client.userFile.Username.Equals(Username, StringComparison.OrdinalIgnoreCase));
        }

        public static ChatCommand GetCommandFromName(string commandName)
        {
            return ChatCommandManager.ChatCommands.ToArray()
                .FirstOrDefault(cmd => cmd.Prefix.Equals(commandName, StringComparison.OrdinalIgnoreCase));
        }

        public static string GetUsernameFromMention(string mention)
        {
            return mention.Replace("@", "").Trim();
        }

        public static void ShowChatInConsole(string username, string message, bool fromDiscord = false)
        {
            if (!Master.serverConfig.DisplayChatInConsole) return;
            else 
            {
                if (fromDiscord) Logger.Message($"[Discord] > {username} > {message}");
                else Logger.Message($"[Chat] > {username} > {message}");
            }
        }
    }
}