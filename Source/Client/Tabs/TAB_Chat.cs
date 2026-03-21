using System;
using System.Collections.Generic;
using System.Linq;
using GameClient.Managers;
using GameClient.PacketManagers;
using RimWorld;
using Shared;
using UnityEngine;
using Verse;

namespace GameClient.Tabs
{
    public class TAB_Chat : MainTabWindow
    {
        public override Vector2 RequestedTabSize => new Vector2(800f, 600f);

        private Vector2 _scrollPlayers = Vector2.zero;
        private Vector2 _scrollChat = Vector2.zero;

        private int _lastChatCount = -1;
        private bool _pendingFocusInput = true;

        private Vector2 _positionAtOpen = Vector2.zero;

        private static bool _defaultPositionCaptured = false;
        private static Vector2 _defaultPosition = Vector2.zero;

        private const string ChatInputControlName = "RWT_ChatInput";

        private const float Pad = 8f;
        private const float Gap = 6f;

        private const float LeftPanelWidth = 170f;
        private const float TopBarHeight = 32f;
        private const float InputHeight = 30f;

        private const float PlayerRowH = 28f;

        private const float SendBtnW = 84f;

        public TAB_Chat()
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

            windowRect.width = RequestedTabSize.x;
            windowRect.height = RequestedTabSize.y;

            float x = PM_Chat.ChatBoxPosition.x;
            float y = PM_Chat.ChatBoxPosition.y;

            windowRect.x = Mathf.Clamp(x, 0f, UI.screenWidth - windowRect.width);
            windowRect.y = Mathf.Clamp(y, 0f, UI.screenHeight - windowRect.height);

            _positionAtOpen = new Vector2(windowRect.x, windowRect.y);

            if (!_defaultPositionCaptured)
            {
                _defaultPositionCaptured = true;
                _defaultPosition = _positionAtOpen;
            }
        }

        public override void PostOpen()
        {
            base.PostOpen();

            PM_Chat.IsChatTabOpen = true;
            PM_Chat.ToggleChatIcon(false);

            _pendingFocusInput = true;
        }

        public override void PostClose()
        {
            base.PostClose();
            PM_Chat.IsChatTabOpen = false;
        }

        public override void DoWindowContents(Rect rect)
        {
            bool enterPressed =
                Event.current != null &&
                (Event.current.type == EventType.KeyDown || Event.current.rawType == EventType.KeyDown) &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);

            bool escapePressed =
                Event.current != null &&
                (Event.current.type == EventType.KeyDown || Event.current.rawType == EventType.KeyDown) &&
                Event.current.keyCode == KeyCode.Escape;

            PM_Chat.ChatBoxPosition.x = windowRect.x;
            PM_Chat.ChatBoxPosition.y = windowRect.y;

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

            float btnW = 110f;

            Rect chatBtn = new Rect(inner.x, inner.y, btnW, inner.height);
            Rect lbBtn = new Rect(chatBtn.xMax + 6f, inner.y, btnW, inner.height);
            Rect resetBtn = new Rect(lbBtn.xMax + 6f, inner.y, btnW, inner.height);
            Rect defaultBtn = new Rect(resetBtn.xMax + 6f, inner.y, btnW, inner.height);

            if (Widgets.ButtonText(chatBtn, "Chat"))
                _pendingFocusInput = true;

            if (Widgets.ButtonText(lbBtn, "Leaderboard"))
                PM_Leaderboard.OpenLeaderboardDialog(requestFresh: true);

            if (Widgets.ButtonText(resetBtn, "Reset"))
                SetWindowPosition(_positionAtOpen);

            if (Widgets.ButtonText(defaultBtn, "Default"))
                SetWindowPosition(_defaultPosition);

            const string pinText = "Auto Scroll";
            Text.Font = GameFont.Small;
            Vector2 size = Text.CalcSize(pinText);

            float cbW = Mathf.Clamp(size.x + 40f, 110f, 160f);
            Rect cbRect = new Rect(inner.xMax - cbW, inner.y + 2f, cbW, inner.height - 4f);

            bool auto = PM_Chat.ChatAutoscroll;
            Widgets.CheckboxLabeled(cbRect, pinText, ref auto, placeCheckboxNearText: true);
            PM_Chat.ChatAutoscroll = auto;

