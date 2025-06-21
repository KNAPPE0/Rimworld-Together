using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using Shared.Packets.Data;
using GameClient.Managers;

namespace GameClient.Patches.Tabs
{
    public class ChatTab : MainTabWindow
    {
        private enum TabMode { Chat, Leaderboard, Settings }
        private TabMode currentTab = TabMode.Chat;

        private const int   DefaultTopN  = 10;
        private const float TopBarH      = 36f;
        private const float InputBarH    = 48f;
        private const float Pad          = 6f;
        private const float MinWidth     = 600f;
        private const float MinHeight    = 400f;
        private const float ScrollbarW   = 16f;

        private bool requestedLeaderboard = false;
        private int  topN                 = DefaultTopN;

        private Vector2 scrollPlayers     = Vector2.zero;
        private Vector2 scrollMessages    = Vector2.zero;
        private Vector2 scrollLeaderboard = Vector2.zero;

        private string filterText = "";
        private readonly List<string> inputHistory = new List<string>();
        private int historyIndex = -1;

        // Temporary settings
        private string tmpFontColor;
        private string tmpBackgroundColor;
        private string tmpFontSize;
        private string tmpTopNStr;
        private string tmpWindowWidthStr;
        private string tmpWindowHeightStr;
        private readonly string[] fontSizes = { "Small", "Medium", "Large" };

        public ChatTab()
        {
            layer         = WindowLayer.GameUI;
            draggable     = true;
            closeOnAccept = false;
            closeOnCancel = true;

            var s = ChatCustomizationManager.Settings;
            tmpFontColor       = s.FontColor;
            tmpBackgroundColor = s.BackgroundColor;
            tmpFontSize        = string.IsNullOrEmpty(s.FontSize) ? "Medium" : s.FontSize;
            tmpTopNStr         = DefaultTopN.ToString();
            tmpWindowWidthStr  = Mathf.RoundToInt(s.WindowWidth).ToString();
            tmpWindowHeightStr = Mathf.RoundToInt(s.WindowHeight).ToString();
        }

