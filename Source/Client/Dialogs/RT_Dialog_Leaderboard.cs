using GameClient.Managers;
using Shared.Files;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using TCPNetwork.Packets;

namespace GameClient.Dialogs
{
    public class RT_Dialog_Leaderboard : RT_Dialog_Base
    {
        public override Vector2 InitialSize => new Vector2(980f, 560f);

        private Vector2 _scroll = Vector2.zero;
        private int _selectedIndex = -1;

        private const float HeaderHeight = 42f;
        private const float ControlsHeight = 36f;
        private const float FooterHeight = 60f;

        private const float HeaderRowHeight = 30f;
        private const float RowHeight = 26f;

        private const float OuterPadding = 10f;
        private const float InnerPadding = 8f;

        private struct SortOption
        {
            public string Label;
            public InformationData.LeaderboardSortMode Mode;

            public SortOption(string label, InformationData.LeaderboardSortMode mode)
            {
                Label = label;
                Mode = mode;
            }
        }

        private static readonly List<SortOption> SortOptions = new List<SortOption>
        {
            new SortOption("Wealth", InformationData.LeaderboardSortMode.WealthExact),
            new SortOption("Colonists", InformationData.LeaderboardSortMode.Colonists),
            new SortOption("Playtime", InformationData.LeaderboardSortMode.PlaytimeTicks),
            new SortOption("Days", InformationData.LeaderboardSortMode.Days),
            new SortOption("Last Saved", InformationData.LeaderboardSortMode.LastSavedUtcTicks),
        };

        private struct ColumnLayout
        {
            public float Rank;
            public float Player;
            public float Community;
            public float Faction;
            public float Wealth;
            public float Cols;
            public float Days;
            public float Time;
        }

        public RT_Dialog_Leaderboard()
        {
            Title = "Leaderboard";
            closeOnAccept = false;
            closeOnCancel = false;

            if (LeaderboardManager.Entries == null || LeaderboardManager.Entries.Length == 0)
            {
                LeaderboardManager.AskForLeaderboard(
                    LeaderboardManager.CurrentSort,
                    LeaderboardManager.CurrentOrder,
                    LeaderboardManager.CurrentLimit,
                    LeaderboardManager.CurrentOffset);
            }
        }

        public override void DoWindowContents(Rect rect)
        {
            DrawHeader(rect);

            Rect controls = new Rect(0f, HeaderHeight, rect.width, ControlsHeight);
            DrawControls(controls);

            Rect outer = new Rect(
                0f,
                HeaderHeight + ControlsHeight,
                rect.width,
                rect.height - HeaderHeight - ControlsHeight - FooterHeight);

            Widgets.DrawMenuSection(outer);

            Rect inner = outer.ContractedBy(OuterPadding);
            if (inner.width <= 30f || inner.height <= 30f)
            {
                DrawFooter(new Rect(0f, rect.height - FooterHeight, rect.width, FooterHeight), Array.Empty<LeaderboardEntryFile>());
                return;
            }

            LeaderboardEntryFile[] entries = LeaderboardManager.Entries ?? Array.Empty<LeaderboardEntryFile>();

            float viewW = Mathf.Max(1f, inner.width - 16f);
            float viewH = Mathf.Max(inner.height, HeaderRowHeight + 2f + (entries.Length * RowHeight) + 10f);

            Rect viewRect = new Rect(0f, 0f, viewW, viewH);

            Widgets.BeginScrollView(inner, ref _scroll, viewRect);
            try
            {
                float y = 0f;

                Rect headerRow = new Rect(0f, y, viewRect.width, HeaderRowHeight);
                DrawHeaderRow(headerRow);
                y += HeaderRowHeight + 2f;

                if (entries.Length == 0)
                {
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.UpperCenter;
                    Widgets.Label(new Rect(0f, y + 12f, viewRect.width, 30f),
                        "<color=grey>No leaderboard data yet (save a map to generate stats).</color>");
                    Text.Anchor = TextAnchor.UpperLeft;
                }
                else
                {
                    float yMin = _scroll.y - RowHeight;
                    float yMax = _scroll.y + inner.height + RowHeight;

                    ColumnLayout cols = GetColumnLayout(viewRect.width);

                    for (int i = 0; i < entries.Length; i++)
                    {
                        float rowY = y + (i * RowHeight);
                        if (rowY < yMin || rowY > yMax) continue;

                        Rect row = new Rect(0f, rowY, viewRect.width, RowHeight);

                        if (i % 2 == 0) Widgets.DrawAltRect(row);

                        if (_selectedIndex == i)
                            Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.06f));

                        Widgets.DrawHighlightIfMouseover(row);

                        int rank = LeaderboardManager.CurrentOffset + i + 1;
                        DrawEntryRow(row, cols, entries[i], rank);

                        if (Widgets.ButtonInvisible(row))
                            _selectedIndex = i;
                    }
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }

