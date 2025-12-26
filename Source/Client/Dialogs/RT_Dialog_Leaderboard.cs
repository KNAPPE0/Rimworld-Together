using Shared.Files;
using System;
using UnityEngine;
using Verse;
using TCPNetwork.Packets;

namespace GameClient.Dialogs
{
    public class RT_Dialog_Leaderboard : RT_Dialog_Base
    {
        public override Vector2 InitialSize => new Vector2(860f, 560f);

        private readonly LeaderboardEntryFile[] _entries;
        private readonly int _total;

        private InformationData.LeaderboardSortMode _sort;
        private InformationData.LeaderboardOrder _order;

        private int _limit;
        private int _offset;

        private Vector2 _scroll = Vector2.zero;

        private const float HeaderHeight = 38f;
        private const float FooterHeight = 54f;

        public RT_Dialog_Leaderboard(
            LeaderboardEntryFile[] entries,
            int total,
            InformationData.LeaderboardSortMode sort,
            InformationData.LeaderboardOrder order,
            int limit,
            int offset)
        {
            Title = "Leaderboard";
            _entries = entries ?? new LeaderboardEntryFile[0];
            _total = total;

            _sort = sort;
            _order = order;
            _limit = limit;
            _offset = offset;

            closeOnAccept = false;
            closeOnCancel = false;
        }

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width, HeaderHeight), Title);

            Text.Font = GameFont.Small;
            Widgets.DrawLineHorizontal(0f, HeaderHeight - 6f, rect.width);

            Rect topControls = new Rect(0f, HeaderHeight, rect.width, 34f);
            DrawControls(topControls);

            Rect outer = new Rect(0f, HeaderHeight + 34f, rect.width, rect.height - HeaderHeight - 34f - FooterHeight);
            Widgets.DrawMenuSection(outer);

            Rect inner = outer.ContractedBy(10f);

            Rect viewRect = new Rect(0f, 0f, inner.width - 16f, Math.Max(240f, _entries.Length * 28f + 40f));
            Widgets.BeginScrollView(inner, ref _scroll, viewRect);

            float y = 0f;

            DrawHeaderRow(new Rect(0f, y, viewRect.width, 28f));
            y += 30f;

            for (int i = 0; i < _entries.Length; i++)
            {
                Rect row = new Rect(0f, y, viewRect.width, 26f);
                if (i % 2 == 0) Widgets.DrawAltRect(row);
                Widgets.DrawHighlightIfMouseover(row);

                DrawEntryRow(row, _entries[i]);
                y += 26f;
            }

            Widgets.EndScrollView();

            Rect footer = new Rect(0f, rect.height - FooterHeight, rect.width, FooterHeight);
            DrawFooter(footer);
        }

        private void DrawControls(Rect rect)
        {
            float x = rect.x;
            float y = rect.y + 4f;

            if (Widgets.ButtonText(new Rect(x, y, 160f, 26f), $"Sort: {_sort}"))
            {
                _sort = NextSort(_sort);
                GameClient.Managers.LeaderboardManager.AskForLeaderboard(_sort, _limit, _offset, _order);
            }
            x += 170f;

            if (Widgets.ButtonText(new Rect(x, y, 120f, 26f), $"Order: {_order}"))
            {
                _order = _order == InformationData.LeaderboardOrder.Desc
                    ? InformationData.LeaderboardOrder.Asc
                    : InformationData.LeaderboardOrder.Desc;

                GameClient.Managers.LeaderboardManager.AskForLeaderboard(_sort, _limit, _offset, _order);
            }
            x += 130f;

            if (Widgets.ButtonText(new Rect(x, y, 120f, 26f), $"Top: {_limit}"))
            {
                _limit = NextLimit(_limit);
                _offset = 0;
                GameClient.Managers.LeaderboardManager.AskForLeaderboard(_sort, _limit, _offset, _order);
            }
        }

        private void DrawFooter(Rect rect)
        {
            int start = _total <= 0 ? 0 : Math.Min(_total, _offset + 1);
            int end = _total <= 0 ? 0 : Math.Min(_total, _offset + _entries.Length);

            string rangeText = _total < 0
                ? "Unknown total"
                : $"Showing {start}-{end} of {_total}";

            Widgets.Label(new Rect(rect.x + 10f, rect.y + 16f, 260f, 26f), rangeText);

            float btnW = 120f;

            if (Widgets.ButtonText(new Rect(rect.xMax - (btnW * 3 + 20f), rect.y + 12f, btnW, 30f), "Prev"))
            {
                _offset = Math.Max(0, _offset - _limit);
                GameClient.Managers.LeaderboardManager.AskForLeaderboard(_sort, _limit, _offset, _order);
            }

            if (Widgets.ButtonText(new Rect(rect.xMax - (btnW * 2 + 15f), rect.y + 12f, btnW, 30f), "Next"))
            {
                _offset = Math.Max(0, _offset + _limit);
                GameClient.Managers.LeaderboardManager.AskForLeaderboard(_sort, _limit, _offset, _order);
            }

            if (Widgets.ButtonText(new Rect(rect.xMax - (btnW + 10f), rect.y + 12f, btnW, 30f), "OK"))
            {
                Close();
            }
        }

        private void DrawHeaderRow(Rect rect)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x + 8f, rect.y, 40f, rect.height), "#");
            Widgets.Label(new Rect(rect.x + 52f, rect.y, 140f, rect.height), "Player");
            Widgets.Label(new Rect(rect.x + 200f, rect.y, 150f, rect.height), "Faction");
            Widgets.Label(new Rect(rect.x + 360f, rect.y, 170f, rect.height), "Colony");

            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(rect.xMax - 260f, rect.y, 80f, rect.height), "Wealth");
            Widgets.Label(new Rect(rect.xMax - 170f, rect.y, 70f, rect.height), "Cols");
            Widgets.Label(new Rect(rect.xMax - 95f, rect.y, 90f, rect.height), "Time");

            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.DrawLineHorizontal(rect.x, rect.yMax - 1f, rect.width);
        }

        private void DrawEntryRow(Rect rect, LeaderboardEntryFile e)
        {
            string player = e != null ? (e.Username ?? "") : "";
            string faction = e != null ? (e.FactionName ?? "") : "";
            string colony = e != null ? (e.SettlementName ?? "") : "";

            string wealth = FormatWealth(e);
            string cols = e == null || e.ColonistCount < 0 ? "?" : e.ColonistCount.ToString();
            string time = FormatTime(e);

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x + 8f, rect.y, 40f, rect.height), e != null ? e.Rank.ToString() : "?");
            Widgets.Label(new Rect(rect.x + 52f, rect.y, 140f, rect.height), player);
            Widgets.Label(new Rect(rect.x + 200f, rect.y, 150f, rect.height), faction);
            Widgets.Label(new Rect(rect.x + 360f, rect.y, 170f, rect.height), colony);

            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(rect.xMax - 260f, rect.y, 80f, rect.height), wealth);
            Widgets.Label(new Rect(rect.xMax - 170f, rect.y, 70f, rect.height), cols);
            Widgets.Label(new Rect(rect.xMax - 95f, rect.y, 90f, rect.height), time);

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static string FormatWealth(LeaderboardEntryFile e)
        {
            if (e == null) return "?";
            if (e.WealthExact >= 0) return "$" + e.WealthExact.ToString("N2");
            if (e.Wealth >= 0) return "$" + e.Wealth.ToString("N0");
            return "?";
        }

        private static string FormatTime(LeaderboardEntryFile e)
        {
            if (e == null || e.GameTicks < 0) return "?";

            int days = e.GameTicks / 60000;
            int remainder = e.GameTicks - (days * 60000);

            int hours = remainder / 2500;
            remainder -= hours * 2500;

            double mins = remainder / (2500d / 60d);
            int minutes = (int)Math.Round(mins);

            if (minutes >= 60) { minutes = 0; hours++; }
            if (hours >= 24) { hours = 0; days++; }

            return $"{hours}h {minutes}m";
        }

        private static InformationData.LeaderboardSortMode NextSort(InformationData.LeaderboardSortMode current)
        {
            switch (current)
            {
                case InformationData.LeaderboardSortMode.WealthExact: return InformationData.LeaderboardSortMode.Wealth;
                case InformationData.LeaderboardSortMode.Wealth: return InformationData.LeaderboardSortMode.Colonists;
                case InformationData.LeaderboardSortMode.Colonists: return InformationData.LeaderboardSortMode.PlaytimeTicks;
                case InformationData.LeaderboardSortMode.PlaytimeTicks: return InformationData.LeaderboardSortMode.SettlementName;
                case InformationData.LeaderboardSortMode.SettlementName: return InformationData.LeaderboardSortMode.FactionName;
                case InformationData.LeaderboardSortMode.FactionName: return InformationData.LeaderboardSortMode.LastSavedUtcTicks;
                default: return InformationData.LeaderboardSortMode.WealthExact;
            }
        }

        private static int NextLimit(int current)
        {
            if (current <= 10) return 25;
            if (current <= 25) return 50;
            if (current <= 50) return 100;
            return 10;
        }
    }
}