using GameClient.Defs;
using GameClient.Misc;
using GameClient.PacketManagers;
using RimWorld;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace GameClient.Dialogs
{
    public class DLG_Chat : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(800f, 560f);

        private Vector2 _scrollPlayers = Vector2.zero;
        private Vector2 _scrollChat = Vector2.zero;
        private int _lastChatCount = -1;
        private bool _pendingFocusInput = true;
        private Vector2 _positionAtOpen = Vector2.zero;

        private static bool _defaultPositionCaptured = false;
        private static Vector2 _defaultPosition = Vector2.zero;
        private static Vector2 _lastClosedPosition = Vector2.zero;
        private static bool _hasLastClosedPosition = false;

        private const string ChatInputControlName = "RWT_ChatInput";
        private const float Pad = 8f;
        private const float Gap = 6f;
        private const float LeftPanelWidth = 170f;
        private const float TopBarHeight = 32f;
        private const float InputHeight = 30f;
        private const float PlayerRowH = 28f;
        private const float SendBtnW = 84f;

        public static DLG_Chat Instance { get; private set; } = null;
        public static bool IsDialogOpen { get; set; } = false;
        public static bool ShouldScrollChat { get; set; } = true;
        public static bool ShouldPlaySounds { get; set; } = false;
        public static bool ChatAutoscroll { get; set; } = true;
        public static string CurrentChatInput { get; set; } = string.Empty;
        public static List<string> ChatMessages { get; set; } = new List<string>();

        public DLG_Chat()
        {
            layer = WindowLayer.Super;
            Instance = this;
            draggable = true;
            forcePause = false;
            preventCameraMotion = false;
            absorbInputAroundWindow = false;
            closeOnAccept = false;
            closeOnCancel = true;
            soundAppear = SoundDefOf.CommsWindow_Open;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            // Default position: bottom-left area
            float defX = 10f;
            float defY = UI.screenHeight - InitialSize.y - 45f;

            if (!_defaultPositionCaptured)
            {
                _defaultPositionCaptured = true;
                _defaultPosition = new Vector2(defX, defY);
            }

            // Use last closed position if available, otherwise default
            if (_hasLastClosedPosition)
            {
                windowRect.x = Mathf.Clamp(_lastClosedPosition.x, 0f, UI.screenWidth - windowRect.width);
                windowRect.y = Mathf.Clamp(_lastClosedPosition.y, 0f, UI.screenHeight - windowRect.height);
            }
            else
            {
                windowRect.x = Mathf.Clamp(defX, 0f, UI.screenWidth - windowRect.width);
                windowRect.y = Mathf.Clamp(defY, 0f, UI.screenHeight - windowRect.height);
            }
            _positionAtOpen = new Vector2(windowRect.x, windowRect.y);
        }

        public override void PostOpen()
        {
            base.PostOpen();
            IsDialogOpen = true;
            _pendingFocusInput = true;
        }

        public override void PostClose()
        {
            base.PostClose();
            IsDialogOpen = false;
            // Remember position for next open
            _lastClosedPosition = new Vector2(windowRect.x, windowRect.y);
            _hasLastClosedPosition = true;
        }

        public override void DoWindowContents(Rect rect)
        {
            bool enterPressed = Event.current != null &&
                (Event.current.type == EventType.KeyDown || Event.current.rawType == EventType.KeyDown) &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            bool escapePressed = Event.current != null &&
                (Event.current.type == EventType.KeyDown || Event.current.rawType == EventType.KeyDown) &&
                Event.current.keyCode == KeyCode.Escape;

            Rect inner = rect.ContractedBy(Pad);
            Rect topBar = new Rect(inner.x, inner.y, inner.width, TopBarHeight);
            Rect body = new Rect(inner.x, topBar.yMax + Gap, inner.width, inner.height - TopBarHeight - Gap);
            Rect leftOuter = new Rect(body.x, body.y, LeftPanelWidth, body.height);
            Rect rightOuter = new Rect(leftOuter.xMax + Gap, body.y, body.width - LeftPanelWidth - Gap, body.height);

            Widgets.DrawMenuSection(topBar);
            Widgets.DrawMenuSection(leftOuter);
            Widgets.DrawMenuSection(rightOuter);

            DrawTopBar(topBar);
            DrawLeftPanel(leftOuter);
            DrawRightPanel(rightOuter, enterPressed);

            if (escapePressed && GUI.GetNameOfFocusedControl() == ChatInputControlName)
            {
                Event.current.Use();
                Close();
            }
        }

        private void DrawTopBar(Rect rect)
        {
            Rect inner = rect.ContractedBy(6f, 4f);
            float btnW = 90f;
            float x = inner.x;

            if (Widgets.ButtonText(new Rect(x, inner.y, btnW, inner.height), "Chat"))
                _pendingFocusInput = true;
            x += btnW + 4f;

            if (Widgets.ButtonText(new Rect(x, inner.y, btnW + 10f, inner.height), "Leaderboard"))
            {
                Close(); // Close chat first so leaderboard appears on top
                PM_Leaderboard.OpenLeaderboardDialog(requestFresh: true);
            }
            x += btnW + 14f;

            if (Widgets.ButtonText(new Rect(x, inner.y, 50f, inner.height), "Clear"))
                ChatMessages.Clear();
            x += 54f;

            if (Widgets.ButtonText(new Rect(x, inner.y, 60f, inner.height), "Reset"))
                SetWindowPosition(_positionAtOpen);
            x += 64f;

            if (Widgets.ButtonText(new Rect(x, inner.y, 62f, inner.height), "Default"))
                SetWindowPosition(_defaultPosition);

            // Right side checkboxes
            const string autoText = "Auto Scroll";
            Text.Font = GameFont.Small;
            float cbW = 100f;
            Rect cbRect = new Rect(inner.xMax - cbW, inner.y + 2f, cbW, inner.height - 4f);
            bool auto = ChatAutoscroll;
            Widgets.CheckboxLabeled(cbRect, autoText, ref auto, placeCheckboxNearText: true);
            ChatAutoscroll = auto;

            float muteW = 60f;
            Rect muteRect = new Rect(cbRect.x - muteW - 4f, inner.y + 2f, muteW, inner.height - 4f);
            bool muted = ShouldPlaySounds;
            Widgets.CheckboxLabeled(muteRect, "Mute", ref muted, placeCheckboxNearText: true);
            ShouldPlaySounds = muted;
        }

        private void SetWindowPosition(Vector2 desired)
        {
            windowRect.x = Mathf.Clamp(desired.x, 0f, UI.screenWidth - windowRect.width);
            windowRect.y = Mathf.Clamp(desired.y, 0f, UI.screenHeight - windowRect.height);
        }

        private void DrawLeftPanel(Rect rect)
        {
            Rect inner = rect.ContractedBy(6f);
            string playersLabel = SessionHandler.CurrentServerPlayers == 1
                ? "1 Player Online" : $"{SessionHandler.CurrentServerPlayers} Players Online";

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 22f), $"<color=grey>{playersLabel}</color>");
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.DrawLineHorizontal(inner.x, inner.y + 24f, inner.width);

            Rect listRect = new Rect(inner.x, inner.y + 28f, inner.width, inner.height - 28f);
            List<string> players = new List<string>();
            if (!string.IsNullOrWhiteSpace(SessionHandler.Username))
                players.Add(SessionHandler.Username);

            float viewH = Mathf.Max(listRect.height, 6f + players.Count * PlayerRowH);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, viewH);

            Widgets.BeginScrollView(listRect, ref _scrollPlayers, viewRect);
            try
            {
                float y = 0f;
                for (int i = 0; i < players.Count; i++)
                {
                    Rect row = new Rect(0f, y, viewRect.width, PlayerRowH);
                    if (i % 2 == 0) Widgets.DrawAltRect(row);
                    Widgets.DrawHighlightIfMouseover(row);
                    Rect label = new Rect(row.x + 6f, row.y + 2f, row.width - 12f, row.height - 4f);
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(label, players[i]);
                    Text.Anchor = TextAnchor.UpperLeft;
                    if (Widgets.ButtonInvisible(label, false))
                    {
                        if (!string.IsNullOrWhiteSpace(CurrentChatInput) && !CurrentChatInput.EndsWith(" "))
                            CurrentChatInput += " ";
                        CurrentChatInput += $"@{players[i]} ";
                        _pendingFocusInput = true;
                    }
                    y += PlayerRowH;
                }
            }
            finally { Widgets.EndScrollView(); }
        }

        private void DrawRightPanel(Rect rect, bool enterPressed)
        {
            Rect inner = rect.ContractedBy(6f);
            Rect inputRow = new Rect(inner.x, inner.yMax - InputHeight, inner.width, InputHeight);
            Rect messagesRect = new Rect(inner.x, inner.y, inner.width, inner.height - InputHeight - 6f);
            Widgets.DrawLineHorizontal(inner.x, inputRow.y - 3f, inner.width);
            DrawMessageList(messagesRect);
            bool inputFocused = GUI.GetNameOfFocusedControl() == ChatInputControlName;
            DrawInputRow(inputRow, enterPressed, inputFocused);
        }

        private void DrawMessageList(Rect mainRect)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            string[] messages = ChatMessages?.ToArray() ?? Array.Empty<string>();
            float scrollBarReserve = 16f;
            float viewW = mainRect.width - scrollBarReserve;
            float textW = Mathf.Max(10f, viewW - 12f);
            float height = 6f;
            for (int i = 0; i < messages.Length; i++)
            {
                float textH = Text.CalcHeight(messages[i] ?? "", textW);
                height += Mathf.Max(22f, textH + 6f);
            }
            Rect viewRect = new Rect(0f, 0f, viewW, Mathf.Max(mainRect.height, height));
            Widgets.BeginScrollView(mainRect, ref _scrollChat, viewRect);
            try
            {
                float y = 0f;
                for (int i = 0; i < messages.Length; i++)
                {
                    string msg = messages[i] ?? "";
                    float textH = Text.CalcHeight(msg, textW);
                    float rowH = Mathf.Max(22f, textH + 6f);
                    Widgets.Label(new Rect(6f, y + 2f, textW, rowH - 4f), msg);
                    y += rowH;
                }
            }
            finally { Widgets.EndScrollView(); }

            int currentCount = messages.Length;
            if (ChatAutoscroll && (ShouldScrollChat || currentCount != _lastChatCount))
            {
                _scrollChat.y = float.MaxValue;
                ShouldScrollChat = false;
            }
            _lastChatCount = currentCount;
        }

        private void DrawInputRow(Rect rect, bool enterPressed, bool inputFocused)
        {
            Text.Font = GameFont.Small;
            Rect sendRect = new Rect(rect.xMax - SendBtnW, rect.y, SendBtnW, rect.height);
            Rect textRect = new Rect(rect.x, rect.y, rect.width - SendBtnW - 6f, rect.height);
            GUI.SetNextControlName(ChatInputControlName);
            string input = Widgets.TextField(textRect, CurrentChatInput ?? "");
            if (input.Length <= 512) CurrentChatInput = input;
            if (!inputFocused && string.IsNullOrWhiteSpace(CurrentChatInput))
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
                Widgets.Label(textRect.ContractedBy(6f, 0f), "Type a message...");
                GUI.color = old;
            }
            if (_pendingFocusInput) { GUI.FocusControl(ChatInputControlName); _pendingFocusInput = false; }
            bool clickedSend = Widgets.ButtonText(sendRect, "Send");
            bool pressedEnterToSend = enterPressed && inputFocused;
            if (clickedSend || pressedEnterToSend)
            {
                TrySendChatInput();
                _pendingFocusInput = true;
                if (pressedEnterToSend && Event.current != null) Event.current.Use();
            }
        }

        private void TrySendChatInput()
        {
            string msg = CurrentChatInput;
            if (string.IsNullOrWhiteSpace(msg)) return;
            if (PM_Leaderboard.TryHandleChatCommand(msg.Trim()))
            {
                CurrentChatInput = "";
                Close(); // Close chat when opening leaderboard via command
                return;
            }
            PM_Chat.SendMessage(msg.Trim());
            CurrentChatInput = "";
        }

        [OnSessionEnd]
        private static void CloseTab() { IsDialogOpen = false; }
    }
}