            Rect footer = new Rect(0f, rect.height - FooterHeight, rect.width, FooterHeight);
            DrawFooter(footer, entries);
        }

        private void DrawHeader(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;

            Rect titleRect = new Rect(0f, 0f, rect.width, HeaderHeight);
            Widgets.Label(titleRect, Title);

            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            Widgets.DrawLineHorizontal(0f, HeaderHeight - 1f, rect.width);
        }

        private void DrawControls(Rect rect)
        {
            float pad = 6f;
            float btnH = 26f;

            float y = rect.y + Mathf.Max(0f, (rect.height - btnH) * 0.5f);

            float refreshW = Mathf.Clamp(120f, 90f, rect.width * 0.22f);
            Rect refreshBtn = new Rect(rect.xMax - pad - refreshW, y, refreshW, btnH);

            float leftW = refreshBtn.xMin - (rect.x + pad) - pad;
            leftW = Mathf.Max(0f, leftW);

            float spacing = 6f;

            float wEach = (leftW - (spacing * 2f)) / 3f;
            float sortW = Mathf.Clamp(wEach, 90f, 260f);
            float orderW = Mathf.Clamp(wEach, 90f, 220f);
            float topW = Mathf.Clamp(wEach, 90f, 160f);

            float required = sortW + orderW + topW + (spacing * 2f);
            if (required > leftW && required > 0.01f)
            {
                float scale = leftW / required;
                sortW *= scale;
                orderW *= scale;
                topW *= scale;
            }

            float x = rect.x + pad;

            Rect sortBtn = new Rect(x, y, sortW, btnH);
            x += sortBtn.width + spacing;

            Rect orderBtn = new Rect(x, y, orderW, btnH);
            x += orderBtn.width + spacing;

            Rect topBtn = new Rect(x, y, topW, btnH);

            string sortLabel = $"Sort: {FriendlySort(LeaderboardManager.CurrentSort)}";
            string orderLabel = $"Order: {FriendlyOrder(LeaderboardManager.CurrentOrder)}";
            string topLabel = $"Top: {LeaderboardManager.CurrentLimit}";

            if (Widgets.ButtonText(sortBtn, sortLabel))
                OpenSortMenu();

            if (Widgets.ButtonText(orderBtn, orderLabel))
                OpenOrderMenu();

            if (Widgets.ButtonText(topBtn, topLabel))
                OpenLimitMenu();

            if (Widgets.ButtonText(refreshBtn, "Refresh"))
            {
                _selectedIndex = -1;
                _scroll = Vector2.zero;

                LeaderboardManager.AskForLeaderboard(
                    LeaderboardManager.CurrentSort,
                    LeaderboardManager.CurrentOrder,
                    LeaderboardManager.CurrentLimit,
                    LeaderboardManager.CurrentOffset);
            }
        }

        private void OpenSortMenu()
        {
            List<FloatMenuOption> opts = new List<FloatMenuOption>();

            foreach (SortOption opt in SortOptions)
            {
                InformationData.LeaderboardSortMode mode = opt.Mode;
                bool isCurrent = FriendlySort(mode) == FriendlySort(LeaderboardManager.CurrentSort);
                string label = isCurrent ? $"✓ {opt.Label}" : opt.Label;

                opts.Add(new FloatMenuOption(label, () =>
                {
                    _selectedIndex = -1;
                    _scroll = Vector2.zero;

                    LeaderboardManager.AskForLeaderboard(
                        mode,
                        LeaderboardManager.CurrentOrder,
                        LeaderboardManager.CurrentLimit,
                        0);
                }));
            }

            Find.WindowStack.Add(new FloatMenu(opts));
        }

        private void OpenOrderMenu()
        {
            List<FloatMenuOption> opts = new List<FloatMenuOption>();

            bool isDesc = LeaderboardManager.CurrentOrder == InformationData.LeaderboardOrder.Desc;
            bool isAsc = LeaderboardManager.CurrentOrder == InformationData.LeaderboardOrder.Asc;

            opts.Add(new FloatMenuOption(isDesc ? "✓ High → Low" : "High → Low", () =>
            {
                _selectedIndex = -1;
                _scroll = Vector2.zero;

                LeaderboardManager.AskForLeaderboard(
                    LeaderboardManager.CurrentSort,
                    InformationData.LeaderboardOrder.Desc,
                    LeaderboardManager.CurrentLimit,
                    0);
            }));

            opts.Add(new FloatMenuOption(isAsc ? "✓ Low → High" : "Low → High", () =>
            {
                _selectedIndex = -1;
                _scroll = Vector2.zero;

                LeaderboardManager.AskForLeaderboard(
                    LeaderboardManager.CurrentSort,
                    InformationData.LeaderboardOrder.Asc,
                    LeaderboardManager.CurrentLimit,
                    0);
            }));

            Find.WindowStack.Add(new FloatMenu(opts));
        }

