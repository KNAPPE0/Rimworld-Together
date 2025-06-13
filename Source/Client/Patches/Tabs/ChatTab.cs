using RimWorld;
using UnityEngine;
using Verse;
using System;
<<<<<<< HEAD
=======
using System.Collections.Generic;
>>>>>>> 8122f745b7e5c1dd1f5d4e47aa7442f948a0eab5
using System.Linq;
using GameClient.Managers;
using GameClient.Values;

namespace GameClient.Patches.Tabs
{
    public class ChatTab : MainTabWindow
    {
        public override Vector2 RequestedTabSize => new Vector2(800f, 600f);

<<<<<<< HEAD
        private Vector2 scrollPositionPlayers     = Vector2.zero;
        private Vector2 scrollPositionChat        = Vector2.zero;
        private Vector2 scrollPositionLeaderboard = Vector2.zero;
        private Vector2 scrollPositionSettings    = Vector2.zero;
=======
        private Vector2 scrollPositionPlayers = Vector2.zero;
        private Vector2 scrollPositionChat = Vector2.zero;
        private Vector2 scrollPositionLeaderboard = Vector2.zero;
        private Vector2 scrollPositionSettings = Vector2.zero;
>>>>>>> 8122f745b7e5c1dd1f5d4e47aa7442f948a0eab5

        private readonly int startAcceptingInputAtFrame;
        private bool leaderboardRequested;

        private enum Panel { Chat, Leaderboard, Settings }
        private Panel currentPanel;

        private bool AcceptsInput => startAcceptingInputAtFrame <= Time.frameCount;

        public ChatTab()
        {
<<<<<<< HEAD
            startAcceptingInputAtFrame = Time.frameCount + 1;

            layer             = WindowLayer.GameUI;
            forcePause        = false;
            draggable         = true;
            focusWhenOpened   = false;
            drawShadow        = false;
            closeOnAccept     = false;
            closeOnCancel     = false;
            preventCameraMotion = false;
            drawInScreenshotMode = false;
            soundAppear       = SoundDefOf.CommsWindow_Open;
            closeOnCancel     = true;

            currentPanel = (Panel)ChatCustomizationManager.DefaultPanel;
            ChatManager.ChatAutoscroll = ChatCustomizationManager.AutoScrollDefault;
=======
            layer = WindowLayer.GameUI;
            forcePause = false;
            draggable = true;
            focusWhenOpened = false;
            drawShadow = false;
            closeOnAccept = false;
            closeOnCancel = false;
            preventCameraMotion = false;
            drawInScreenshotMode = false;
            soundAppear = SoundDefOf.CommsWindow_Open;
            closeOnCancel = true;

            // Load customization defaults
            currentPanel = (Panel)ChatCustomizationManager.Settings.DefaultPanel;
            ChatManager.ChatAutoscroll = ChatCustomizationManager.Settings.AutoScrollDefault;
>>>>>>> 8122f745b7e5c1dd1f5d4e47aa7442f948a0eab5
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
<<<<<<< HEAD
            int oldFontSize = GUI.skin.label.fontSize;
            GUI.skin.label.fontSize = Mathf.RoundToInt(ChatCustomizationManager.FontSize);

            Widgets.DrawBoxSolid(rect, ChatCustomizationManager.BackgroundColor);
            ChatManager.ChatBoxPosition = new Vector2(windowRect.x, windowRect.y);

            float tabH = 28f, btnW = 110f;
            Rect chatBtn = new Rect(rect.x, rect.y, btnW, tabH);
            Rect lbBtn   = new Rect(rect.x + btnW + 6f, rect.y, btnW, tabH);
            Rect setBtn  = new Rect(rect.x + 2*(btnW+6f), rect.y, btnW, tabH);

            Widgets.DrawHighlightIfMouseover(chatBtn);
            Widgets.DrawHighlightIfMouseover(lbBtn);
            Widgets.DrawHighlightIfMouseover(setBtn);

            if (Widgets.ButtonText(chatBtn, "Chat"))        currentPanel = Panel.Chat;
            if (Widgets.ButtonText(lbBtn,   "Leaderboard")) currentPanel = Panel.Leaderboard;
            if (Widgets.ButtonText(setBtn,  "Settings"))    currentPanel = Panel.Settings;

            Widgets.DrawLineHorizontal(rect.x, rect.y + tabH, rect.width);

            Rect contentRect = new Rect(rect.x, rect.y + tabH + 4f,
                                        rect.width, rect.height - tabH - 4f);
            switch (currentPanel)
            {
                case Panel.Chat:
                    DrawChatPanel(contentRect);
                    break;
                case Panel.Leaderboard:
                    DrawLeaderboardPanel(contentRect);
                    break;
                case Panel.Settings:
                    DrawSettingsPanel(contentRect);
                    break;
            }

            GUI.skin.label.fontSize = oldFontSize;
        }

