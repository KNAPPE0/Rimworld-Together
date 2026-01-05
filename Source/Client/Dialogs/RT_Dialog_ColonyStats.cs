using Shared.Files;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs
{
    public class RT_Dialog_ColonyStats : RT_Dialog_Base
    {
        public override Vector2 InitialSize => new Vector2(560f, 460f);

        private readonly MapStatsFile _stats;
        private readonly bool? _isOnline;
        private readonly string _localSettlementLabel;

        private readonly List<StatsSection> _sections = new List<StatsSection>();

        private float _viewHeight;
        private Vector2 _localScroll = Vector2.zero;

        private const float HeaderHeight = 34f;
        private const float FooterHeight = 48f;

        private const float SectionHeaderHeight = 26f;
        private const float RowHeight = 26f;
        private const float RowPaddingX = 10f;

        public RT_Dialog_ColonyStats(MapStatsFile stats, bool? isOnline, string settlementLabel = null)
        {
            Title = "Colony Stats";
            _stats = stats;
            _isOnline = isOnline;
            _localSettlementLabel = settlementLabel;

            closeOnAccept = false;
            closeOnCancel = false;

            BuildSections();
            _viewHeight = CalculateViewHeight();
        }

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width, HeaderHeight), Title);

            Text.Font = GameFont.Small;
            Widgets.DrawLineHorizontal(0f, HeaderHeight - 6f, rect.width);

            Rect outer = new Rect(0f, HeaderHeight, rect.width, rect.height - HeaderHeight - FooterHeight);
            Widgets.DrawMenuSection(outer);

            Rect inner = outer.ContractedBy(10f);

            Rect scrollRect = new Rect(inner.x, inner.y, inner.width, inner.height);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, _viewHeight);

            Widgets.BeginScrollView(scrollRect, ref _localScroll, viewRect);
            try
            {
                float y = 0f;
                float split = Mathf.Clamp(viewRect.width * 0.58f, 240f, viewRect.width - 160f);

                int globalRowIndex = 0;

                foreach (StatsSection section in _sections)
                {
                    DrawSectionHeader(viewRect, ref y, section.Title);

                    for (int i = 0; i < section.Rows.Count; i++)
                    {
                        StatsRow row = section.Rows[i];
                        Rect rowRect = new Rect(0f, y, viewRect.width, RowHeight);

                        if (globalRowIndex % 2 == 0)
                            Widgets.DrawAltRect(rowRect);

                        Widgets.DrawHighlightIfMouseover(rowRect);

                        DrawRow(rowRect, row.Key, row.Value, split);

                        y += RowHeight;
                        globalRowIndex++;
                    }

                    y += 8f;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }

            Rect footer = new Rect(0f, rect.height - FooterHeight, rect.width, FooterHeight);

            if (Widgets.ButtonText(new Rect(footer.x, footer.y + 8f, SmallButtonSize.x, SmallButtonSize.y), "Refresh"))
            {
                Close();
                GameClient.Managers.StatisticalManager.AskForStats();
            }

            if (Widgets.ButtonText(new Rect(footer.xMax - SmallButtonSize.x, footer.y + 8f, SmallButtonSize.x, SmallButtonSize.y), "OK"))
                Close();
        }

        private void DrawSectionHeader(Rect viewRect, ref float y, string title)
        {
            Rect headerRect = new Rect(0f, y, viewRect.width, SectionHeaderHeight);

            Widgets.DrawBoxSolid(headerRect, new Color(0f, 0f, 0f, 0.18f));

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;

            Rect textRect = headerRect.ContractedBy(RowPaddingX, 0f);
            Widgets.Label(textRect, title);

            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            y += SectionHeaderHeight;
            Widgets.DrawLineHorizontal(0f, y, viewRect.width);
            y += 2f;
        }

        private void DrawRow(Rect rowRect, string key, string value, float split)
        {
            Rect left = new Rect(rowRect.x + RowPaddingX, rowRect.y + 3f, split - RowPaddingX * 2f, rowRect.height - 6f);
            Rect right = new Rect(rowRect.x + split, rowRect.y + 3f, rowRect.width - split - RowPaddingX, rowRect.height - 6f);

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(left, key);

            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(right, value);

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private float CalculateViewHeight()
        {
            float total = 0f;

            foreach (StatsSection section in _sections)
            {
                total += SectionHeaderHeight + 2f;
                total += section.Rows.Count * RowHeight;
                total += 8f;
            }

            return Mathf.Max(total, 200f);
        }

        private void BuildSections()
        {
            _sections.Clear();

            if (_stats == null)
            {
                StatsSection missing = new StatsSection("Overview");
                missing.Rows.Add(new StatsRow("Stats", "No stats found (settlement may not be saved yet)."));
                _sections.Add(missing);
                return;
            }

            string username = string.IsNullOrWhiteSpace(_stats.Username) ? "Unknown" : _stats.Username;
            string settlementLabel = string.IsNullOrWhiteSpace(_localSettlementLabel) ? "Unknown" : _localSettlementLabel;
            string communityName = string.IsNullOrWhiteSpace(_stats.SettlementName) ? "Unknown" : _stats.SettlementName;
            string factionName = string.IsNullOrWhiteSpace(_stats.FactionName) ? "Unknown" : _stats.FactionName;

            string status = "Unknown";
            if (_isOnline.HasValue)
                status = _isOnline.Value ? "Online" : "Offline";

            string wealth = FormatWealth(_stats);

            string playtimeStr = FormatPlaytime(_stats.RealPlayTimeSeconds, _stats.RealPlayTimeInteractingSeconds);

            string daysStr = "Unknown";
            if (_stats.GameTicks >= 0)
            {
                int days = _stats.GameTicks / 60000;
                daysStr = days.ToString("N0");
            }

            string lastSaved = FormatUtcTicks(_stats.LastSavedUtcTicks);

            StatsSection overview = new StatsSection("Overview");
            overview.Rows.Add(new StatsRow("Settlement", settlementLabel));
            overview.Rows.Add(new StatsRow("Community", communityName));
            overview.Rows.Add(new StatsRow("Faction", factionName));
            overview.Rows.Add(new StatsRow("Player", username));
            overview.Rows.Add(new StatsRow("Status", status));
            overview.Rows.Add(new StatsRow("Wealth", wealth));
            overview.Rows.Add(new StatsRow("Colonists", FormatInt(_stats.ColonistCount)));
            overview.Rows.Add(new StatsRow("Playtime", playtimeStr));
            overview.Rows.Add(new StatsRow("Days", daysStr));
            overview.Rows.Add(new StatsRow("Last Saved", lastSaved));
            _sections.Add(overview);

            StatsSection population = new StatsSection("Population");
            population.Rows.Add(new StatsRow("Faction Humans", FormatInt(_stats.FactionHumanCount)));
            population.Rows.Add(new StatsRow("Non-Faction Humans", FormatInt(_stats.NonFactionHumanCount)));
            population.Rows.Add(new StatsRow("Faction Animals", FormatInt(_stats.FactionAnimalCount)));
            population.Rows.Add(new StatsRow("Non-Faction Animals", FormatInt(_stats.NonFactionAnimalCount)));
            _sections.Add(population);

            StatsSection things = new StatsSection("Things");
            things.Rows.Add(new StatsRow("Faction Things", FormatInt(_stats.FactionThingCount)));
            things.Rows.Add(new StatsRow("Non-Faction Things", FormatInt(_stats.NonFactionThingCount)));
            _sections.Add(things);
        }

        private static string FormatWealth(MapStatsFile stats)
        {
            if (stats.WealthExact >= 0)
                return "$" + stats.WealthExact.ToString("N2");

            if (stats.Wealth < 0) return "Unknown";

            return "$" + stats.Wealth.ToString("N0");
        }

        private static string FormatPlaytime(double totalSeconds, double interactingSeconds)
        {
            double seconds = totalSeconds >= 0 ? totalSeconds : interactingSeconds;
            if (seconds < 0) return "Unknown";

            try
            {
                TimeSpan ts = TimeSpan.FromSeconds(seconds);
                int hours = (int)Math.Floor(ts.TotalHours);
                int minutes = ts.Minutes;
                return $"{hours}h {minutes}m";
            }
            catch
            {
                return "Unknown";
            }
        }

        private static string FormatInt(int value)
        {
            if (value < 0) return "Unknown";
            return value.ToString("N0");
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

        private class StatsSection
        {
            public string Title;
            public List<StatsRow> Rows = new List<StatsRow>();

            public StatsSection(string title) { Title = title; }
        }

        private class StatsRow
        {
            public string Key;
            public string Value;

            public StatsRow(string key, string value) { Key = key; Value = value; }
        }
    }
}