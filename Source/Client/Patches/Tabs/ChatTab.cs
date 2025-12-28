using RimWorld;
using UnityEngine;
using Verse;
using System.Collections.Generic;
using System.Linq;
using GameClient.Managers;

namespace GameClient.Patches.Tabs
{
    public class ChatTab : MainTabWindow
    {
        public override Vector2 RequestedTabSize => new Vector2(800f, 600f);

        private Vector2 scrollPositionPlayers = Vector2.zero;
        private Vector2 scrollPositionChat = Vector2.zero;

        private enum ViewMode : byte { Chat }
        private ViewMode _viewMode = ViewMode.Chat;

        private const string ChatInputControlName = "RWT_ChatInput";

        public ChatTab()
        {
            layer = WindowLayer.GameUI;

            forcePause = false;
            draggable = true;
            focusWhenOpened = false;
            drawShadow = false;
            closeOnAccept = false;
            closeOnCancel = true;
            preventCameraMotion = false;
            drawInScreenshotMode = false;

            soundAppear = SoundDefOf.CommsWindow_Open;
        }

        public override void PreOpen()
        {
            base.PreOpen();

            windowRect.x = ChatManager.ChatBoxPosition.x;
            windowRect.y = ChatManager.ChatBoxPosition.y;
        }

        public override void PostOpen()
        {
            base.PostOpen();

            ChatManager.IsChatTabOpen = true;
            ChatManager.ToggleChatIcon(false);

            // Optional: auto-focus input on open
            // GUI.FocusControl(ChatInputControlName);
        }

        public override void PostClose()
        {
            base.PostClose();

            ChatManager.IsChatTabOpen = false;
        }

        public override void DoWindowContents(Rect rect)
        {
            // IMPORTANT: capture Enter BEFORE TextField consumes it
            bool enterPressed =
                Event.current != null &&
                (Event.current.type == EventType.KeyDown || Event.current.rawType == EventType.KeyDown) &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);

            bool inputFocused =
                GUI.GetNameOfFocusedControl() == ChatInputControlName;

            ChatManager.ChatBoxPosition.x = windowRect.x;
            ChatManager.ChatBoxPosition.y = windowRect.y;

            DrawTopBar(rect);

            Widgets.DrawLineHorizontal(rect.x, rect.y + 25f, rect.width);
            Widgets.DrawLineVertical(rect.x + 160f, rect.y + 25f, rect.height);

            DrawPlayerCount(rect);
            DrawPlayerList(new Rect(rect.x, rect.y + 25f, 160f, rect.height - 50f));
            DrawMessageList(new Rect(rect.x + 160f, rect.y + 32f, rect.width - 160f, rect.height - 60f));

            DrawPinCheckbox(rect);
            DrawInput(rect);

            // Send on Enter ONLY when the input is focused
            if (enterPressed && inputFocused)
            {
                TrySendChatInput();
                Event.current.Use();
            }

            if (ChatManager.ShouldScrollChat && ChatManager.ChatAutoscroll)
                ScrollToLastMessage();
        }

        private void DrawTopBar(Rect rect)
        {
            Rect right = new Rect(rect.x + 160f, rect.y, rect.width - 160f, 25f);

            float btnW = 110f;
            Rect chatBtn = new Rect(right.x + 6f, right.y + 2f, btnW, 21f);
            Rect lbBtn = new Rect(chatBtn.xMax + 6f, right.y + 2f, btnW, 21f);

            if (Widgets.ButtonText(chatBtn, "Chat"))
                _viewMode = ViewMode.Chat;

            if (Widgets.ButtonText(lbBtn, "Leaderboard"))
                LeaderboardManager.OpenLeaderboardDialog(requestFresh: true);
        }

