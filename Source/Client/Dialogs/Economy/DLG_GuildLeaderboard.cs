using GameClient.PacketManagers;
using System.Collections.Generic;
using System.Linq;
using TCPNetwork.Packets;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs.Economy
{
    /// <summary>
    /// Cross-guild leaderboard view. Sortable by silver, members, contribution, etc.
    /// KMH 2.7: Expanded with sites, alliances, tenure, total worker XP.
    /// </summary>
    public class DLG_GuildLeaderboard : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(1200f, 620f);

        public static List<GuildLeaderboardEntry> CachedRows = new List<GuildLeaderboardEntry>();

        private Vector2 _scroll = Vector2.zero;
        private SortMode _sort = SortMode.LifetimeSilverIn;
        private View _view = View.Standard;

        // KMH 2.7: Live re-fetch every AutoRefreshSeconds so guild rankings
        // update in near-real-time as members donate / quest / build sites.
        private float _refreshTimer = DialogLayout.AutoRefreshSeconds;
        private System.DateTime _lastRefreshUtc = System.DateTime.UtcNow;

        private enum SortMode
        {
            Members, TreasurySilver, LifetimeSilverIn, Perks,
            Quests, Contributions, Sites, Allies, Tenure, WorkerXp
        }

        private enum View { Standard, Diplomacy, Activity }

        public DLG_GuildLeaderboard()
        {
            Title = "Guild Leaderboard";
            closeOnCancel = true;
            absorbInputAroundWindow = true;

            PM_GuildHall.RequestLeaderboard();
            _lastRefreshUtc = System.DateTime.UtcNow;

            // KMH 2.7: React the instant a server-pushed snapshot lands so the
            // "● live · just now" badge updates without waiting for the next
            // poll, and so we never linger on stale data after a save.
            PM_GuildHall.OnLeaderboardSnapshotUpdated += OnServerPushedSnapshot;
        }

        public override void PostClose()
        {
            base.PostClose();
            PM_GuildHall.OnLeaderboardSnapshotUpdated -= OnServerPushedSnapshot;
        }

        private void OnServerPushedSnapshot()
        {
            _lastRefreshUtc = System.DateTime.UtcNow;
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            _refreshTimer -= UnityEngine.Time.unscaledDeltaTime;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = DialogLayout.AutoRefreshSeconds;
                PM_GuildHall.RequestLeaderboard();
                _lastRefreshUtc = System.DateTime.UtcNow;
            }
        }

        public override void DoWindowContents(Rect rect)
        {
            // KMH 26.5.20.1: Same title + live-badge + divider pattern as
            // every other KMH dialog via DialogLayout.
            float y = DialogLayout.DrawTitle(rect, "Guild Leaderboard");
            int secsSince = System.Math.Max(0, (int)(System.DateTime.UtcNow - _lastRefreshUtc).TotalSeconds);
            DialogLayout.DrawLiveBadge(rect, secsSince);
            DialogLayout.DrawSectionDivider(rect, ref y);

            // KMH 26.5.20.1: Friendly enum display names — was showing raw
            // CamelCase identifiers like "LifetimeSilverIn" / "WorkerXp".
            if (Widgets.ButtonText(new Rect(0f, y, 220f, 28f), $"Sort: {DialogLayout.FriendlyEnumName(_sort)}"))
            {
                List<FloatMenuOption> opts = new List<FloatMenuOption>();
                foreach (SortMode m in System.Enum.GetValues(typeof(SortMode)))
                {
                    SortMode cap = m;
                    opts.Add(new FloatMenuOption(DialogLayout.FriendlyEnumName(cap), () => _sort = cap));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            if (Widgets.ButtonText(new Rect(228f, y, 200f, 28f), $"View: {DialogLayout.FriendlyEnumName(_view)}"))
            {
                List<FloatMenuOption> opts = new List<FloatMenuOption>();
                foreach (View v in System.Enum.GetValues(typeof(View)))
                {
                    View cap = v;
                    opts.Add(new FloatMenuOption(DialogLayout.FriendlyEnumName(cap), () => _view = cap));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            if (Widgets.ButtonText(new Rect(rect.width - 110f, y, 100f, 28f), "Refresh"))
                PM_GuildHall.RequestLeaderboard();
            y += 34f;

            DrawHeader(new Rect(0f, y, rect.width, 22f));
            y += 24f;

            Rect listBox = new Rect(0f, y, rect.width, rect.height - y - 44f);
            Widgets.DrawMenuSection(listBox);
            DrawRows(listBox);

            if (DialogLayout.DrawCloseButton(rect)) Close();
        }

        private void DrawHeader(Rect r)
        {
            // KMH 26.5.20.1: Guild column stays left-aligned, all numeric
            // columns center-aligned for visual consistency with the
            // player leaderboard.
            float[] cols = ColumnXs(r.width);
            Widgets.Label(new Rect(cols[0], r.y, cols[1] - cols[0], r.height), "<b>Guild</b>");
            switch (_view)
            {
                case View.Standard:
                    DialogLayout.DrawCenteredLabel(new Rect(cols[1], r.y, cols[2] - cols[1], r.height), "<b>Members</b>");
                    DialogLayout.DrawCenteredLabel(new Rect(cols[2], r.y, cols[3] - cols[2], r.height), "<b>Treasury</b>");
                    DialogLayout.DrawCenteredLabel(new Rect(cols[3], r.y, cols[4] - cols[3], r.height), "<b>Lifetime In</b>");
                    DialogLayout.DrawCenteredLabel(new Rect(cols[4], r.y, cols[5] - cols[4], r.height), "<b>Perks</b>");
                    DialogLayout.DrawCenteredLabel(new Rect(cols[5], r.y, cols[6] - cols[5], r.height), "<b>Quests</b>");
                    DialogLayout.DrawCenteredLabel(new Rect(cols[6], r.y, cols[7] - cols[6], r.height), "<b>Sites</b>");
                    DialogLayout.DrawCenteredLabel(new Rect(cols[7], r.y, r.width - cols[7], r.height), "<b>Tenure</b>");
                    break;
                case View.Diplomacy:
                    DialogLayout.DrawCenteredLabel(new Rect(cols[1], r.y, cols[2] - cols[1], r.height), "<b>Members</b>");
                    DialogLayout.DrawCenteredLabel(new Rect(cols[2], r.y, cols[3] - cols[2], r.height), "<b>Allies</b>");
                    DialogLayout.DrawCenteredLabel(new Rect(cols[3], r.y, cols[4] - cols[3], r.height), "<b>Hostiles</b>");
                    DialogLayout.DrawCenteredLabel(new Rect(cols[4], r.y, cols[5] - cols[4], r.height), "<b>Sites</b>");
                    DialogLayout.DrawCenteredLabel(new Rect(cols[5], r.y, cols[6] - cols[5], r.height), "<b>Treasury</b>");
                    DialogLayout.DrawCenteredLabel(new Rect(cols[6], r.y, cols[7] - cols[6], r.height), "<b>Contribs</b>");
                    DialogLayout.DrawCenteredLabel(new Rect(cols[7], r.y, r.width - cols[7], r.height), "<b>Tenure</b>");
                    break;
                case View.Activity:
                    DialogLayout.DrawCenteredLabel(new Rect(cols[1], r.y, cols[2] - cols[1], r.height), "<b>Members</b>");
                    DialogLayout.DrawCenteredLabel(new Rect(cols[2], r.y, cols[3] - cols[2], r.height), "<b>Member XP</b>");
                    DialogLayout.DrawCenteredLabel(new Rect(cols[3], r.y, cols[4] - cols[3], r.height), "<b>Member Earned</b>");
                    DialogLayout.DrawCenteredLabel(new Rect(cols[4], r.y, cols[5] - cols[4], r.height), "<b>Sites Built</b>");
                    DialogLayout.DrawCenteredLabel(new Rect(cols[5], r.y, cols[6] - cols[5], r.height), "<b>Quests</b>");
                    DialogLayout.DrawCenteredLabel(new Rect(cols[6], r.y, cols[7] - cols[6], r.height), "<b>Lifetime In</b>");
                    DialogLayout.DrawCenteredLabel(new Rect(cols[7], r.y, r.width - cols[7], r.height), "<b>Tenure</b>");
                    break;
            }
        }

        private void DrawRows(Rect box)
        {
            Rect inner = box.ContractedBy(4f);
            const float rowH = 24f;

            IEnumerable<GuildLeaderboardEntry> sorted = CachedRows;
            switch (_sort)
            {
                case SortMode.Members: sorted = sorted.OrderByDescending(r => r.MemberCount); break;
                case SortMode.TreasurySilver: sorted = sorted.OrderByDescending(r => r.TreasurySilver); break;
                case SortMode.LifetimeSilverIn: sorted = sorted.OrderByDescending(r => r.LifetimeSilverIn); break;
                case SortMode.Perks: sorted = sorted.OrderByDescending(r => r.TotalPerkLevels); break;
                case SortMode.Quests: sorted = sorted.OrderByDescending(r => r.QuestsCompletedByMembers); break;
                case SortMode.Contributions: sorted = sorted.OrderByDescending(r => r.SilverContributedByMembers); break;
                case SortMode.Sites: sorted = sorted.OrderByDescending(r => r.TotalSites); break;
                case SortMode.Allies: sorted = sorted.OrderByDescending(r => r.AlliesCount); break;
                case SortMode.Tenure: sorted = sorted.OrderByDescending(r => r.AvgMemberTenureDays); break;
                case SortMode.WorkerXp: sorted = sorted.OrderByDescending(r => r.TotalMemberWorkerXp); break;
            }
            var rows = sorted.ToList();

            // KMH 26.5.20.1: Resolve the local player's own guild so we
            // can mark its row with the ★ marker (same affordance as the
            // player leaderboard uses for "this is you").
            string myGuild = string.Empty;
            try
            {
                var snap = GameClient.Managers.GuildClientCache.Guild;
                if (snap != null && !string.IsNullOrEmpty(snap.Name)) myGuild = snap.Name;
            }
            catch { }

            float viewH = Mathf.Max(inner.height, rows.Count * rowH + 8f);
            Rect viewRect = new Rect(0f, 0f, inner.width - 16f, viewH);

            Widgets.BeginScrollView(inner, ref _scroll, viewRect);
            float ly = 0f;
            float[] cols = ColumnXs(viewRect.width);

            for (int i = 0; i < rows.Count; i++)
            {
                GuildLeaderboardEntry r = rows[i];
                Rect row = new Rect(0f, ly, viewRect.width, rowH);
                if (i % 2 == 0) Widgets.DrawAltRect(row);
                Widgets.DrawHighlightIfMouseover(row);

                bool isMyGuild = !string.IsNullOrEmpty(myGuild) &&
                    string.Equals(r.Name, myGuild, System.StringComparison.OrdinalIgnoreCase);
                string rank = i < 3 ? $"<color=yellow>#{i + 1}</color>" : $"<color=grey>#{i + 1}</color>";
                string nameRender = isMyGuild ? $"<color=#80ff80>★</color> <b>{r.Name}</b>" : $"<b>{r.Name}</b>";
                Widgets.Label(new Rect(cols[0], ly + 2f, cols[1] - cols[0], rowH - 4f), $"{rank}  {nameRender}");
                DialogLayout.DrawCenteredLabel(new Rect(cols[1], ly + 2f, cols[2] - cols[1], rowH - 4f), r.MemberCount.ToString());

                string tenure = r.AvgMemberTenureDays > 0 ? $"{r.AvgMemberTenureDays:0.#}d" : "<color=grey>—</color>";
                // KMH 26.5.20.1: All numeric cells center-aligned via the shared helper.
                switch (_view)
                {
                    case View.Standard:
                        DialogLayout.DrawCenteredLabel(new Rect(cols[2], ly + 2f, cols[3] - cols[2], rowH - 4f), $"{r.TreasurySilver}s");
                        DialogLayout.DrawCenteredLabel(new Rect(cols[3], ly + 2f, cols[4] - cols[3], rowH - 4f), $"{r.LifetimeSilverIn}s");
                        DialogLayout.DrawCenteredLabel(new Rect(cols[4], ly + 2f, cols[5] - cols[4], rowH - 4f), r.TotalPerkLevels.ToString());
                        DialogLayout.DrawCenteredLabel(new Rect(cols[5], ly + 2f, cols[6] - cols[5], rowH - 4f), r.QuestsCompletedByMembers.ToString());
                        DialogLayout.DrawCenteredLabel(new Rect(cols[6], ly + 2f, cols[7] - cols[6], rowH - 4f), r.TotalSites.ToString());
                        DialogLayout.DrawCenteredLabel(new Rect(cols[7], ly + 2f, viewRect.width - cols[7], rowH - 4f), tenure);
                        break;
                    case View.Diplomacy:
                        DialogLayout.DrawCenteredLabel(new Rect(cols[2], ly + 2f, cols[3] - cols[2], rowH - 4f),
                            r.AlliesCount > 0 ? $"<color=#80ff80>{r.AlliesCount}</color>" : "0");
                        DialogLayout.DrawCenteredLabel(new Rect(cols[3], ly + 2f, cols[4] - cols[3], rowH - 4f),
                            r.HostilesCount > 0 ? $"<color=#ff8080>{r.HostilesCount}</color>" : "0");
                        DialogLayout.DrawCenteredLabel(new Rect(cols[4], ly + 2f, cols[5] - cols[4], rowH - 4f), r.TotalSites.ToString());
                        DialogLayout.DrawCenteredLabel(new Rect(cols[5], ly + 2f, cols[6] - cols[5], rowH - 4f), $"{r.TreasurySilver}s");
                        DialogLayout.DrawCenteredLabel(new Rect(cols[6], ly + 2f, cols[7] - cols[6], rowH - 4f), $"{r.SilverContributedByMembers}s");
                        DialogLayout.DrawCenteredLabel(new Rect(cols[7], ly + 2f, viewRect.width - cols[7], rowH - 4f), tenure);
                        break;
                    case View.Activity:
                        DialogLayout.DrawCenteredLabel(new Rect(cols[2], ly + 2f, cols[3] - cols[2], rowH - 4f), r.TotalMemberWorkerXp.ToString());
                        DialogLayout.DrawCenteredLabel(new Rect(cols[3], ly + 2f, cols[4] - cols[3], rowH - 4f), $"{r.TotalMemberSilverEarned}s");
                        DialogLayout.DrawCenteredLabel(new Rect(cols[4], ly + 2f, cols[5] - cols[4], rowH - 4f), r.TotalMemberSitesBuilt.ToString());
                        DialogLayout.DrawCenteredLabel(new Rect(cols[5], ly + 2f, cols[6] - cols[5], rowH - 4f), r.QuestsCompletedByMembers.ToString());
                        DialogLayout.DrawCenteredLabel(new Rect(cols[6], ly + 2f, cols[7] - cols[6], rowH - 4f), $"{r.LifetimeSilverIn}s");
                        DialogLayout.DrawCenteredLabel(new Rect(cols[7], ly + 2f, viewRect.width - cols[7], rowH - 4f), tenure);
                        break;
                }

                ly += rowH;
            }
            if (rows.Count == 0)
                Widgets.Label(new Rect(6f, 6f, viewRect.width, 20f), "<color=grey>No guilds on this server yet.</color>");

            Widgets.EndScrollView();
        }

        private static float[] ColumnXs(float w)
        {
            // 8 cols evenly spaced — content varies per view.
            return new float[]
            {
                10f,
                w * 0.24f,
                w * 0.34f,
                w * 0.46f,
                w * 0.58f,
                w * 0.68f,
                w * 0.78f,
                w * 0.90f
            };
        }
    }
}