        private void DrawChatPanel(Rect rect)
        {
            Widgets.DrawLineVertical(rect.x + 160f, rect.y, rect.height);
=======
            // Apply custom font size
            int oldFontSize = GUI.skin.label.fontSize;
            GUI.skin.label.fontSize = Mathf.RoundToInt(ChatCustomizationManager.Settings.FontSize);

            // Background tint
            Widgets.DrawBoxSolid(rect, ChatCustomizationManager.Settings.BackgroundColor);
>>>>>>> 8122f745b7e5c1dd1f5d4e47aa7442f948a0eab5

            ChatManager.ChatBoxPosition = new Vector2(windowRect.x, windowRect.y);

            // Top toolbar
            float tabH = 28f, btnW = 110f;
            Rect chatBtn = new Rect(rect.x, rect.y, btnW, tabH);
            Rect lbBtn = new Rect(rect.x + btnW + 6f, rect.y, btnW, tabH);
            Rect setBtn = new Rect(rect.x + 2 * (btnW + 6f), rect.y, btnW, tabH);

            Widgets.DrawHighlightIfMouseover(chatBtn);
            Widgets.DrawHighlightIfMouseover(lbBtn);
            Widgets.DrawHighlightIfMouseover(setBtn);

            if (Widgets.ButtonText(chatBtn, "Chat")) currentPanel = Panel.Chat;
            if (Widgets.ButtonText(lbBtn, "Leaderboard"))
            {
                currentPanel = Panel.Leaderboard;
                if (!leaderboardRequested)
                {
                    ChatManager.SendMessage("/leaderboard");
                    leaderboardRequested = true;
                }
            }
            if (Widgets.ButtonText(setBtn, "Settings")) currentPanel = Panel.Settings;

            Widgets.DrawLineHorizontal(rect.x, rect.y + tabH, rect.width);

            // Content area
            Rect contentRect = new Rect(rect.x, rect.y + tabH + 4f, rect.width, rect.height - tabH - 4f);
            switch (currentPanel)
            {
                case Panel.Chat:
                    DrawChatPanel(contentRect);
                    break;
                case Panel.Leaderboard:
                    DrawLeaderboardPanel(contentRect);
                    break;
                case Panel.Settings:
                    DrawSettingsPanel(contentRect);
                    break;
            }

            // Restore font size
            GUI.skin.label.fontSize = oldFontSize;
        }

        private void DrawChatPanel(Rect rect)
        {
            Widgets.DrawLineVertical(rect.x + 160f, rect.y, rect.height);
            DrawPlayerCount(rect);
            DrawPlayerList(new Rect(rect.x, rect.y, 160f, rect.height - 50f));
<<<<<<< HEAD
            DrawMessageList(new Rect(rect.x + 160f, rect.y + 7f,
                                     rect.width - 160f, rect.height - 60f));

            DrawPinCheckbox(rect);

            var clearBtn = new Rect(rect.x + 5f, rect.y + 5f, 100f, 24f);
            if (Widgets.ButtonText(clearBtn, "Clear Chat"))
                ChatManager.ChatMessageCache.Clear();

            DrawInput(rect);
            CheckForEnterKey();

=======
            DrawMessageList(new Rect(rect.x + 160f, rect.y + 7f, rect.width - 160f, rect.height - 60f));
            DrawPinCheckbox(rect);
            DrawInput(rect);
            CheckForEnterKey();
>>>>>>> 8122f745b7e5c1dd1f5d4e47aa7442f948a0eab5
            if (ChatManager.ShouldScrollChat)
                ScrollToLastMessage();
        }