        private void OpenLimitMenu()
        {
            List<int> limits = new List<int> { 10, 25, 50, 100 };
            List<FloatMenuOption> opts = new List<FloatMenuOption>();

            foreach (int l in limits)
            {
                int captured = l;
                bool isCurrent = LeaderboardManager.CurrentLimit == captured;

                opts.Add(new FloatMenuOption(isCurrent ? $"✓ {captured}" : captured.ToString(), () =>
                {
                    _selectedIndex = -1;
                    _scroll = Vector2.zero;

                    LeaderboardManager.AskForLeaderboard(
                        LeaderboardManager.CurrentSort,
                        LeaderboardManager.CurrentOrder,
                        captured,
                        0);
                }));
            }

            Find.WindowStack.Add(new FloatMenu(opts));
        }

        private void DrawFooter(Rect rect, LeaderboardEntryFile[] entriesOnScreen)
        {
            int total = LeaderboardManager.Total;
            int offset = LeaderboardManager.CurrentOffset;
            int count = entriesOnScreen?.Length ?? 0;

            int start = total <= 0 ? 0 : Math.Min(total, offset + 1);
            int end = total <= 0 ? 0 : Math.Min(total, offset + count);

            string rangeText = total < 0 ? "Showing ?-? of ?" : $"Showing {start}-{end} of {total}";
            string detailText = rangeText;

            if (_selectedIndex >= 0 && entriesOnScreen != null && _selectedIndex < entriesOnScreen.Length)
            {
                LeaderboardEntryFile e = entriesOnScreen[_selectedIndex] ?? new LeaderboardEntryFile();

                string player = string.IsNullOrWhiteSpace(e.Username) ? "Unknown" : e.Username;
                string tile = e.Tile < 0 ? "?" : e.Tile.ToString();
                string saved = FormatUtcTicks(e.LastSavedUtcTicks);
                string wealth = FormatWealth(e);
                string cols = e.ColonistCount < 0 ? "?" : e.ColonistCount.ToString();
                string days = FormatDays(e.GameTicks);
                string play = FormatTime(e);

                detailText = $"{rangeText} | {player} | Tile {tile} | Wealth {wealth} | Cols {cols} | Days {days} | Playtime {play} | Saved {saved}";
            }

            float pad = 10f;
            float spacing = 6f;
            float btnH = 30f;

            float btnW = 120f;
            float maxBtnW = (rect.width - (pad * 2f) - (spacing * 2f)) / 3f;
            if (btnW > maxBtnW) btnW = Mathf.Max(80f, maxBtnW);

            float totalBtnsW = (btnW * 3f) + (spacing * 2f);

            Rect okBtn = new Rect(rect.xMax - pad - btnW, rect.y + (rect.height - btnH) * 0.5f, btnW, btnH);
            Rect nextBtn = new Rect(okBtn.xMin - spacing - btnW, okBtn.y, btnW, btnH);
            Rect prevBtn = new Rect(nextBtn.xMin - spacing - btnW, okBtn.y, btnW, btnH);

            float labelW = Mathf.Max(80f, rect.width - (pad * 2f) - totalBtnsW - spacing);
            Rect leftLabel = new Rect(rect.x + pad, rect.y + 8f, labelW, rect.height - 16f);

            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Small;

            string labelText = (leftLabel.width < 220f) ? rangeText : detailText;
            Widgets.LabelEllipses(leftLabel, labelText);

            Text.Anchor = TextAnchor.UpperLeft;

            bool canPrev = LeaderboardManager.CanPagePrev();
            bool canNext = LeaderboardManager.CanPageNext();

            GUI.color = canPrev ? Color.white : Color.gray;
            if (Widgets.ButtonText(prevBtn, "Prev") && canPrev)
            {
                _selectedIndex = -1;
                LeaderboardManager.PrevPage();
            }

            GUI.color = canNext ? Color.white : Color.gray;
            if (Widgets.ButtonText(nextBtn, "Next") && canNext)
            {
                _selectedIndex = -1;
                LeaderboardManager.NextPage();
            }

            GUI.color = Color.white;
            if (Widgets.ButtonText(okBtn, "OK"))
            {
                Close();
            }

            GUI.color = Color.white;
        }

