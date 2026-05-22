using GameClient.Files;
using GameClient.Managers;
using GameClient.PacketManagers;
using System;
using System.Collections.Generic;
using System.Linq;
using TCPNetwork.Packets;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs.Economy
{
    /// <summary>
    /// Per-player lifetime leaderboard. Sortable by donation,
    /// quests, sales, sites, worker XP, or total economy score.
    ///
    /// Discord-linked names are coloured via <see cref="LinkedAccountsCache"/>
    /// so the player can immediately see who's actually wired up to the
    /// Discord bridge.
    /// </summary>
    public class DLG_PlayerLeaderboard : DLG_Base
    {
        // Standardised to 1200x620 — same dimensions as
        // DLG_GuildLeaderboard so the two dialogs feel like one family.
        public override Vector2 InitialSize => new Vector2(1200f, 620f);

        public static List<PlayerLeaderboardEntry> CachedRows = new List<PlayerLeaderboardEntry>();

        private Vector2 _scroll = Vector2.zero;
        private SortMode _sort = SortMode.EconomyScore;
        private bool _onlyLinked;
        private bool _onlyMyGuild;
        private string _filter = "";

        // Live-refresh — re-pull every DialogLayout.AutoRefreshSeconds
        // so stats visibly update as players post quests, build sites, donate,
        // etc. The server's snapshot computation reads UserManagerH directly
        // so numbers are current the instant a stat is recorded.
        private float _refreshTimer = DialogLayout.AutoRefreshSeconds;
        private DateTime _lastRefreshUtc = DateTime.UtcNow;

        // Cache the filtered+sorted view so we don't run LINQ
        // through Where().OrderByDescending().ToList() on every redraw
        // (60fps × N players = a lot of unnecessary allocation). Cache is
        // invalidated by:
        //   * Fresh server snapshot (`CachedRows` reference changes)
        //   * Filter string change
        //   * Sort mode change
        //   * "Only linked" / "Only my guild" toggle change
        // and rebuilt lazily next draw.
        private List<PlayerLeaderboardEntry> _visibleCache;
        private List<PlayerLeaderboardEntry> _visibleCacheSource;
        private string _visibleCacheFilter;
        private SortMode _visibleCacheSort;
        private bool _visibleCacheOnlyLinked;
        private bool _visibleCacheOnlyMyGuild;

        private enum SortMode
        {
            EconomyScore,
            SilverDonated,
            SalesEarned,
            QuestsCompleted,
            SitesBuilt,
            WorkerXp,
            Tenure
        }

        public DLG_PlayerLeaderboard()
        {
            Title = "Player Leaderboard";
            closeOnCancel = true;
            absorbInputAroundWindow = true;

            PM_PlayerStats.RequestLeaderboard();
            _lastRefreshUtc = DateTime.UtcNow;

            // React the instant a server-pushed snapshot lands —
            // invalidate the filter/sort cache and flip the live indicator
            // so the user sees fresh data without waiting for the next poll.
            PM_PlayerStats.OnLeaderboardSnapshotUpdated += OnServerPushedSnapshot;
        }

        public override void PostClose()
        {
            base.PostClose();
            PM_PlayerStats.OnLeaderboardSnapshotUpdated -= OnServerPushedSnapshot;
        }

        private void OnServerPushedSnapshot()
        {
            // Drop the filter/sort cache so the next draw rebuilds from the
            // fresh CachedRows; also reset the "● live" badge to "just now".
            _visibleCache = null;
            _lastRefreshUtc = DateTime.UtcNow;
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            // Tick down a per-frame timer so the dialog re-fetches on a
            // wall-clock interval rather than per-redraw count.
            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = DialogLayout.AutoRefreshSeconds;
                PM_PlayerStats.RequestLeaderboard();
                _lastRefreshUtc = DateTime.UtcNow;
            }
        }

        public override void DoWindowContents(Rect rect)
        {
            // Standard title + live-badge + section divider
            // via DialogLayout. Same look as every other KMH dialog.
            float y = DialogLayout.DrawTitle(rect, "Player Leaderboard");
            int secsSince = Math.Max(0, (int)(DateTime.UtcNow - _lastRefreshUtc).TotalSeconds);
            DialogLayout.DrawLiveBadge(rect, secsSince);
            DialogLayout.DrawSectionDivider(rect, ref y);

            // Compact toolbar matching DLG_GuildLeaderboard.
            //   [filter ........] [Sort: X] [☐ Linked only] [☐ My guild only]   [Refresh]
            // Tight-checkbox helper puts ☐ right beside the label instead
            // of stretching across 130px of dead space.
            Rect filterRect = new Rect(0f, y, 220f, 28f);
            _filter = Widgets.TextField(filterRect, _filter ?? "");
            if (string.IsNullOrWhiteSpace(_filter))
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.3f);
                Widgets.Label(filterRect.ContractedBy(6f, 4f), "Filter by player or guild…");
                GUI.color = old;
            }

            // Friendly enum labels.
            if (Widgets.ButtonText(new Rect(228f, y, 200f, 28f), $"Sort: {DialogLayout.FriendlyEnumName(_sort)}"))
            {
                List<FloatMenuOption> opts = new List<FloatMenuOption>();
                foreach (SortMode m in Enum.GetValues(typeof(SortMode)))
                {
                    SortMode cap = m;
                    opts.Add(new FloatMenuOption(DialogLayout.FriendlyEnumName(cap), () => _sort = cap));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }

            float cbx = 436f;
            cbx = DialogLayout.DrawTightCheckbox(cbx, y + 4f, "Linked only", ref _onlyLinked);
            cbx = DialogLayout.DrawTightCheckbox(cbx, y + 4f, "My guild only", ref _onlyMyGuild);

            if (Widgets.ButtonText(new Rect(rect.width - 110f, y, 100f, 28f), "Refresh"))
                PM_PlayerStats.RequestLeaderboard();
            y += 34f;

            // Header
            DrawHeader(new Rect(0f, y, rect.width, 22f));
            y += 24f;

            Rect listBox = new Rect(0f, y, rect.width, rect.height - y - 44f);
            Widgets.DrawMenuSection(listBox);
            DrawRows(listBox);

            if (DialogLayout.DrawCloseButton(rect)) Close();
        }

        private void DrawHeader(Rect r)
        {
            // Player + Guild columns stay left-aligned (text).
            // All numeric columns center-align both header and cells.
            float[] cols = ColumnXs(r.width);
            Widgets.Label(new Rect(cols[0], r.y, cols[1] - cols[0], r.height), "<b>Player</b>");
            Widgets.Label(new Rect(cols[1], r.y, cols[2] - cols[1], r.height), "<b>Guild</b>");
            DialogLayout.DrawCenteredLabel(new Rect(cols[2], r.y, cols[3] - cols[2], r.height), "<b>Score</b>");
            DialogLayout.DrawCenteredLabel(new Rect(cols[3], r.y, cols[4] - cols[3], r.height), "<b>Donated</b>");
            DialogLayout.DrawCenteredLabel(new Rect(cols[4], r.y, cols[5] - cols[4], r.height), "<b>Sales</b>");
            DialogLayout.DrawCenteredLabel(new Rect(cols[5], r.y, cols[6] - cols[5], r.height), "<b>Quests</b>");
            DialogLayout.DrawCenteredLabel(new Rect(cols[6], r.y, cols[7] - cols[6], r.height), "<b>Sites</b>");
            DialogLayout.DrawCenteredLabel(new Rect(cols[7], r.y, cols[8] - cols[7], r.height), "<b>XP</b>");
            DialogLayout.DrawCenteredLabel(new Rect(cols[8], r.y, r.width - cols[8], r.height), "<b>Tenure</b>");
        }

        private void DrawRows(Rect box)
        {
            Rect inner = box.ContractedBy(4f);
            const float rowH = 24f;

            string mine = PersistentSettings.Load().UserSettings.Username ?? string.Empty;
            string myGuild = string.Empty;
            // Best-effort: read from cached treasury if owned by guild.
            if (TreasuryClientCache.HasSnapshot && TreasuryClientCache.IsGuildOwned)
                myGuild = TreasuryClientCache.OwnerKey ?? string.Empty;

            string filterLower = (_filter ?? "").Trim().ToLower();

            // Only rebuild the filtered/sorted list when one of the
            // inputs actually changes — otherwise reuse the cached list. This
            // avoids a Where()/OrderBy()/ToList() per frame at 60fps.
            bool inputsChanged =
                _visibleCache == null
                || !ReferenceEquals(_visibleCacheSource, CachedRows)
                || _visibleCacheFilter != filterLower
                || _visibleCacheSort != _sort
                || _visibleCacheOnlyLinked != _onlyLinked
                || _visibleCacheOnlyMyGuild != _onlyMyGuild;

            if (inputsChanged)
            {
                IEnumerable<PlayerLeaderboardEntry> visible = CachedRows.Where(r =>
                    (!_onlyLinked || r.IsLinkedToDiscord) &&
                    (!_onlyMyGuild || (!string.IsNullOrEmpty(myGuild) && string.Equals(r.GuildName, myGuild, StringComparison.OrdinalIgnoreCase))) &&
                    (string.IsNullOrEmpty(filterLower)
                        || (r.Username ?? "").ToLower().Contains(filterLower)
                        || (r.GuildName ?? "").ToLower().Contains(filterLower)));

                switch (_sort)
                {
                    case SortMode.EconomyScore: visible = visible.OrderByDescending(x => x.EconomyScore); break;
                    case SortMode.SilverDonated: visible = visible.OrderByDescending(x => x.SilverDonated); break;
                    case SortMode.SalesEarned: visible = visible.OrderByDescending(x => x.SalesEarned); break;
                    case SortMode.QuestsCompleted: visible = visible.OrderByDescending(x => x.QuestsCompleted); break;
                    case SortMode.SitesBuilt: visible = visible.OrderByDescending(x => x.SitesBuilt); break;
                    case SortMode.WorkerXp: visible = visible.OrderByDescending(x => x.WorkerXp); break;
                    case SortMode.Tenure: visible = visible.OrderBy(x => x.FirstSeenUtcTicks > 0 ? x.FirstSeenUtcTicks : long.MaxValue); break;
                }

                _visibleCache = visible.ToList();
                _visibleCacheSource = CachedRows;
                _visibleCacheFilter = filterLower;
                _visibleCacheSort = _sort;
                _visibleCacheOnlyLinked = _onlyLinked;
                _visibleCacheOnlyMyGuild = _onlyMyGuild;
            }

            List<PlayerLeaderboardEntry> rows = _visibleCache;
            float viewH = Mathf.Max(inner.height, rows.Count * rowH + 8f);
            Rect viewRect = new Rect(0f, 0f, inner.width - 16f, viewH);

            Widgets.BeginScrollView(inner, ref _scroll, viewRect);
            float ly = 0f;
            float[] cols = ColumnXs(viewRect.width);

            for (int i = 0; i < rows.Count; i++)
            {
                PlayerLeaderboardEntry r = rows[i];
                Rect row = new Rect(0f, ly, viewRect.width, rowH);
                if (i % 2 == 0) Widgets.DrawAltRect(row);
                Widgets.DrawHighlightIfMouseover(row);

                bool isMe = string.Equals(r.Username, mine, StringComparison.OrdinalIgnoreCase);
                string rank = i < 3
                    ? $"<color=yellow>#{i + 1}</color>"
                    : $"<color=grey>#{i + 1}</color>";
                string nameRender = LinkedAccountsCache.Format(r.Username);
                if (isMe) nameRender = $"<color=#80ff80>★</color> {nameRender}";

                Widgets.Label(new Rect(cols[0], ly + 2f, cols[1] - cols[0], rowH - 4f), $"{rank}  <b>{nameRender}</b>");
                Widgets.Label(new Rect(cols[1], ly + 2f, cols[2] - cols[1], rowH - 4f),
                    string.IsNullOrEmpty(r.GuildName) ? "<color=grey>—</color>" : r.GuildName);
                // Numeric cells center-aligned to match headers.
                DialogLayout.DrawCenteredLabel(new Rect(cols[2], ly + 2f, cols[3] - cols[2], rowH - 4f), r.EconomyScore.ToString());
                DialogLayout.DrawCenteredLabel(new Rect(cols[3], ly + 2f, cols[4] - cols[3], rowH - 4f), $"{r.SilverDonated}s");
                DialogLayout.DrawCenteredLabel(new Rect(cols[4], ly + 2f, cols[5] - cols[4], rowH - 4f), $"{r.SalesEarned}s");
                DialogLayout.DrawCenteredLabel(new Rect(cols[5], ly + 2f, cols[6] - cols[5], rowH - 4f), $"{r.QuestsCompleted}/{r.QuestsPosted}");
                DialogLayout.DrawCenteredLabel(new Rect(cols[6], ly + 2f, cols[7] - cols[6], rowH - 4f), r.SitesBuilt.ToString());
                DialogLayout.DrawCenteredLabel(new Rect(cols[7], ly + 2f, cols[8] - cols[7], rowH - 4f), r.WorkerXp.ToString());
                DialogLayout.DrawCenteredLabel(new Rect(cols[8], ly + 2f, viewRect.width - cols[8], rowH - 4f), FormatTenure(r.FirstSeenUtcTicks));

                ly += rowH;
            }
            if (rows.Count == 0)
                Widgets.Label(new Rect(6f, 6f, viewRect.width, 20f), "<color=grey>No matching players.</color>");

            Widgets.EndScrollView();
        }

        private static float[] ColumnXs(float w)
        {
            // 9 cols: Player | Guild | Score | Donated | Sales | Quests | Sites | XP | Tenure
            return new float[]
            {
                10f,           // 0 Player
                w * 0.28f,     // 1 Guild
                w * 0.42f,     // 2 Score
                w * 0.52f,     // 3 Donated
                w * 0.62f,     // 4 Sales
                w * 0.72f,     // 5 Quests
                w * 0.80f,     // 6 Sites
                w * 0.86f,     // 7 XP
                w * 0.92f      // 8 Tenure
            };
        }

        private static string FormatTenure(long firstSeenUtcTicks)
        {
            if (firstSeenUtcTicks <= 0) return "<color=grey>—</color>";
            try
            {
                TimeSpan span = DateTime.UtcNow - new DateTime(firstSeenUtcTicks, DateTimeKind.Utc);
                if (span.TotalDays >= 1) return $"{(int)span.TotalDays}d";
                if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h";
                return $"{Math.Max(1, (int)span.TotalMinutes)}m";
            }
            catch { return "—"; }
        }
    }
}