        private void DrawPlayerCount(Rect rect)
        {
            string toShow = RecountManager.CurrentPlayers > 1
                ? $"{RecountManager.CurrentPlayers} Players Online"
                : $"{RecountManager.CurrentPlayers} Player Online";

            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x, rect.y, Text.CalcSize(toShow).x, Text.CalcSize(toShow).y),
                $"<color=grey>{toShow}</color>");
        }

        private void DrawPlayerList(Rect mainRect)
        {
            List<string> orderedList = RecountManager.CurrentPlayerNames?.ToList() ?? new List<string>();
            orderedList.Sort();

            float rowH = 25f;
            float height = 6f + orderedList.Count * rowH;

            Rect viewRect = new Rect(0f, 0f, mainRect.width - 16f, height);

            Widgets.BeginScrollView(mainRect, ref scrollPositionPlayers, viewRect);

            float y = 0f;
            for (int i = 0; i < orderedList.Count; i++)
            {
                string str = orderedList[i];

                Rect row = new Rect(0f, y, viewRect.width, rowH);
                DrawCustomRowPlayerList(row, str);

                y += rowH;
            }

            Widgets.EndScrollView();
        }

        private void DrawMessageList(Rect mainRect)
        {
            float chatScrollbarSafezone = 30f;
            float contentW = mainRect.width - chatScrollbarSafezone;

            float height = 6f;
            foreach (string str in ChatManager.ChatMessageCache.ToArray())
                height += Text.CalcHeight(str, contentW);

            Rect viewRect = new Rect(0f, 0f, contentW, height);

            Widgets.BeginScrollView(mainRect, ref scrollPositionChat, viewRect);

            float y = 0f;
            foreach (string str in ChatManager.ChatMessageCache.ToArray())
            {
                float h = Text.CalcHeight(str, contentW);
                Rect row = new Rect(0f, y, viewRect.width, h);
                DrawCustomRow(row, str);
                y += h;
            }

            Widgets.EndScrollView();
        }

        private void DrawInput(Rect rect)
        {
            Text.Font = GameFont.Small;

            Rect inputRect = new Rect(rect.xMin + 165f, rect.yMax - 25f, rect.width - 165f, 25f);

            GUI.SetNextControlName(ChatInputControlName);
            string inputOne = Widgets.TextField(inputRect, ChatManager.CurrentChatInput);

            if (inputOne.Length <= 512)
                ChatManager.CurrentChatInput = inputOne;
        }

        private void DrawPinCheckbox(Rect rect)
        {
            string pinText = "Auto Scroll";

            Text.Font = GameFont.Small;
            Widgets.CheckboxLabeled(
                new Rect(rect.xMax - Text.CalcSize(pinText).x * 1.5f, rect.y,
                    Text.CalcSize(pinText).x * 2, Text.CalcSize(pinText).y),
                pinText,
                ref ChatManager.ChatAutoscroll,
                placeCheckboxNearText: true);
        }

        private void TrySendChatInput()
        {
            if (string.IsNullOrWhiteSpace(ChatManager.CurrentChatInput))
                return;

            ChatManager.SendMessage(ChatManager.CurrentChatInput);
            ChatManager.CurrentChatInput = "";
        }

        private void ScrollToLastMessage()
        {
            scrollPositionChat.y = float.MaxValue;
            ChatManager.ShouldScrollChat = false;
        }

        private void DrawCustomRow(Rect rect, string message)
        {
            Text.Font = GameFont.Small;
            Rect fixedRect = new Rect(rect.x + 10f, rect.y + 5f, rect.width, rect.height);
            Widgets.Label(fixedRect, message);
        }

        private void DrawCustomRowPlayerList(Rect rect, string str)
        {
            Text.Font = GameFont.Small;

            Rect fixedRect = new Rect(rect.x + 10f, rect.y + 5f, rect.width - 10f, rect.height);
            Widgets.Label(fixedRect, str);

            if (Widgets.ButtonInvisible(fixedRect, false))
                ChatManager.CurrentChatInput += $"@{str}";

            Widgets.DrawHighlightIfMouseover(fixedRect);
        }
    }
}
