using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using Shared;
using UnityEngine;
using Verse;
using Verse.Sound;
using static Shared.CommonEnumerators;

namespace GameClient
{
    [StaticConstructorOnStartup]
    public static class ChatManager
    {
        public static Vector2 chatBoxPosition = new Vector2(0, UI.screenHeight - 35f - 600f);
        private static readonly MainButtonDef chatButtonDef = DefDatabase<MainButtonDef>.GetNamed("Chat");

        //Data
        public static string currentChatInput = "";
        public static List<string> chatMessageCache = new List<string>();

        //Booleans
        public static bool isChatTabOpen;
        public static bool isChatIconActive;
        public static bool shouldScrollChat;
        public static bool chatAutoscroll = true;

        //Chat clock
        private static Task? chatClockTask;
        private static readonly Semaphore semaphore = new Semaphore(1, 1);

        //Icons
        public static int chatIconIndex;
        public static List<Texture2D> chatIcons = new List<Texture2D>();

        public static void ParsePacket(Packet packet)
        {
            ChatData chatData = Serializer.ConvertBytesToObject<ChatData>(packet.Contents);

            bool hasBeenTagged = false;
            if (ChatManagerHelper.GetMessageWords(chatData.Message).Contains($"@{ClientValues.Username}"))
            {
                hasBeenTagged = true;
                chatData.Message = chatData.Message.Replace($"@{ClientValues.Username}", $"<color=red>@{ClientValues.Username}</color>");
            }

            AddMessageToChat(chatData.Username, chatData.Message, chatData.UserColor, chatData.MessageColor);

            if (!ClientValues.isReadyToPlay) return;

            if (!isChatTabOpen) ToggleChatIcon(true);

            if (ClientValues.muteSoundBool) return;

            if (hasBeenTagged) ChatSounds.SystemChatDing.PlayOneShotOnCamera();
        }