        private ColumnLayout GetColumnLayout(float totalWidth)
        {
            float pad = InnerPadding;
            float available = Mathf.Max(1f, totalWidth - (pad * 2f));

            float wRank = 40f;
            float wWealth = 120f;
            float wCols = 55f;
            float wDays = 55f;
            float wTime = 80f;

            float fixedW = wRank + wWealth + wCols + wDays + wTime;

            float minPlayer = 130f;
            float minCommunity = 150f;
            float minFaction = 150f;
            float minTextW = minPlayer + minCommunity + minFaction;

            float remaining = available - fixedW;

            float wPlayer = minPlayer;
            float wCommunity = minCommunity;
            float wFaction = minFaction;

            if (remaining <= 1f)
            {
                float tiny = Mathf.Max(40f, (available - fixedW) / 3f);
                wPlayer = tiny;
                wCommunity = tiny;
                wFaction = tiny;
            }
            else if (remaining < minTextW)
            {
                float scale = Mathf.Clamp01(remaining / Mathf.Max(1f, minTextW));
                scale = Mathf.Max(0.35f, scale);

                wPlayer = Mathf.Max(60f, minPlayer * scale);
                wCommunity = Mathf.Max(70f, minCommunity * scale);
                wFaction = Mathf.Max(70f, minFaction * scale);

                float sum = wPlayer + wCommunity + wFaction;
                float target = Mathf.Max(1f, remaining);
                if (sum > target)
                {
                    float s = target / sum;
                    wPlayer *= s;
                    wCommunity *= s;
                    wFaction *= s;
                }
            }
            else
            {
                float extra = remaining - minTextW;

                wPlayer = minPlayer + (extra * 0.30f);
                wCommunity = minCommunity + (extra * 0.35f);
                wFaction = minFaction + (extra * 0.35f);
            }

            float total = fixedW + wPlayer + wCommunity + wFaction;
            if (total > available)
            {
                float over = total - available;
                float reducible = Mathf.Max(1f, wPlayer + wCommunity + wFaction);
                float ratio = over / reducible;

                wPlayer = Mathf.Max(50f, wPlayer - (wPlayer * ratio));
                wCommunity = Mathf.Max(60f, wCommunity - (wCommunity * ratio));
                wFaction = Mathf.Max(60f, wFaction - (wFaction * ratio));
            }

            return new ColumnLayout
            {
                Rank = wRank,
                Player = wPlayer,
                Community = wCommunity,
                Faction = wFaction,
                Wealth = wWealth,
                Cols = wCols,
                Days = wDays,
                Time = wTime
            };
        }