            TooltipHandler.TipRegion(resetBtn, "Return to the position the window opened at.");
            TooltipHandler.TipRegion(defaultBtn, "Return to the original default position.");
        }

        private void SetWindowPosition(Vector2 desired)
        {
            float clampedX = Mathf.Clamp(desired.x, 0f, UI.screenWidth - windowRect.width);
            float clampedY = Mathf.Clamp(desired.y, 0f, UI.screenHeight - windowRect.height);

            windowRect.x = clampedX;
            windowRect.y = clampedY;

            PM_Chat.ChatBoxPosition.x = windowRect.x;
            PM_Chat.ChatBoxPosition.y = windowRect.y;
        }

        private void DrawLeftPanel(Rect rect)
        {
            Rect inner = rect.ContractedBy(6f);

            string playersLabel = PM_Recount.CurrentPlayers == 1
                ? "1 Player Online"
                : $"{PM_Recount.CurrentPlayers} Players Online";

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 22f), $"<color=grey>{playersLabel}</color>");
            Text.Anchor = TextAnchor.UpperLeft;

            Widgets.DrawLineHorizontal(inner.x, inner.y + 24f, inner.width);

            Rect listRect = new Rect(inner.x, inner.y + 28f, inner.width, inner.height - 28f);

            List<string> ordered = PM_Recount.CurrentPlayerNames?.ToList() ?? new List<string>();
            ordered.Sort(StringComparer.OrdinalIgnoreCase);

            float viewH = Mathf.Max(listRect.height, 6f + ordered.Count * PlayerRowH);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, viewH);

            Widgets.BeginScrollView(listRect, ref _scrollPlayers, viewRect);
            try
            {
                float y = 0f;
                float yMin = _scrollPlayers.y - PlayerRowH;
                float yMax = _scrollPlayers.y + listRect.height + PlayerRowH;

                for (int i = 0; i < ordered.Count; i++)
                {
                    if (y > yMin && y < yMax)
                    {
                        Rect row = new Rect(0f, y, viewRect.width, PlayerRowH);

                        if (i % 2 == 0)
                            Widgets.DrawAltRect(row);

                        Widgets.DrawHighlightIfMouseover(row);

                        Rect label = new Rect(row.x + 6f, row.y + 2f, row.width - 12f, row.height - 4f);

                        Text.Anchor = TextAnchor.MiddleLeft;
                        Widgets.LabelEllipses(label, ordered[i]);
                        Text.Anchor = TextAnchor.UpperLeft;

                        if (Widgets.ButtonInvisible(label, false))
                        {
                            string who = ordered[i];
                            if (!string.IsNullOrWhiteSpace(who))
                            {
                                if (!string.IsNullOrWhiteSpace(PM_Chat.CurrentChatInput) && !PM_Chat.CurrentChatInput.EndsWith(" "))
                                    PM_Chat.CurrentChatInput += " ";

                                PM_Chat.CurrentChatInput += $"@{who} ";
                                _pendingFocusInput = true;
                            }
                        }
                    }

                    y += PlayerRowH;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
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

            string[] messages = PM_Chat.ChatMessageCache?.ToArray() ?? Array.Empty<string>();

            float scrollBarReserve = 16f;
            float viewW = mainRect.width - scrollBarReserve;

            float textW = Mathf.Max(10f, viewW - 12f);
            float height = 6f;

            for (int i = 0; i < messages.Length; i++)
            {
                string msg = messages[i] ?? string.Empty;
                float textH = Text.CalcHeight(msg, textW);
                float rowH = Mathf.Max(22f, textH + 6f);
                height += rowH;
            }

            float viewH = Mathf.Max(mainRect.height, height);
            Rect viewRect = new Rect(0f, 0f, viewW, viewH);

            Widgets.BeginScrollView(mainRect, ref _scrollChat, viewRect);
            try
            {
                float y = 0f;

                for (int i = 0; i < messages.Length; i++)
                {
                    string msg = messages[i] ?? string.Empty;

                    float textH = Text.CalcHeight(msg, textW);
                    float rowH = Mathf.Max(22f, textH + 6f);

                    Rect textRect = new Rect(6f, y + 2f, textW, rowH - 4f);
                    Widgets.Label(textRect, msg);

                    y += rowH;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }

            int currentCount = messages.Length;
            bool countChanged = currentCount != _lastChatCount;
            _lastChatCount = currentCount;

            if (PM_Chat.ChatAutoscroll && (PM_Chat.ShouldScrollChat || countChanged))
            {
                _scrollChat.y = float.MaxValue;
                PM_Chat.ShouldScrollChat = false;
            }
        }

        private void DrawInputRow(Rect rect, bool enterPressed, bool inputFocused)
        {
            Text.Font = GameFont.Small;

            Rect sendRect = new Rect(rect.xMax - SendBtnW, rect.y, SendBtnW, rect.height);
            Rect textRect = new Rect(rect.x, rect.y, rect.width - SendBtnW - 6f, rect.height);

            GUI.SetNextControlName(ChatInputControlName);
            string input = Widgets.TextField(textRect, PM_Chat.CurrentChatInput ?? string.Empty);

            if (input.Length <= 512)
                PM_Chat.CurrentChatInput = input;

            if (!inputFocused && string.IsNullOrWhiteSpace(PM_Chat.CurrentChatInput))
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
                Widgets.Label(textRect.ContractedBy(6f, 0f), "Type a message...");
                GUI.color = old;
            }

            if (_pendingFocusInput)
            {
                GUI.FocusControl(ChatInputControlName);
                _pendingFocusInput = false;
            }

            bool clickedSend = Widgets.ButtonText(sendRect, "Send");
            bool pressedEnterToSend = enterPressed && inputFocused;

            if (clickedSend || pressedEnterToSend)
            {
                TrySendChatInput();
                _pendingFocusInput = true;

                if (pressedEnterToSend && Event.current != null)
                    Event.current.Use();
            }
        }

        private void TrySendChatInput()
        {
            string msg = PM_Chat.CurrentChatInput;

            if (string.IsNullOrWhiteSpace(msg))
                return;

            PM_Chat.SendMessage(msg.Trim());
            PM_Chat.CurrentChatInput = "";
        }
    }
}