        private void DrawLeaderboardPanel(Rect rect)
        {
<<<<<<< HEAD
            Rect header = new Rect(rect.x, rect.y, rect.width, 30f);
            Widgets.DrawHighlight(header);
            Text.Font = GameFont.Medium;
            GUI.color = ChatCustomizationManager.FontColor;
            Widgets.Label(header, "Leaderboard");
            GUI.color = Color.white;

=======
            // Header
            Rect header = new Rect(rect.x, rect.y, rect.width, 30f);
            Widgets.DrawHighlight(header);
            Text.Font = GameFont.Medium;
            GUI.color = ChatCustomizationManager.Settings.FontColor;
            Widgets.Label(header, "Leaderboard");
            GUI.color = Color.white;

            // Column headers
>>>>>>> 8122f745b7e5c1dd1f5d4e47aa7442f948a0eab5
            Rect cols = new Rect(rect.x, rect.y + 30f, rect.width, 25f);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(cols.x + 5f, cols.y, 40f, cols.height), "#");
            Widgets.Label(new Rect(cols.x + 50f, cols.y, rect.width * 0.5f, cols.height), "Player");
<<<<<<< HEAD
            Widgets.Label(new Rect(cols.x + rect.width * 0.5f + 5f, cols.y,
                                   rect.width * 0.5f - 10f, cols.height), "Wealth");
            Widgets.DrawLineHorizontal(rect.x, cols.y + cols.height, rect.width);

            var lines = ChatManager.ChatMessageCache
                          .Where(l => char.IsDigit(l.FirstOrDefault()) && l.Contains("–"))
                          .ToList();
            int count = lines.Count;
            float rowH = 25f;
            float contentH = count * rowH;
            Rect viewRect = new Rect(rect.x, rect.y + 55f,
                                     rect.width - 16f, contentH);

            Widgets.BeginScrollView(
                new Rect(rect.x, rect.y + 55f, rect.width, rect.height - 55f),
                ref scrollPositionLeaderboard,
                viewRect
            );
=======
            Widgets.Label(new Rect(cols.x + rect.width * 0.5f + 5f, cols.y, rect.width * 0.5f - 10f, cols.height), "Wealth");
            Widgets.DrawLineHorizontal(rect.x, cols.y + cols.height, rect.width);

            // Leaderboard lines
            var lines = ChatManager.ChatMessageCache
                .Where(l => char.IsDigit(l.FirstOrDefault()) && l.Contains("–"))
                .ToList();
            int count = lines.Count;
            float rowH = 25f;
            float contentH = count * rowH;
            Rect viewRect = new Rect(rect.x, rect.y + 55f, rect.width - 16f, contentH);

            Widgets.BeginScrollView(new Rect(rect.x, rect.y + 55f, rect.width, rect.height - 55f), ref scrollPositionLeaderboard, viewRect);
>>>>>>> 8122f745b7e5c1dd1f5d4e47aa7442f948a0eab5
            float y = rect.y + 55f;
            for (int i = 0; i < count; i++)
            {
                Rect row = new Rect(rect.x, y, viewRect.width, rowH);
                if (i % 2 == 0) Widgets.DrawLightHighlight(row);

                string line = lines[i];
                int sep = line.IndexOf('–');
                string left = sep > 0 ? line.Substring(0, sep).Trim() : line;
                string right = sep > 0 ? line.Substring(sep + 1).Trim() : string.Empty;
                var parts = left.Split(new[] { '.' }, 2);
                string rank = parts.Length > 0 ? parts[0].Trim() : string.Empty;
                string name = parts.Length > 1 ? parts[1].Trim() : string.Empty;

<<<<<<< HEAD
                GUI.color = ChatCustomizationManager.FontColor;
                Widgets.Label(new Rect(row.x + 5f, row.y + 3f,
                                       30f, Text.LineHeight), rank);
                Widgets.Label(new Rect(row.x + 40f, row.y + 3f,
                                       rect.width * 0.5f, Text.LineHeight), name);
                Widgets.Label(new Rect(row.x + rect.width * 0.5f + 5f,
                                       row.y + 3f, rect.width * 0.5f - 10f,
                                       Text.LineHeight), right);
=======
                GUI.color = ChatCustomizationManager.Settings.FontColor;
                Widgets.Label(new Rect(row.x + 5f, row.y + 3f, 30f, Text.LineHeight), rank);
                Widgets.Label(new Rect(row.x + 40f, row.y + 3f, rect.width * 0.5f, Text.LineHeight), name);
                Widgets.Label(new Rect(row.x + rect.width * 0.5f + 5f, row.y + 3f, rect.width * 0.5f - 10f, Text.LineHeight), right);
>>>>>>> 8122f745b7e5c1dd1f5d4e47aa7442f948a0eab5
                GUI.color = Color.white;
                y += rowH;
            }
            Widgets.EndScrollView();
        }