        private void DrawHeaderRow(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0f, 0f, 0f, 0.18f));
            Widgets.DrawLineHorizontal(rect.x, rect.yMax - 1f, rect.width);

            ColumnLayout c = GetColumnLayout(rect.width);

            float x = rect.x + InnerPadding;

            Text.Font = GameFont.Small;

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(x, rect.y, c.Rank, rect.height), "#"); x += c.Rank;
            Widgets.Label(new Rect(x, rect.y, c.Player, rect.height), "Player"); x += c.Player;
            Widgets.Label(new Rect(x, rect.y, c.Community, rect.height), "Community"); x += c.Community;
            Widgets.Label(new Rect(x, rect.y, c.Faction, rect.height), "Faction"); x += c.Faction;

            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(x, rect.y, c.Wealth, rect.height), "Wealth"); x += c.Wealth;
            Widgets.Label(new Rect(x, rect.y, c.Cols, rect.height), "Cols"); x += c.Cols;
            Widgets.Label(new Rect(x, rect.y, c.Days, rect.height), "Days"); x += c.Days;
            Widgets.Label(new Rect(x, rect.y, c.Time, rect.height), "Time");

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawEntryRow(Rect rect, ColumnLayout c, LeaderboardEntryFile e, int rank)
        {
            e ??= new LeaderboardEntryFile();

            string player = string.IsNullOrWhiteSpace(e.Username) ? "Unknown" : e.Username;
            string community = string.IsNullOrWhiteSpace(e.SettlementName) ? "Unknown" : e.SettlementName;
            string faction = string.IsNullOrWhiteSpace(e.FactionName) ? "Unknown" : e.FactionName;

            string wealth = FormatWealth(e);
            string cols = e.ColonistCount < 0 ? "?" : e.ColonistCount.ToString();
            string days = FormatDays(e.GameTicks);
            string time = FormatTime(e);

            float x = rect.x + InnerPadding;

            Rect rRank = new Rect(x, rect.y, c.Rank, rect.height); x += c.Rank;
            Rect rPlayer = new Rect(x, rect.y, c.Player, rect.height); x += c.Player;
            Rect rCommunity = new Rect(x, rect.y, c.Community, rect.height); x += c.Community;
            Rect rFaction = new Rect(x, rect.y, c.Faction, rect.height); x += c.Faction;

            Rect rWealth = new Rect(x, rect.y, c.Wealth, rect.height); x += c.Wealth;
            Rect rCols = new Rect(x, rect.y, c.Cols, rect.height); x += c.Cols;
            Rect rDays = new Rect(x, rect.y, c.Days, rect.height); x += c.Days;
            Rect rTime = new Rect(x, rect.y, c.Time, rect.height);

            Text.Font = GameFont.Small;

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rRank, rank.ToString());
            Widgets.LabelEllipses(rPlayer.ContractedBy(2f, 0f), player);
            Widgets.LabelEllipses(rCommunity.ContractedBy(2f, 0f), community);
            Widgets.LabelEllipses(rFaction.ContractedBy(2f, 0f), faction);

            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(rWealth, wealth);
            Widgets.Label(rCols, cols);
            Widgets.Label(rDays, days);
            Widgets.Label(rTime, time);

            Text.Anchor = TextAnchor.UpperLeft;

            string tip =
                $"Player: {player}\n" +
                $"Community: {community}\n" +
                $"Faction: {faction}\n" +
                $"Tile: {(e.Tile < 0 ? "?" : e.Tile.ToString())}\n" +
                $"Wealth: {wealth}\n" +
                $"Colonists: {cols}\n" +
                $"Days: {days}\n" +
                $"Playtime: {time}\n" +
                $"Last Saved: {FormatUtcTicks(e.LastSavedUtcTicks)}";

            TooltipHandler.TipRegion(rect, tip);
        }

        private static string FriendlySort(InformationData.LeaderboardSortMode mode)
        {
            if (mode == InformationData.LeaderboardSortMode.Wealth || mode == InformationData.LeaderboardSortMode.WealthExact)
                return "Wealth";

            switch (mode)
            {
                case InformationData.LeaderboardSortMode.Colonists: return "Colonists";
                case InformationData.LeaderboardSortMode.PlaytimeTicks: return "Playtime";
                case InformationData.LeaderboardSortMode.Days: return "Days";
                case InformationData.LeaderboardSortMode.LastSavedUtcTicks: return "Last Saved";
                default: return mode.ToString();
            }
        }

        private static string FriendlyOrder(InformationData.LeaderboardOrder order)
        {
            return order == InformationData.LeaderboardOrder.Desc ? "High → Low" : "Low → High";
        }

        private static string FormatWealth(LeaderboardEntryFile e)
        {
            if (e == null) return "?";
            if (e.WealthExact >= 0) return "$" + e.WealthExact.ToString("N2");
            if (e.Wealth >= 0) return "$" + e.Wealth.ToString("N0");
            return "?";
        }

        private static string FormatDays(int ticks)
        {
            if (ticks < 0) return "?";
            int days = ticks / 60000;
            return days.ToString("N0");
        }

        private static string FormatTime(LeaderboardEntryFile e)
        {
            if (e == null) return "?";

            double seconds = e.RealPlayTimeSeconds >= 0 ? e.RealPlayTimeSeconds : e.RealPlayTimeInteractingSeconds;

            if (seconds >= 0)
            {
                try
                {
                    TimeSpan ts = TimeSpan.FromSeconds(seconds);
                    int hours = (int)Math.Floor(ts.TotalHours);
                    int minutes = ts.Minutes;
                    return $"{hours}h {minutes}m";
                }
                catch
                {
                }
            }

            if (e.GameTicks < 0) return "?";

            int days = e.GameTicks / 60000;
            int remainder = e.GameTicks - (days * 60000);

            int hours2 = remainder / 2500;
            remainder -= hours2 * 2500;

            double mins = remainder / (2500d / 60d);
            int minutes2 = (int)Math.Round(mins);

            if (minutes2 >= 60) { minutes2 = 0; hours2++; }
            if (hours2 >= 24) { hours2 = 0; days++; }

            return $"{hours2}h {minutes2}m";
        }

        private static string FormatUtcTicks(long utcTicks)
        {
            if (utcTicks <= 0) return "Unknown";

            try
            {
                DateTime dt = new DateTime(utcTicks, DateTimeKind.Utc);
                return dt.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}