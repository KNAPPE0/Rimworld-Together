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

        private const string ChatInputControlName = "RWT_ChatInput";

        private const float LeftPanelWidth = 160f;
        private const float TopBarHeight = 25f;
        private const float InputHeight = 25f;

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
        }

        public override void PostClose()
        {
            base.PostClose();

            ChatManager.IsChatTabOpen = false;
        }

        public override void DoWindowContents(Rect rect)
        {
            bool enterPressed =
                Event.current != null &&
                (Event.current.type == EventType.KeyDown || Event.current.rawType == EventType.KeyDown) &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);

            bool inputFocused = GUI.GetNameOfFocusedControl() == ChatInputControlName;

            ChatManager.ChatBoxPosition.x = windowRect.x;
            ChatManager.ChatBoxPosition.y = windowRect.y;

            DrawTopBar(rect);

            Widgets.DrawLineHorizontal(rect.x, rect.y + TopBarHeight, rect.width);
            Widgets.DrawLineVertical(rect.x + LeftPanelWidth, rect.y + TopBarHeight, rect.height);

            DrawPlayerCount(rect);
            DrawPlayerList(new Rect(rect.x, rect.y + TopBarHeight, LeftPanelWidth, rect.height - (TopBarHeight + InputHeight)));
            DrawMessageList(new Rect(rect.x + LeftPanelWidth, rect.y + TopBarHeight + 7f, rect.width - LeftPanelWidth, rect.height - (TopBarHeight + InputHeight + 10f)));

            DrawPinCheckbox(rect);
            DrawInput(rect);

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
            Rect right = new Rect(rect.x + LeftPanelWidth, rect.y, rect.width - LeftPanelWidth, TopBarHeight);

            float btnW = 110f;
            Rect chatBtn = new Rect(right.x + 6f, right.y + 2f, btnW, 21f);
            Rect lbBtn = new Rect(chatBtn.xMax + 6f, right.y + 2f, btnW, 21f);

            if (Widgets.ButtonText(chatBtn, "Chat"))
                GUI.FocusControl(ChatInputControlName);

            if (Widgets.ButtonText(lbBtn, "Leaderboard"))
                LeaderboardManager.OpenLeaderboardDialog(requestFresh: true);
        }

        private void DrawPlayerCount(Rect rect)
        {
            string toShow = RecountManager.CurrentPlayers == 1
                ? "1 Player Online"
                : $"{RecountManager.CurrentPlayers} Players Online";

            Text.Font = GameFont.Small;

            Vector2 size = Text.CalcSize(toShow);
            Widgets.Label(new Rect(rect.x, rect.y, size.x, size.y), $"<color=grey>{toShow}</color>");
        }

        private void DrawPlayerList(Rect mainRect)
        {
            List<string> orderedList = RecountManager.CurrentPlayerNames?.ToList() ?? new List<string>();
            orderedList.Sort();

            float rowH = 25f;
            float height = 6f + orderedList.Count * rowH;

            Rect viewRect = new Rect(0f, 0f, mainRect.width - 16f, height);

            Widgets.BeginScrollView(mainRect, ref scrollPositionPlayers, viewRect);
            try
            {
                float y = 0f;
                for (int i = 0; i < orderedList.Count; i++)
                {
                    Rect row = new Rect(0f, y, viewRect.width, rowH);
                    DrawCustomRowPlayerList(row, orderedList[i]);
                    y += rowH;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private void DrawMessageList(Rect mainRect)
        {
            float chatScrollbarSafezone = 30f;
            float contentW = mainRect.width - chatScrollbarSafezone;

            string[] messages = ChatManager.ChatMessageCache.ToArray();

            float height = 6f;
            for (int i = 0; i < messages.Length; i++)
                height += Text.CalcHeight(messages[i], contentW);

            Rect viewRect = new Rect(0f, 0f, contentW, height);

            Widgets.BeginScrollView(mainRect, ref scrollPositionChat, viewRect);
            try
            {
                float y = 0f;
                for (int i = 0; i < messages.Length; i++)
                {
                    string str = messages[i];
                    float h = Text.CalcHeight(str, contentW);
                    Rect row = new Rect(0f, y, viewRect.width, h);
                    DrawCustomRow(row, str);
                    y += h;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private void DrawInput(Rect rect)
        {
            Text.Font = GameFont.Small;

            Rect inputRect = new Rect(rect.xMin + (LeftPanelWidth + 5f), rect.yMax - InputHeight, rect.width - (LeftPanelWidth + 5f), InputHeight);

            GUI.SetNextControlName(ChatInputControlName);
            string inputOne = Widgets.TextField(inputRect, ChatManager.CurrentChatInput);

            if (inputOne.Length <= 512)
                ChatManager.CurrentChatInput = inputOne;
        }

        private void DrawPinCheckbox(Rect rect)
        {
            const string pinText = "Auto Scroll";

            Text.Font = GameFont.Small;
            Vector2 size = Text.CalcSize(pinText);

            Widgets.CheckboxLabeled(
                new Rect(rect.xMax - size.x * 1.5f, rect.y, size.x * 2f, size.y),
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