        private void DrawSettingsPanel(Rect rect)
        {
            float y = rect.y + 10f;
            float lineH = 26f;
            Text.Font = GameFont.Small;

            // Font Size
<<<<<<< HEAD
            Widgets.Label(new Rect(rect.x + 10f, y, 200f, lineH),
                          $"Font Size: {ChatCustomizationManager.FontSize:F0}");
            ChatCustomizationManager.FontSize = Widgets.HorizontalSlider(
                new Rect(rect.x + 220f, y + 6f, rect.width - 240f, 20f),
                ChatCustomizationManager.FontSize, 12f, 40f);
            y += lineH + 4f;

            // Font Color RGBA
            var fc = ChatCustomizationManager.FontColor;
            Widgets.Label(new Rect(rect.x + 10f, y, 200f, lineH), "Font Color R:");
            fc.r = Widgets.HorizontalSlider(
                new Rect(rect.x + 120f, y + 6f, rect.width - 140f, 20f),
                fc.r, 0f, 1f);
            y += lineH;
            Widgets.Label(new Rect(rect.x + 10f, y, 200f, lineH), "Font Color G:");
            fc.g = Widgets.HorizontalSlider(
                new Rect(rect.x + 120f, y + 6f, rect.width - 140f, 20f),
                fc.g, 0f, 1f);
            y += lineH;
            Widgets.Label(new Rect(rect.x + 10f, y, 200f, lineH), "Font Color B:");
            fc.b = Widgets.HorizontalSlider(
                new Rect(rect.x + 120f, y + 6f, rect.width - 140f, 20f),
                fc.b, 0f, 1f);
            y += lineH;
            Widgets.Label(new Rect(rect.x + 10f, y, 200f, lineH), "Font Color A:");
            fc.a = Widgets.HorizontalSlider(
                new Rect(rect.x + 120f, y + 6f, rect.width - 140f, 20f),
                fc.a, 0f, 1f);
            y += lineH + 8f;
            ChatCustomizationManager.FontColor = fc;

            // Background Color RGBA
            var bc = ChatCustomizationManager.BackgroundColor;
            Widgets.Label(new Rect(rect.x + 10f, y, 200f, lineH), "Background R:");
            bc.r = Widgets.HorizontalSlider(
                new Rect(rect.x + 120f, y + 6f, rect.width - 140f, 20f),
                bc.r, 0f, 1f);
            y += lineH;
            Widgets.Label(new Rect(rect.x + 10f, y, 200f, lineH), "Background G:");
            bc.g = Widgets.HorizontalSlider(
                new Rect(rect.x + 120f, y + 6f, rect.width - 140f, 20f),
                bc.g, 0f, 1f);
            y += lineH;
            Widgets.Label(new Rect(rect.x + 10f, y, 200f, lineH), "Background B:");
            bc.b = Widgets.HorizontalSlider(
                new Rect(rect.x + 120f, y + 6f, rect.width - 140f, 20f),
                bc.b, 0f, 1f);
            y += lineH;
            Widgets.Label(new Rect(rect.x + 10f, y, 200f, lineH), "Background A:");
            bc.a = Widgets.HorizontalSlider(
                new Rect(rect.x + 120f, y + 6f, rect.width - 140f, 20f),
                bc.a, 0f, 1f);
            y += lineH + 8f;
            ChatCustomizationManager.BackgroundColor = bc;

            // Auto-scroll default
            var asd = ChatCustomizationManager.AutoScrollDefault;
            Widgets.CheckboxLabeled(new Rect(rect.x + 10f, y, 250f, lineH),
                "Auto-Scroll Chat by Default", ref asd);
            ChatCustomizationManager.AutoScrollDefault = asd;
=======
            Widgets.Label(new Rect(rect.x + 10f, y, 200f, lineH), $"Font Size: {ChatCustomizationManager.Settings.FontSize:F0}");
            ChatCustomizationManager.Settings.FontSize = Widgets.HorizontalSlider(
                new Rect(rect.x + 220f, y + 6f, rect.width - 240f, 20f),
                ChatCustomizationManager.Settings.FontSize, 10f, 30f);
            y += lineH + 4f;

            // Font Color RGBA
            Widgets.Label(new Rect(rect.x + 10f, y, 200f, lineH), "Font Color R:");
            ChatCustomizationManager.Settings.FontColor.r = Widgets.HorizontalSlider(
                new Rect(rect.x + 120f, y + 6f, rect.width - 140f, 20f),
                ChatCustomizationManager.Settings.FontColor.r, 0f, 1f);
            y += lineH;
            Widgets.Label(new Rect(rect.x + 10f, y, 200f, lineH), "Font Color G:");
            ChatCustomizationManager.Settings.FontColor.g = Widgets.HorizontalSlider(
                new Rect(rect.x + 120f, y + 6f, rect.width - 140f, 20f),
                ChatCustomizationManager.Settings.FontColor.g, 0f, 1f);
            y += lineH;
            Widgets.Label(new Rect(rect.x + 10f, y, 200f, lineH), "Font Color B:");
            ChatCustomizationManager.Settings.FontColor.b = Widgets.HorizontalSlider(
                new Rect(rect.x + 120f, y + 6f, rect.width - 140f, 20f),
                ChatCustomizationManager.Settings.FontColor.b, 0f, 1f);
            y += lineH;
            Widgets.Label(new Rect(rect.x + 10f, y, 200f, lineH), "Font Color A:");
            ChatCustomizationManager.Settings.FontColor.a = Widgets.HorizontalSlider(
                new Rect(rect.x + 120f, y + 6f, rect.width - 140f, 20f),
                ChatCustomizationManager.Settings.FontColor.a, 0f, 1f);
            y += lineH + 8f;

            // Background Color RGBA
            Widgets.Label(new Rect(rect.x + 10f, y, 200f, lineH), "Background R:");
            ChatCustomizationManager.Settings.BackgroundColor.r = Widgets.HorizontalSlider(
                new Rect(rect.x + 120f, y + 6f, rect.width - 140f, 20f),
                ChatCustomizationManager.Settings.BackgroundColor.r, 0f, 1f);
            y += lineH;
            Widgets.Label(new Rect(rect.x + 10f, y, 200f, lineH), "Background G:");
            ChatCustomizationManager.Settings.BackgroundColor.g = Widgets.HorizontalSlider(
                new Rect(rect.x + 120f, y + 6f, rect.width - 140f, 20f),
                ChatCustomizationManager.Settings.BackgroundColor.g, 0f, 1f);
            y += lineH;
            Widgets.Label(new Rect(rect.x + 10f, y, 200f, lineH), "Background B:");
            ChatCustomizationManager.Settings.BackgroundColor.b = Widgets.HorizontalSlider(
                new Rect(rect.x + 120f, y + 6f, rect.width - 140f, 20f),
                ChatCustomizationManager.Settings.BackgroundColor.b, 0f, 1f);
            y += lineH;
            Widgets.Label(new Rect(rect.x + 10f, y, 200f, lineH), "Background A:");
            ChatCustomizationManager.Settings.BackgroundColor.a = Widgets.HorizontalSlider(
                new Rect(rect.x + 120f, y + 6f, rect.width - 140f, 20f),
                ChatCustomizationManager.Settings.BackgroundColor.a, 0f, 1f);
            y += lineH + 12f;

            // Auto-scroll default
            Widgets.CheckboxLabeled(new Rect(rect.x + 10f, y, 250f, lineH),
                "Auto-Scroll Chat by Default", ref ChatCustomizationManager.Settings.AutoScrollDefault);
>>>>>>> 8122f745b7e5c1dd1f5d4e47aa7442f948a0eab5
            y += lineH + 8f;

            // Default Panel
            Widgets.Label(new Rect(rect.x + 10f, y, 200f, lineH), "Default Panel:");
<<<<<<< HEAD
            var rChat = new Rect(rect.x + 220f, y, 100f, lineH);
            if (Widgets.RadioButton(new Vector2(rChat.x, rChat.y),
                     ChatCustomizationManager.DefaultPanel == ChatPanel.Chat))
                ChatCustomizationManager.DefaultPanel = ChatPanel.Chat;
            Widgets.Label(new Rect(rChat.x + 20f, rChat.y, 80f, lineH), "Chat");

            var rLb = new Rect(rChat.x + 120f, y, 150f, lineH);
            if (Widgets.RadioButton(new Vector2(rLb.x, rLb.y),
                     ChatCustomizationManager.DefaultPanel == ChatPanel.Leaderboard))
                ChatCustomizationManager.DefaultPanel = ChatPanel.Leaderboard;
            Widgets.Label(new Rect(rLb.x + 20f, rLb.y, 120f, lineH), "Leaderboard");
            y += lineH + 12f;

            // Save Button
            var btn = new Rect(rect.center.x - 40f, y, 80f, 30f);
            if (Widgets.ButtonText(btn, "Save"))
                ChatCustomizationManager.Save();
=======
            Rect radioChat = new Rect(rect.x + 220f, y, 100f, lineH);
            if (Widgets.RadioButton(new Vector2(radioChat.x, radioChat.y), ChatCustomizationManager.Settings.DefaultPanel == ChatPanel.Chat))
                ChatCustomizationManager.Settings.DefaultPanel = ChatPanel.Chat;
            Widgets.Label(new Rect(radioChat.x + 20f, radioChat.y, 80f, lineH), "Chat");

            Rect radioLb = new Rect(radioChat.x + 120f, y, 150f, lineH);
            if (Widgets.RadioButton(new Vector2(radioLb.x, radioLb.y), ChatCustomizationManager.Settings.DefaultPanel == ChatPanel.Leaderboard))
                ChatCustomizationManager.Settings.DefaultPanel = ChatPanel.Leaderboard;
            Widgets.Label(new Rect(radioLb.x + 20f, radioLb.y, 120f, lineH), "Leaderboard");
            y += lineH + 12f;

            // Save Button
            Rect btnRect = new Rect(rect.center.x - 40f, y, 80f, 30f);
            if (Widgets.ButtonText(btnRect, "Save"))
            {
                ChatCustomizationManager.Save();
            }
>>>>>>> 8122f745b7e5c1dd1f5d4e47aa7442f948a0eab5
        }

