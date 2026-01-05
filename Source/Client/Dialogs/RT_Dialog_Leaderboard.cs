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

        private const float HeaderHeight = 38f;
        private const float ControlsHeight = 34f;
        private const float FooterHeight = 58f;

        private const float RowHeight = 26f;
        private const float HeaderRowHeight = 28f;

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
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width, HeaderHeight), Title);

            Text.Font = GameFont.Small;
            Widgets.DrawLineHorizontal(0f, HeaderHeight - 6f, rect.width);

            Rect controls = new Rect(0f, HeaderHeight, rect.width, ControlsHeight);
            DrawControls(controls);

            Rect outer = new Rect(0f, HeaderHeight + ControlsHeight, rect.width,
                rect.height - HeaderHeight - ControlsHeight - FooterHeight);

            Widgets.DrawMenuSection(outer);

            Rect inner = outer.ContractedBy(10f);

            LeaderboardEntryFile[] entries = LeaderboardManager.Entries ?? Array.Empty<LeaderboardEntryFile>();

            float viewH = Math.Max(240f, HeaderRowHeight + 2f + (entries.Length * RowHeight) + 20f);
            Rect viewRect = new Rect(0f, 0f, inner.width - 16f, viewH);

            Widgets.BeginScrollView(inner, ref _scroll, viewRect);

            float y = 0f;

            Rect headerRow = new Rect(0f, y, viewRect.width, HeaderRowHeight);
            DrawHeaderRow(headerRow);
            y += HeaderRowHeight + 2f;

            if (entries.Length == 0)
            {
                Widgets.Label(new Rect(0f, y + 10f, viewRect.width, 30f),
                    "<color=grey>No leaderboard data yet (save a map to generate stats).</color>");
            }
            else
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    Rect row = new Rect(0f, y, viewRect.width, RowHeight);

                    if (i % 2 == 0) Widgets.DrawAltRect(row);

                    if (_selectedIndex == i)
                        Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.06f));

                    Widgets.DrawHighlightIfMouseover(row);

                    int rank = LeaderboardManager.CurrentOffset + i + 1;
                    DrawEntryRow(row, entries[i], rank);

                    if (Widgets.ButtonInvisible(row))
                        _selectedIndex = i;

                    y += RowHeight;
                }
            }

            Widgets.EndScrollView();

            Rect footer = new Rect(0f, rect.height - FooterHeight, rect.width, FooterHeight);
            DrawFooter(footer, entries);
        }

        private void DrawControls(Rect rect)
        {
            float x = rect.x + 6f;
            float y = rect.y + 4f;

            float btnH = 26f;

            Rect sortBtn = new Rect(x, y, 220f, btnH);
            x += sortBtn.width + 6f;

            Rect orderBtn = new Rect(x, y, 170f, btnH);
            x += orderBtn.width + 6f;

            Rect topBtn = new Rect(x, y, 120f, btnH);
            x += topBtn.width + 6f;

            Rect refreshBtn = new Rect(x, y, 120f, btnH);

            if (Widgets.ButtonText(sortBtn, $"Sort: {FriendlySort(LeaderboardManager.CurrentSort)}"))
                OpenSortMenu();

            if (Widgets.ButtonText(orderBtn, $"Order: {FriendlyOrder(LeaderboardManager.CurrentOrder)}"))
                OpenOrderMenu();

            if (Widgets.ButtonText(topBtn, $"Top: {LeaderboardManager.CurrentLimit}"))
                OpenLimitMenu();

            if (Widgets.ButtonText(refreshBtn, "Refresh"))
            {
                _selectedIndex = -1;
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
                LeaderboardManager.AskForLeaderboard(
                    LeaderboardManager.CurrentSort,
                    InformationData.LeaderboardOrder.Desc,
                    LeaderboardManager.CurrentLimit,
                    0);
            }));

            opts.Add(new FloatMenuOption(isAsc ? "✓ Low → High" : "Low → High", () =>
            {
                _selectedIndex = -1;
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

                detailText = $"{rangeText}  |  {player}  |  Tile {tile}  |  Wealth {wealth}  |  Cols {cols}  |  Days {days}  |  Playtime {play}  |  Saved {saved}";
            }

            Rect leftLabel = new Rect(rect.x + 10f, rect.y + 10f, rect.width - 420f, rect.height - 14f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Small;
            Widgets.LabelEllipses(leftLabel, detailText);
            Text.Anchor = TextAnchor.UpperLeft;

            float btnW = 120f;

            GUI.color = LeaderboardManager.CanPagePrev() ? Color.white : Color.gray;
            if (Widgets.ButtonText(new Rect(rect.xMax - (btnW * 3 + 20f), rect.y + 14f, btnW, 30f), "Prev") && LeaderboardManager.CanPagePrev())
            {
                _selectedIndex = -1;
                LeaderboardManager.PrevPage();
            }

            GUI.color = LeaderboardManager.CanPageNext() ? Color.white : Color.gray;
            if (Widgets.ButtonText(new Rect(rect.xMax - (btnW * 2 + 15f), rect.y + 14f, btnW, 30f), "Next") && LeaderboardManager.CanPageNext())
            {
                _selectedIndex = -1;
                LeaderboardManager.NextPage();
            }

            GUI.color = Color.white;
            if (Widgets.ButtonText(new Rect(rect.xMax - (btnW + 10f), rect.y + 14f, btnW, 30f), "OK"))
            {
                Close();
            }
        }

        private void DrawHeaderRow(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0f, 0f, 0f, 0.18f));

            float x = rect.x + 8f;

            float wRank = 40f;
            float wPlayer = 150f;
            float wCommunity = 170f;
            float wFaction = 170f;
            float wWealth = 120f;
            float wCols = 55f;
            float wDays = 55f;
            float wTime = 80f;

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(x, rect.y, wRank, rect.height), "#"); x += wRank;
            Widgets.Label(new Rect(x, rect.y, wPlayer, rect.height), "Player"); x += wPlayer;
            Widgets.Label(new Rect(x, rect.y, wCommunity, rect.height), "Community"); x += wCommunity;
            Widgets.Label(new Rect(x, rect.y, wFaction, rect.height), "Faction"); x += wFaction;

            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(x, rect.y, wWealth, rect.height), "Wealth"); x += wWealth;
            Widgets.Label(new Rect(x, rect.y, wCols, rect.height), "Cols"); x += wCols;
            Widgets.Label(new Rect(x, rect.y, wDays, rect.height), "Days"); x += wDays;
            Widgets.Label(new Rect(x, rect.y, wTime, rect.height), "Time");

            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.DrawLineHorizontal(rect.x, rect.yMax - 1f, rect.width);
        }

        private void DrawEntryRow(Rect rect, LeaderboardEntryFile e, int rank)
        {
            e ??= new LeaderboardEntryFile();

            string player = string.IsNullOrWhiteSpace(e.Username) ? "Unknown" : e.Username;
            string community = string.IsNullOrWhiteSpace(e.SettlementName) ? "Unknown" : e.SettlementName;
            string faction = string.IsNullOrWhiteSpace(e.FactionName) ? "Unknown" : e.FactionName;

            string wealth = FormatWealth(e);
            string cols = e.ColonistCount < 0 ? "?" : e.ColonistCount.ToString();
            string days = FormatDays(e.GameTicks);
            string time = FormatTime(e);

            float x = rect.x + 8f;

            float wRank = 40f;
            float wPlayer = 150f;
            float wCommunity = 170f;
            float wFaction = 170f;
            float wWealth = 120f;
            float wCols = 55f;
            float wDays = 55f;
            float wTime = 80f;

            Rect rRank = new Rect(x, rect.y, wRank, rect.height); x += wRank;
            Rect rPlayer = new Rect(x, rect.y, wPlayer, rect.height); x += wPlayer;
            Rect rCommunity = new Rect(x, rect.y, wCommunity, rect.height); x += wCommunity;
            Rect rFaction = new Rect(x, rect.y, wFaction, rect.height); x += wFaction;

            Rect rWealth = new Rect(x, rect.y, wWealth, rect.height); x += wWealth;
            Rect rCols = new Rect(x, rect.y, wCols, rect.height); x += wCols;
            Rect rDays = new Rect(x, rect.y, wDays, rect.height); x += wDays;
            Rect rTime = new Rect(x, rect.y, wTime, rect.height);

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rRank, rank.ToString());
            Widgets.LabelEllipses(rPlayer, player);
            Widgets.LabelEllipses(rCommunity, community);
            Widgets.LabelEllipses(rFaction, faction);

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