        public static void SendMessage(string MessageToSend)
        {
            ChatSounds.OwnChatDing.PlayOneShotOnCamera();
    
            ChatData chatData = new ChatData();
            chatData.Username = ClientValues.Username;
            chatData.Message = MessageToSend;

            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.ChatPacket), chatData);
            Network.Listener?.EnqueuePacket(packet);
        }

        private static string ParseMessage(string Message)
        {
            bool verifying = false;
            string verification = "";
            Stack<string> codeType = new Stack<string>();

            Message = Regex.Replace(Message, @"\*\*\*(.+?)\*\*\*", "[b][i]$1[/][/]");
            Message = Regex.Replace(Message, @"\*\*(.+?)\*\*", "[b]$1[/]");
            Message = Regex.Replace(Message, @"\*(.+?)\*", "[i]$1[/]");
            Message = Regex.Replace(Message, @"\&([a-fA-F0-9]{6})(.+?)\&\&", "[$1]$2[/]");

            foreach (char c in Message)
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
                            if (codeType.Count > 0) Message = Message.ReplaceFirst(verification, $"</{codeType.Pop()}>");
                            verification = "";
                            break;

                        //Check for BOLD

                        case "[b]":
                            Message = Message.Replace(verification, "<b>");
                            codeType.Push("b");
                            verification = "";
                            break;

                        //Check for CURSIVE

                        case "[i]":
                            Message = Message.Replace(verification, "<i>");
                            codeType.Push("i");
                            verification = "";
                            break;

                        //Check for CUSTOM COLOR

                        default:
                            if (Regex.IsMatch(verification, @"\[[a-fA-F0-9]{6}\]"))
                            {
                                string verificationReplacement = verification.Replace("[", "<color=#").Replace("]", ">");
                                Message = Message.Replace(verification, verificationReplacement);
                                codeType.Push("color");
                                verification = "";
                            }
                            break;
                    }
                }
            }

            while (codeType.Count > 0) Message += $"</{codeType.Pop()}>";

            return Message;
        }

        public static void AddMessageToChat(string Username, string Message, UserColor userColor, MessageColor MessageColor)
        {
            if (chatMessageCache.Count() > 100) chatMessageCache.RemoveAt(0);

            chatMessageCache.Add($"<color=grey>{DateTime.Now.ToString("HH:mm")}</color> " + $"{ChatManagerHelper.userColorDictionary[userColor]}{Username}</color>: " +
                $"{ChatManagerHelper.MessageColorDictionary[MessageColor]}{ParseMessage(Message)}</color>");

            if (chatAutoscroll) ClientValues.ToggleChatScroll(true);
        }

        public static void CleanChat()
        {
            currentChatInput = "";
            chatMessageCache = new List<string>();

            isChatTabOpen = false;
            isChatIconActive = false;
            chatAutoscroll = true;
        }

        public static void ToggleChatIcon(bool mode)
        {
            if (!ClientValues.isReadyToPlay) return;

            isChatIconActive = mode;

            if (mode)
            {
                semaphore.WaitOne();

                chatClockTask ??= Threader.GenerateThread(Threader.Mode.Chat);

                semaphore.Release();
            }
        }

        public static void UpdateChatIcon()
        {
            chatIconIndex++;
            if(chatIconIndex > chatIcons.Count) chatIconIndex = 0;
            AccessTools.Field(typeof(MainButtonDef), "icon").SetValue(chatButtonDef, chatIcons[chatIconIndex]);
        }

        private static void TurnOffChatIcon() { AccessTools.Field(typeof(MainButtonDef), "icon").SetValue(chatButtonDef, chatIcons[0]); }

        public static void ChatClock()
        {
            while(isChatIconActive)
            {
                Master.threadDispatcher.Enqueue(UpdateChatIcon);

                Thread.Sleep(250);
            }

            chatIconIndex = 0;

            Master.threadDispatcher.Enqueue(TurnOffChatIcon);

            chatClockTask = null;
        }
    }

    public static class ChatManagerHelper
    {
        public static Dictionary<UserColor, string> userColorDictionary = new Dictionary<UserColor, string>()
        {
            { UserColor.Normal, "<color=white>" },
            { UserColor.Admin, "<color=red>" },
            { UserColor.Console, "<color=yellow>" },
            { UserColor.Private, "<color=#3ae0dd>" },
            { UserColor.Discord, "<color=#9656ce>" }
        };

        public static Dictionary<MessageColor, string> MessageColorDictionary = new Dictionary<MessageColor, string>()
        {
            { MessageColor.Normal, "<color=white>" },
            { MessageColor.Admin, "<color=white>" },
            { MessageColor.Console, "<color=yellow>" },
            { MessageColor.Private, "<color=#3ae0dd>" },
            { MessageColor.Discord, "<color=white>" }
        };

        public static string[] GetMessageWords(string Message)
        {
            return Message.Split(' ');
        }
    }

    [StaticConstructorOnStartup]
    public static class ChatIcons
    {
        static ChatIcons()
        {
            ChatManager.chatIcons.Add(ContentFinder<Texture2D>.Get("UI/ChatIconOff"));
            ChatManager.chatIcons.Add(ContentFinder<Texture2D>.Get("UI/ChatIconOn"));
            ChatManager.chatIcons.Add(ContentFinder<Texture2D>.Get("UI/ChatIconMid"));
            ChatManager.chatIcons.Add(ContentFinder<Texture2D>.Get("UI/ChatIconOff"));
        }
    }

    //TODO
    //Apply different sounds depending on the Message type, since right now only "Own" and "System" play

    [DefOf]
    public static class ChatSounds
    {
        public static SoundDef? OwnChatDing;
        public static SoundDef? AllyChatDing;
        public static SoundDef? NeutralChatDing;
        public static SoundDef? HostileChatDing;
        public static SoundDef? SystemChatDing;
    }
}