        public override Vector2 RequestedTabSize
        {
            get
            {
                var s = ChatCustomizationManager.Settings;
                float w = s.WindowWidth, h = s.WindowHeight;
                if (float.TryParse(tmpWindowWidthStr,  out var pw)) w = pw;
                if (float.TryParse(tmpWindowHeightStr, out var ph)) h = ph;
                return new Vector2(Mathf.Max(w, MinWidth), Mathf.Max(h, MinHeight));
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            var s = ChatCustomizationManager.Settings;
            bool hasBg = ColorUtility.TryParseHtmlString(s.BackgroundColor, out var bg) && bg.a >= 0.1f;
            bool hasFg = ColorUtility.TryParseHtmlString(s.FontColor,       out var fg) && fg.a >= 0.1f;
            if (!hasFg) fg = Color.white;

            GUI.color = Color.white;
            DrawTopBar(new Rect(inRect.x, inRect.y, inRect.width, TopBarH));
            if (currentTab == TabMode.Chat)
                DrawInputBar(new Rect(inRect.x, inRect.yMax - InputBarH, inRect.width, InputBarH));

            float bottom = currentTab == TabMode.Chat ? InputBarH : 0f;
            var body = new Rect(inRect.x, inRect.y + TopBarH, inRect.width, inRect.height - TopBarH - bottom);

            if (hasBg)
            {
                var prev = GUI.color; GUI.color = bg;
                GUI.DrawTexture(body, Texture2D.whiteTexture);
                GUI.color = prev;
            }

            GUI.color = hasFg ? fg : Color.white;
            Widgets.DrawBox(body);

            switch (currentTab)
            {
                case TabMode.Chat:        DrawChatBody(body);        break;
                case TabMode.Leaderboard: DrawLeaderboardBody(body); break;
                case TabMode.Settings:    DrawSettingsBody(body);    break;
            }

            GUI.color = Color.white;
        }

        private void DrawTopBar(Rect r)
        {
            Text.Font = GameFont.Small;
            float x = r.x + Pad, y = r.y + Pad, h = r.height - 2*Pad;

            if (Widgets.ButtonText(new Rect(x, y,  80, h), "Chat"))        currentTab = TabMode.Chat;
            x += 84;
            if (Widgets.ButtonText(new Rect(x, y, 100, h), "Leaderboard"))
            {
                currentTab = TabMode.Leaderboard;
                if (!requestedLeaderboard)
                {
                    LeaderboardManager.Request(int.Parse(tmpTopNStr));
                    requestedLeaderboard = true;
                }
            }
            x += 104;
            if (Widgets.ButtonText(new Rect(x, y,  80, h), "Settings"))    currentTab = TabMode.Settings;
            x += 84;
            if (currentTab == TabMode.Chat && Widgets.ButtonText(new Rect(x, y,  60, h), "Clear"))
                ChatManager.CleanChat();

            filterText = Widgets.TextField(new Rect(r.xMax - Pad - 200, y, 200, h), filterText);
        }

        private void DrawInputBar(Rect r)
        {
            var s = ChatCustomizationManager.Settings;
            Text.Font = s.FontSize switch {
                "Small"  => GameFont.Tiny,
                "Medium" => GameFont.Small,
                "Large"  => GameFont.Medium,
                _        => GameFont.Small
            };

            float h = r.height - 2*Pad;
            var inR = new Rect(r.x + Pad, r.y + Pad, r.width - 220 - 3*Pad, h);
            HandleInput(inR);

            var btnR = new Rect(inR.xMax + Pad, r.y + Pad, 80, h);
            if (Widgets.ButtonText(btnR, "Send"))
            {
                var txt = ChatManager.CurrentChatInput.Trim();
                if (!txt.NullOrEmpty())
                {
                    ChatManager.SendMessage(txt);
                    inputHistory.Add(txt);
                    historyIndex = inputHistory.Count;
                    ChatManager.CurrentChatInput = "";
                }
            }

            const string cb = "Auto Scroll";
            float cbW = Text.CalcSize(cb).x + 24f;
            Widgets.CheckboxLabeled(
                new Rect(btnR.xMax + Pad, r.y + Pad, cbW, h),
                cb, ref ChatManager.ChatAutoscroll
            );
        }

        private void DrawChatBody(Rect body)
        {
            // Header
            Text.Font = GameFont.Small;
            var oldA = Text.Anchor;
            float titleY = body.y + Pad;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(
                new Rect(body.x + Pad, titleY, body.width - 2*Pad, Text.LineHeight),
                $"{RecountManager.CurrentPlayers} Players Online"
            );
            Text.Anchor = oldA;

            // Divider + split
            float splitY = titleY + Text.LineHeight + Pad;
            Widgets.DrawLineHorizontal(body.x + Pad, splitY, body.width - 2*Pad);
            float leftW  = (body.width - 2*Pad) * .25f;
            float splitX = body.x + Pad + leftW;
            Widgets.DrawLineVertical(splitX, splitY, body.yMax - splitY - Pad);

            // Player list
            var pR    = new Rect(body.x + Pad, splitY + Pad, leftW, body.yMax - splitY - 2*Pad);
            var names = RecountManager.CurrentPlayerNames.OrderBy(n => n).ToList();
            float prowH = Text.LineHeight + 4f;
            var pView  = new Rect(0, 0, pR.width - ScrollbarW, names.Count * prowH);

            Widgets.BeginScrollView(pR, ref scrollPlayers, pView);
            {
                float py = 0f;
                Text.Font = GameFont.Small;
                foreach (var n in names)
                {
                    var row = new Rect(0, py, pView.width, prowH);
                    row.x += Pad; row.width -= 2*Pad;
                    Widgets.DrawHighlightIfMouseover(row);
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(row, n);
                    if (Widgets.ButtonInvisible(row) && Event.current.button == 0)
                        ChatManager.CurrentChatInput += $"@{n} ";
                    Text.Anchor = oldA;
                    py += prowH;
                }
            }
            Widgets.EndScrollView();

            // Chat messages — outRect is full width, viewRect leaves ScrollbarW on the right so the bar never overlaps text
            var mR = new Rect(
                splitX + Pad,
                splitY + Pad,
                body.width - leftW - 2*Pad,
                body.yMax - splitY - 2*Pad
            );

            var msgs = string.IsNullOrWhiteSpace(filterText)
                ? ChatManager.ChatMessageCache
                : ChatManager.ChatMessageCache
                      .Where(m => m.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0)
                      .ToList();

            float totalH = msgs.Sum(m => Text.CalcHeight(m, mR.width - ScrollbarW)) + 8f;
            var mView = new Rect(0, 0, mR.width - ScrollbarW, totalH);

            // manual wheel — inverted so wheel-down moves down
            if (!ChatManager.ChatAutoscroll
                && Event.current.type == EventType.ScrollWheel
                && mR.Contains(Event.current.mousePosition))
            {
                scrollMessages.y = Mathf.Clamp(
                    scrollMessages.y + Event.current.delta.y * 20f,
                    0f,
                    Mathf.Max(0f, mView.height - mR.height)
                );
                Event.current.Use();
            }
            // auto-scroll
            if (ChatManager.ChatAutoscroll)
                scrollMessages.y = Mathf.Max(0f, mView.height - mR.height);

            Widgets.BeginScrollView(mR, ref scrollMessages, mView);
            {
                float my = 0f;
                Text.Font = ChatCustomizationManager.Settings.FontSize switch {
                    "Small"  => GameFont.Tiny,
                    "Medium" => GameFont.Small,
                    "Large"  => GameFont.Medium,
                    _        => GameFont.Small
                };
                foreach (var msg in msgs)
                {
                    float mh = Text.CalcHeight(msg, mView.width);
                    var row = new Rect(0, my, mView.width, mh);
                    row.x += Pad; row.width -= 2*Pad;
                    Widgets.DrawHighlightIfMouseover(row);
                    Widgets.Label(row, msg);
                    my += mh;
                }
            }
            Widgets.EndScrollView();
        }

        private void DrawLeaderboardBody(Rect body)
        {
            Text.Font = GameFont.Medium;
            float ty = body.y + Pad;
            Widgets.Label(
                new Rect(body.x + Pad, ty, body.width - 2*Pad, Text.LineHeight),
                "Leaderboard"
            );

            var allRows = LeaderboardManager.Rows;
            if (!requestedLeaderboard || allRows == null || !allRows.Any())
            {
                Widgets.Label(
                    new Rect(body.x + Pad, ty + Text.LineHeight + Pad, body.width - 2*Pad, Text.LineHeight),
                    "Loading…"
                );
                return;
            }

            var rows = allRows.Take(topN).ToList();
            string[] headers = { "Player", "Wealth", "Colonists", "Playtime", "Days" };
            int cols = headers.Length;

            float contentW    = body.width - 2*Pad;
            float columnSpace = contentW - ScrollbarW;
            float[] widths    = new float[cols];
            Text.Font = GameFont.Small;

            // Measure
            for (int i = 0; i < cols; i++)
                widths[i] = Text.CalcSize(headers[i]).x + 2*Pad;
            foreach (var sd in rows)
            {
                var cells = new[] {
                    sd._uid,
                    $"${sd._wealth:N0}",
                    sd._colonistCount.ToString(),
                    FormatPlaytime(sd._playtimeSeconds),
                    sd._daysPassed.ToString()
                };
                for (int i = 0; i < cols; i++)
                    widths[i] = Mathf.Max(widths[i], Text.CalcSize(cells[i]).x + 2*Pad);
            }
            widths[4] = Mathf.Max(widths[4], Text.CalcSize("9999").x + 2*Pad);

            float sumW = widths.Sum();
            if (sumW > columnSpace)
            {
                float f = columnSpace / sumW;
                for (int i = 0; i < cols; i++) widths[i] *= f;
            }
            else
            {
                float extra = (columnSpace - sumW) / cols;
                for (int i = 0; i < cols; i++) widths[i] += extra;
            }

            float headerY = ty + Text.LineHeight + Pad;
            float rowH    = Text.LineHeight + 6f;
            float tableH  = rowH * (rows.Count + 1);

            // Vertical lines
            float x0 = body.x + Pad;
            for (int c = 0; c < cols; c++)
            {
                Widgets.DrawLineVertical(x0, headerY, tableH);
                x0 += widths[c];
            }
            Widgets.DrawLineVertical(body.x + Pad + contentW, headerY, tableH);

            // Horizontal lines
            for (int r = 0; r <= rows.Count + 1; r++)
                Widgets.DrawLineHorizontal(body.x + Pad, headerY + r*rowH, contentW);

            // Headers centered
            Text.Anchor = TextAnchor.MiddleCenter;
            x0 = body.x + Pad;
            for (int i = 0; i < cols; i++)
            {
                Widgets.Label(new Rect(x0, headerY, widths[i], rowH), headers[i]);
                x0 += widths[i];
            }

            // Data rows scroll
            var oldA = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;

            var scrollRect = new Rect(body.x + Pad, headerY + rowH, contentW, body.yMax - (headerY + rowH) - Pad);
            var viewRect   = new Rect(0, 0, columnSpace, rows.Count*rowH);

            // manual wheel — inverted
            if (Event.current.type == EventType.ScrollWheel && scrollRect.Contains(Event.current.mousePosition))
            {
                scrollLeaderboard.y = Mathf.Clamp(
                    scrollLeaderboard.y + Event.current.delta.y * 20f,
                    0f,
                    Mathf.Max(0f, viewRect.height - scrollRect.height)
                );
                Event.current.Use();
            }

            Widgets.BeginScrollView(scrollRect, ref scrollLeaderboard, viewRect);
            {
                float yy = 0f;
                foreach (var sd in rows)
                {
                    x0 = 0f;
                    var cells = new[] {
                        sd._uid,
                        $"${sd._wealth:N0}",
                        sd._colonistCount.ToString(),
                        FormatPlaytime(sd._playtimeSeconds),
                        sd._daysPassed.ToString()
                    };
                    for (int i = 0; i < cols; i++)
                    {
                        Widgets.Label(new Rect(x0, yy, widths[i], rowH), cells[i]);
                        x0 += widths[i];
                    }
                    yy += rowH;
                }
            }
            Widgets.EndScrollView();
            Text.Anchor = oldA;
        }

        private void DrawSettingsBody(Rect body)
        {
            Text.Font = GameFont.Medium;
            float ty = body.y + Pad;
            Widgets.Label(
                new Rect(body.x + Pad, ty, body.width - 2*Pad, Text.LineHeight),
                "Settings"
            );

            Text.Font = GameFont.Small;
            float y = ty + Text.LineHeight + Pad;
            void Field(string lbl, ref string val, float w = 200f)
            {
                Widgets.Label(new Rect(body.x + Pad, y, 140,24), lbl);
                var prev = GUI.color; GUI.color = Color.white;
                val = Widgets.TextField(new Rect(body.x + Pad + 150, y, w,24), val);
                GUI.color = prev;
                y += 32f;
            }

            Field("Font Color (hex):",   ref tmpFontColor);
            Field("Background Color:",   ref tmpBackgroundColor);

            Widgets.Label(new Rect(body.x + Pad, y,140,24), "Font Size:");
            float btnW = 80f;
            for (int i = 0; i < fontSizes.Length; i++)
            {
                var fs = fontSizes[i];
                var br = new Rect(body.x + Pad + 150 + i*(btnW+Pad), y, btnW,24f);
                if (Widgets.ButtonText(br, fs)) tmpFontSize = fs;
                if (tmpFontSize == fs)      Widgets.DrawHighlight(br);
            }
            y += 32f;

            Field("Leaderboard Count:", ref tmpTopNStr,       80f);
            Field("Window Width:",      ref tmpWindowWidthStr,80f);
            Field("Window Height:",     ref tmpWindowHeightStr,80f);

            if (Widgets.ButtonText(new Rect(body.x + Pad, y,100,30), "Apply"))
            {
                var s = ChatCustomizationManager.Settings;
                s.FontColor       = tmpFontColor;
                s.BackgroundColor = tmpBackgroundColor;
                s.FontSize        = tmpFontSize;
                if (int.TryParse(tmpTopNStr, out var n) && n >= 0)
                {
                    topN = n;
                    requestedLeaderboard = false;
                    LeaderboardManager.Request(topN);
                }
                if (float.TryParse(tmpWindowWidthStr,  out var pw)) s.WindowWidth  = Mathf.Max(pw, MinWidth);
                if (float.TryParse(tmpWindowHeightStr, out var ph)) s.WindowHeight = Mathf.Max(ph, MinHeight);
                ChatCustomizationManager.SaveSettings();
                windowRect.size = RequestedTabSize;
            }
        }

        private void HandleInput(Rect r)
        {
            if (Event.current.type == EventType.KeyDown)
            {
                switch (Event.current.keyCode)
                {
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                        var txt = ChatManager.CurrentChatInput.Trim();
                        if (!txt.NullOrEmpty())
                        {
                            ChatManager.SendMessage(txt);
                            inputHistory.Add(txt);
                            historyIndex = inputHistory.Count;
                            ChatManager.CurrentChatInput = "";
                        }
                        Event.current.Use();
                        return;
                    case KeyCode.UpArrow when inputHistory.Any():
                        historyIndex = historyIndex <= 0 ? inputHistory.Count - 1 : historyIndex - 1;
                        ChatManager.CurrentChatInput = inputHistory[historyIndex];
                        Event.current.Use();
                        return;
                    case KeyCode.DownArrow when inputHistory.Any():
                        historyIndex = historyIndex >= inputHistory.Count - 1 ? 0 : historyIndex + 1;
                        ChatManager.CurrentChatInput = inputHistory[historyIndex];
                        Event.current.Use();
                        return;
                }
            }

            var newText = Widgets.TextField(r, ChatManager.CurrentChatInput);
            if (newText.Length <= 512)
                ChatManager.CurrentChatInput = newText;
        }

        // days-hours-minutes-seconds formatting
        private static string FormatPlaytime(double totalSeconds)
        {
            var ts = TimeSpan.FromSeconds(totalSeconds);
            int d = ts.Days, h = ts.Hours, m = ts.Minutes, s = ts.Seconds;
            var parts = new List<string>();
            if (d>0) parts.Add($"{d}d");
            if (h>0) parts.Add($"{h}h");
            if (m>0) parts.Add($"{m}m");
            parts.Add($"{s}s");
            return string.Join("", parts);
        }
    }
}