        private void DrawPlayerCount(Rect rect)
        {
            string txt = RecountManager.CurrentPlayers > 1
                ? $"{RecountManager.CurrentPlayers} Players Online"
                : $"{RecountManager.CurrentPlayers} Player Online";
            Text.Font = GameFont.Small;
<<<<<<< HEAD
            GUI.color = ChatCustomizationManager.FontColor;
            Vector2 size = Text.CalcSize(txt);
            Widgets.Label(new Rect(rect.x, rect.y, size.x, size.y),
                          $"<color=grey>{txt}</color>");
=======
            GUI.color = ChatCustomizationManager.Settings.FontColor;
            Vector2 size = Text.CalcSize(txt);
            Widgets.Label(new Rect(rect.x, rect.y, size.x, size.y), $"<color=grey>{txt}</color>");
>>>>>>> 8122f745b7e5c1dd1f5d4e47aa7442f948a0eab5
            GUI.color = Color.white;
        }

        private void DrawPlayerList(Rect mainRect)
        {
            var list = RecountManager.CurrentPlayerNames.OrderBy(n => n).ToList();
            float height = list.Count * 25f + 10f;
<<<<<<< HEAD
            Rect viewRect = new Rect(mainRect.x, mainRect.y,
                                     mainRect.width - 16f, height);
            Widgets.BeginScrollView(mainRect, ref scrollPositionPlayers, viewRect);
            float y = mainRect.y;
            GUI.color = ChatCustomizationManager.FontColor;
            foreach (string name in list)
            {
                Rect row = new Rect(mainRect.x + 5f, y + 5f,
                                     viewRect.width - 10f, 25f);
                Widgets.Label(row, name);
                if (Widgets.ButtonInvisible(row))
                    ChatManager.CurrentChatInput += $"@{name} ";
=======
            Rect viewRect = new Rect(mainRect.x, mainRect.y, mainRect.width - 16f, height);
            Widgets.BeginScrollView(mainRect, ref scrollPositionPlayers, viewRect);
            float y = mainRect.y;
            GUI.color = ChatCustomizationManager.Settings.FontColor;
            foreach (string name in list)
            {
                Rect row = new Rect(mainRect.x + 5f, y + 5f, viewRect.width - 10f, 25f);
                Widgets.Label(row, name);
                if (Widgets.ButtonInvisible(row)) ChatManager.CurrentChatInput += $"@{name} ";
>>>>>>> 8122f745b7e5c1dd1f5d4e47aa7442f948a0eab5
                Widgets.DrawHighlightIfMouseover(row);
                y += 25f;
            }
            GUI.color = Color.white;
            Widgets.EndScrollView();
        }

        private void DrawMessageList(Rect mainRect)
        {
            var cache = ChatManager.ChatMessageCache.ToArray();
<<<<<<< HEAD
            float totalHeight = cache.Sum(
                m => Text.CalcHeight(m, mainRect.width - 30f)) + 10f;
            Rect viewRect = new Rect(mainRect.x, mainRect.y,
                                     mainRect.width - 30f, totalHeight);

            Widgets.BeginScrollView(mainRect, ref scrollPositionChat, viewRect);

            float maxY = viewRect.height - mainRect.height;
            if (maxY < 0) maxY = 0;
            if (scrollPositionChat.y < maxY && ChatManager.ChatAutoscroll)
                ChatManager.ChatAutoscroll = false;

            float y = mainRect.y;
            GUI.color = ChatCustomizationManager.FontColor;
=======
            float totalHeight = cache.Sum(m => Text.CalcHeight(m, mainRect.width - 30f)) + 10f;
            Rect viewRect = new Rect(mainRect.x, mainRect.y, mainRect.width - 30f, totalHeight);
            Widgets.BeginScrollView(mainRect, ref scrollPositionChat, viewRect);
            float y = mainRect.y;
            GUI.color = ChatCustomizationManager.Settings.FontColor;
>>>>>>> 8122f745b7e5c1dd1f5d4e47aa7442f948a0eab5
            foreach (string msg in cache)
            {
                float h = Text.CalcHeight(msg, mainRect.width - 30f);
                Rect row = new Rect(mainRect.x + 5f, y + 5f, viewRect.width, h);
                Widgets.Label(row, msg);
                y += h;
            }
            GUI.color = Color.white;
            Widgets.EndScrollView();
        }

        private void DrawInput(Rect rect)
        {
            Text.Font = GameFont.Small;
<<<<<<< HEAD
            Rect inputRect = new Rect(rect.x + 165f,
                                      rect.yMax - 25f,
                                      rect.width - 165f, 25f);
=======
            Rect inputRect = new Rect(rect.x + 165f, rect.yMax - 25f, rect.width - 165f, 25f);
>>>>>>> 8122f745b7e5c1dd1f5d4e47aa7442f948a0eab5
            string input = Widgets.TextField(inputRect, ChatManager.CurrentChatInput);
            if (AcceptsInput && input.Length <= 512)
                ChatManager.CurrentChatInput = input;
        }

        private void DrawPinCheckbox(Rect rect)
        {
            const string label = "Auto Scroll";
            Text.Font = GameFont.Small;
<<<<<<< HEAD
            bool autoScroll = ChatManager.ChatAutoscroll;
            Rect chkRect = new Rect(rect.xMax - 140f, rect.y + 5f,
                                     140f, 24f);
            Widgets.CheckboxLabeled(chkRect, label, ref autoScroll);
            ChatManager.ChatAutoscroll = autoScroll;
=======
            Rect chkRect = new Rect(rect.xMax - 140f, rect.y + 5f, 140f, 24f);
            Widgets.CheckboxLabeled(chkRect, label, ref ChatManager.ChatAutoscroll);
>>>>>>> 8122f745b7e5c1dd1f5d4e47aa7442f948a0eab5
        }

        private void CheckForEnterKey()
        {
<<<<<<< HEAD
            if (AcceptsInput &&
                !string.IsNullOrWhiteSpace(ChatManager.CurrentChatInput) &&
                (Event.current.keyCode == KeyCode.Return ||
                 Event.current.keyCode == KeyCode.KeypadEnter))
            {
                ChatManager.SendMessage(ChatManager.CurrentChatInput);
                ChatManager.CurrentChatInput = string.Empty;
                Event.current.Use();
=======
            if (!string.IsNullOrWhiteSpace(ChatManager.CurrentChatInput) &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            {
                ChatManager.SendMessage(ChatManager.CurrentChatInput);
                ChatManager.CurrentChatInput = string.Empty;
>>>>>>> 8122f745b7e5c1dd1f5d4e47aa7442f948a0eab5
            }
        }

        private void ScrollToLastMessage()
        {
            scrollPositionChat.y = Mathf.Infinity;
            ClientValues.ToggleChatScroll(false);
        }
    }
}