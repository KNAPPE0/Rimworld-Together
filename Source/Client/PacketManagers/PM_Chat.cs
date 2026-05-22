using GameClient.Defs;
using GameClient.Dialogs;
using GameClient.Misc;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;
using Verse;
using Verse.Sound;

namespace GameClient.PacketManagers
{
    [StaticConstructorOnStartup]
    public class PM_Chat : PM_Base
    {
        [HandlesPacket(PacketHeader.ChatManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_Chat data = Serializer.ConvertBytesToObject<PKT_Chat>(bytes);

            AddMessageToChat(data);
        }

        public static void SendMessage(string messageToSend)
        {
            RTSoundDefs.ChatSend.PlayOneShotOnCamera();

            PKT_Chat chatData = new PKT_Chat();
            chatData.Username = SessionHandler.Username;
            chatData.Message = messageToSend;
            chatData.IsCommand = messageToSend.StartsWith("/");

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.ChatManager, chatData);
        }

        public static void AddMessageToChat(PKT_Chat data)
        {
            if (DLG_Chat.ChatMessages.Count > 100) DLG_Chat.ChatMessages.RemoveAt(0);

            if (PM_Chat.CheckIfHasBeenTagged(data.Message))
            {
                data.Message = data.Message.Replace($"@{SessionHandler.Username}", $"<color=red>@{SessionHandler.Username}</color>");
            }

            string timeString = $"<color=grey>{DateTime.Now.ToString("HH:mm")}</color> ";
            string usernameString = $"{PKT_Chat.MessageColorDictionary[data.UsernameColor]}{data.Username}</color>: ";
            string textString = $"{PKT_Chat.MessageColorDictionary[data.MessageColor]}{PM_Chat.ParseMessage(data.Message, false)}</color>";
            DLG_Chat.ChatMessages.Add(string.Concat(timeString, usernameString, textString));

            if (!DLG_Chat.IsDialogOpen & !DLG_Chat.ShouldPlaySounds) RTSoundDefs.ChatReceive.PlayOneShotOnCamera();
        }

        public static string[] GetMessageWords(string message) { return message.Split(' '); }

        public static bool CheckIfHasBeenTagged(string message) { return GetMessageWords(message).Contains($"@{SessionHandler.Username}"); }

        public static string ParseMessage(string message, bool fromBroadcast)
        {
            bool verifying = false;
            string verification = "";
            Stack<string> codeType = new Stack<string>();

            message = Regex.Replace(message, @"\*\*\*(.+?)\*\*\*", "[b][i]$1[/][/]");
            message = Regex.Replace(message, @"\*\*(.+?)\*\*", "[b]$1[/]");
            message = Regex.Replace(message, @"\*(.+?)\*", "[i]$1[/]");
            message = Regex.Replace(message, @"\&([a-fA-F0-9]{6})(.+?)\&\&", "[$1]$2[/]");

            foreach (char c in message)
            {
                if (c == '[') verifying = true;

                if (verifying)
                {
                    verification += c;
                    if (c == ']') verifying = false;
                }

                if (verification != "" && !verifying)
                {
                    switch (verification.ToLower())
                    {
                        //Check for TAG CLOSING

                        case "[/]":
                            if (codeType.Count > 0) message = message.ReplaceFirst(verification, $"</{codeType.Pop()}>");
                            verification = "";
                            break;

                        //Check for BOLD

                        case "[b]":
                            message = message.Replace(verification, "<b>");
                            codeType.Push("b");
                            verification = "";
                            break;

                        //Check for CURSIVE

                        case "[i]":
                            message = message.Replace(verification, "<i>");
                            codeType.Push("i");
                            verification = "";
                            break;

                        //Check for NEW LINE (broadcasts only)

                        case "[n]":
                            if (fromBroadcast)
                            {
                                message = message.Replace(verification, "\n");
                                verification = "";
                            }
                            break;

                        //Check for CUSTOM COLOR

                        default:
                            if (Regex.IsMatch(verification, @"\[[a-fA-F0-9]{6}\]"))
                            {
                                string verificationReplacement = verification.Replace("[", "<color=#").Replace("]", ">");
                                message = message.Replace(verification, verificationReplacement);
                                codeType.Push("color");
                                verification = "";
                            }
                            break;
                    }
                }
            }

            while (codeType.Count > 0) message += $"</{codeType.Pop()}>";

            return message;
        }
    }
}
