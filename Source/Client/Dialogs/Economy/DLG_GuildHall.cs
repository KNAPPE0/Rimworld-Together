using GameClient.Dialogs.Default;
using GameClient.Files;
using GameClient.Managers;
using GameClient.PacketManagers;
using Shared.Files.Guilds;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs.Economy
{
    /// <summary>
    /// Guild dashboard: members + contributions, perks, settings, MOTD.
    /// </summary>
    public class DLG_GuildHall : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(940f, 640f);

        private Vector2 _membersScroll = Vector2.zero;
        private Vector2 _perksScroll = Vector2.zero;

        // KMH 26.5.20.1: Member list ordering cache — rebuilds only when
        // the GuildMembers list reference changes (i.e. a fresh snapshot
        // landed). Stops the per-frame OrderByDescending+ToList in the
        // members panel.
        private List<GuildMember> _orderedMembersCache;
        private object _orderedMembersSource;

        // KMH 26.5.20.1: Pre-rendered diplomacy line. Was allocating three
        // List<string>s + a handful of strings per frame just to render
        // one static line. Refresh only when the Relationships dictionary
        // reference flips (next snapshot push).
        private string _diplomacyLineCache;
        private object _diplomacyLineSource;

        public DLG_GuildHall()
        {
            Title = "Guild Hall";
            closeOnCancel = true;
            absorbInputAroundWindow = true;

            PM_GuildHall.RequestSnapshot();
            GuildClientCache.OnSnapshotUpdated += MarkRedraw;
        }

        public override void PostClose()
        {
            base.PostClose();
            GuildClientCache.OnSnapshotUpdated -= MarkRedraw;
        }

        private void MarkRedraw() { /* placeholder */ }

        public override void DoWindowContents(Rect rect)
        {
            // KMH 26.5.20.1: Shared title + divider via DialogLayout. Was
            // using `y += 6f` after the divider while every other dialog
            // used 8f — that 2px drift is exactly what DialogLayout is for.
            string headerName = GuildClientCache.Guild?.Name ?? "(no guild)";
            float y = DialogLayout.DrawTitle(rect, $"Guild Hall — {headerName}");

            if (!GuildClientCache.HasSnapshot || GuildClientCache.Guild == null)
            {
                Widgets.Label(new Rect(0f, 40f, rect.width, 22f), "Loading guild snapshot…");
                return;
            }

            GuildFile g = GuildClientCache.Guild;
            string mine = PersistentSettings.Load().UserSettings.Username ?? string.Empty;
            GuildMember myMember = g.GuildMembers.FirstOrDefault(m =>
                string.Equals(m.Username, mine, StringComparison.OrdinalIgnoreCase));
            GuildMember.GuildRanks myRank = myMember?.Rank ?? GuildMember.GuildRanks.Member;

            DialogLayout.DrawSectionDivider(rect, ref y);

            // MOTD
            string motd = string.IsNullOrWhiteSpace(g.Settings?.MessageOfTheDay)
                ? "<color=grey>(no message of the day)</color>"
                : g.Settings.MessageOfTheDay;
            Widgets.Label(new Rect(0f, y, rect.width, 22f), $"<b>MOTD:</b> {motd}");
            y += 26f;

            // KMH 26.5.20.1: Shorter labels + tiny font so the stats line
            // doesn't truncate to "Site rewa..." on narrower dialog widths.
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            int totalMembers = g.GuildMembers?.Count ?? 0;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(0f, y, rect.width, 18f),
                $"Members: {totalMembers}    Site tax: {g.Settings.SiteRewardSilverTaxPercent}%    Market tax: {g.Settings.MarketplaceSaleTaxPercent}%");
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            y += 22f;

            // Two-pane: members on left, perks on right
            float paneH = rect.height - y - 70f;
            float leftW = rect.width * 0.55f;
            float rightX = leftW + 8f;
            float rightW = rect.width - rightX;

            Widgets.Label(new Rect(0f, y, leftW, 20f), "<b>Members & Contributions</b>");
            Rect leftBox = new Rect(0f, y + 22f, leftW, paneH - 22f);
            Widgets.DrawMenuSection(leftBox);
            DrawMembers(leftBox, g, mine, myRank);

            Widgets.Label(new Rect(rightX, y, rightW, 20f), "<b>Perks</b>");
            Rect rightBox = new Rect(rightX, y + 22f, rightW, paneH - 22f);
            Widgets.DrawMenuSection(rightBox);
            DrawPerks(rightBox, g, myRank);

            // Diplomacy line
            DrawDiplomacyLine(g, rect);

            // KMH 26.5.20.1: Single-row bottom toolbar with menu-style
            // sub-groups. Was 6 separate buttons auto-wrapping into messy
            // two rows when the dialog was narrow. Now: 3-4 grouped
            // entry points that open FloatMenus for sub-actions.
            //
            //   [Manage ▾] (Admin: Settings / Distribute Bonus / Diplomacy)
            //   [Leaderboards ▾] (Guild Ranks / Player Ranks)
            //   [Treasury]
            //   ...
            //   [Close]
            //
            // Cleaner visual + room to grow without overflow churn.
            const float btnW = 150f;
            const float btnSpacing = 8f;
            const float closeBtnW = 96f;

            float btnY = rect.height - 44f;
            float bx = 0f;

            if (myRank == GuildMember.GuildRanks.Admin)
            {
                if (Widgets.ButtonText(new Rect(bx, btnY, btnW, 32f), "Manage ▾"))
                {
                    List<FloatMenuOption> opts = new List<FloatMenuOption>
                    {
                        new FloatMenuOption("Edit Settings…", () => Find.WindowStack.Add(new DLG_GuildSettings(g.Settings))),
                        new FloatMenuOption("Distribute Bonus…", PromptDistributeBonus),
                        new FloatMenuOption("Diplomacy…", ShowDiplomacyMenu),
                    };
                    Find.WindowStack.Add(new FloatMenu(opts));
                }
                bx += btnW + btnSpacing;
            }

            if (Widgets.ButtonText(new Rect(bx, btnY, btnW, 32f), "Leaderboards ▾"))
            {
                List<FloatMenuOption> opts = new List<FloatMenuOption>
                {
                    new FloatMenuOption("Guild Ranks", () => Find.WindowStack.Add(new DLG_GuildLeaderboard())),
                    new FloatMenuOption("Player Ranks", () => Find.WindowStack.Add(new DLG_PlayerLeaderboard())),
                };
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            bx += btnW + btnSpacing;

            if (Widgets.ButtonText(new Rect(bx, btnY, btnW, 32f), "Treasury"))
            {
                Close();
                DLG_Base.PushNewDialog(new DLG_Treasury());
            }
            // bx not advanced — Close pins right.

            // Close stays pinned on the bottom-right.
            if (Widgets.ButtonText(new Rect(rect.width - closeBtnW - 4f, btnY, closeBtnW, 32f), "Close"))
                Close();
        }

        private void DrawDiplomacyLine(GuildFile g, Rect rect)
        {
            if (g.Relationships == null || g.Relationships.Count == 0) return;
            float lineY = rect.height - 88f;

            // KMH 26.5.20.1: Cache the rendered string; the diplomacy line
            // is static between snapshot pushes. Was allocating 3 Lists +
            // multiple strings every frame.
            if (_diplomacyLineCache == null
                || !ReferenceEquals(_diplomacyLineSource, g.Relationships))
            {
                List<string> allies = new List<string>();
                List<string> requested = new List<string>();
                List<string> hostiles = new List<string>();
                foreach (var kv in g.Relationships)
                {
                    switch (kv.Value)
                    {
                        case AllianceRelation.Allied: allies.Add(kv.Key); break;
                        case AllianceRelation.AlliedRequested: requested.Add(kv.Key); break;
                        case AllianceRelation.Hostile: hostiles.Add(kv.Key); break;
                    }
                }

                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                if (allies.Count > 0) sb.Append("<color=#80ff80>Allied:</color> ").Append(string.Join(", ", allies)).Append("  ");
                if (requested.Count > 0) sb.Append("<color=yellow>Pending:</color> ").Append(string.Join(", ", requested)).Append("  ");
                if (hostiles.Count > 0) sb.Append("<color=#ff8080>Hostile:</color> ").Append(string.Join(", ", hostiles));

                _diplomacyLineCache = sb.ToString();
                _diplomacyLineSource = g.Relationships;
            }

            Widgets.Label(new Rect(0f, lineY, rect.width, 22f), _diplomacyLineCache);
        }

        private void ShowDiplomacyMenu()
        {
            DLG_Base.PushNewDialog(new DLG_Inputs(
                "Diplomacy",
                new[] { "Other guild name" },
                new[] { false },
                delegate
                {
                    string other = DLG_Inputs.DialogInputResults[0]?.Trim();
                    if (string.IsNullOrEmpty(other)) return;

                    List<FloatMenuOption> opts = new List<FloatMenuOption>
                    {
                        new FloatMenuOption("Propose Alliance", () => PM_GuildHall.ProposeAlliance(other)),
                        new FloatMenuOption("Break Alliance / Cancel Proposal", () => PM_GuildHall.BreakAlliance(other)),
                        new FloatMenuOption("Declare Hostile", () => PM_GuildHall.DeclareHostile(other)),
                    };
                    Find.WindowStack.Add(new FloatMenu(opts));
                }));
        }

        private void PromptDistributeBonus()
        {
            DLG_Base.PushNewDialog(new DLG_Inputs(
                "Distribute Treasury Bonus",
                new[] { "Total silver to split equally among all members" },
                new[] { false },
                delegate
                {
                    if (int.TryParse(DLG_Inputs.DialogInputResults[0]?.Trim(), out int amount) && amount > 0)
                        PM_GuildHall.DistributeBonus(amount);
                }));
        }

        private void DrawMembers(Rect box, GuildFile g, string mine, GuildMember.GuildRanks myRank)
        {
            Rect inner = box.ContractedBy(4f);
            const float rowH = 30f;

            // KMH 26.5.20.1: Cache the ordered member list; rebuild only
            // when the underlying GuildMembers reference flips (next
            // snapshot push).
            if (_orderedMembersCache == null
                || !ReferenceEquals(_orderedMembersSource, g.GuildMembers))
            {
                _orderedMembersCache = g.GuildMembers
                    .OrderByDescending(m => m.SilverContributed)
                    .ToList();
                _orderedMembersSource = g.GuildMembers;
            }
            List<GuildMember> ordered = _orderedMembersCache;

            float viewH = Mathf.Max(inner.height, ordered.Count * rowH + 8f);
            Rect viewRect = new Rect(0f, 0f, inner.width - 16f, viewH);

            Widgets.BeginScrollView(inner, ref _membersScroll, viewRect);
            float ly = 0f;
            for (int i = 0; i < ordered.Count; i++)
            {
                GuildMember m = ordered[i];
                Rect row = new Rect(0f, ly, viewRect.width, rowH);
                if (i % 2 == 0) Widgets.DrawAltRect(row);
                Widgets.DrawHighlightIfMouseover(row);

                string rankColor;
                switch (m.Rank)
                {
                    case GuildMember.GuildRanks.Admin: rankColor = "#ff7d7d"; break;
                    case GuildMember.GuildRanks.Moderator: rankColor = "#ffce4d"; break;
                    case GuildMember.GuildRanks.Officer: rankColor = "#7dd3ff"; break;
                    default: rankColor = "#cccccc"; break;
                }

                string label = $"<b>{LinkedAccountsCache.Format(m.Username)}</b>  <color={rankColor}>[{m.Rank}]</color>  " +
                               $"<color=grey>{m.SilverContributed}s donated · {m.ItemsContributed} items</color>";

                Widgets.Label(new Rect(6f, ly + 6f, viewRect.width - 100f, rowH - 8f), label);

                // Admin-only: rank-pick button
                if (myRank == GuildMember.GuildRanks.Admin &&
                    !string.Equals(m.Username, mine, StringComparison.OrdinalIgnoreCase))
                {
                    Rect btn = new Rect(viewRect.width - 90f, ly + 3f, 86f, rowH - 6f);
                    if (Widgets.ButtonText(btn, "Rank…"))
                        ShowRankMenu(m);
                }

                ly += rowH;
            }
            Widgets.EndScrollView();
        }

        private void ShowRankMenu(GuildMember target)
        {
            List<FloatMenuOption> opts = new List<FloatMenuOption>();
            foreach (GuildMember.GuildRanks r in System.Enum.GetValues(typeof(GuildMember.GuildRanks)))
            {
                GuildMember.GuildRanks captured = r;
                opts.Add(new FloatMenuOption($"Set {target.Username} → {r}", () =>
                    PM_GuildHall.SetRank(target.Username, captured)));
            }
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        private void DrawPerks(Rect box, GuildFile g, GuildMember.GuildRanks myRank)
        {
            Rect inner = box.ContractedBy(4f);
            const float rowH = 56f;

            var perks = new[]
            {
                ( "Site Worker Capacity (+2/lvl)", g.Perks.SiteMaxWorkersBonusLevel, "SiteMaxWorkers" ),
                ( "Marketplace Tax Reduction (-1pp/lvl)", g.Perks.MarketplaceTaxReductionLevel, "MarketplaceTax" ),
                ( "Worker XP Bonus (+25%/lvl, L3=2x)", g.Perks.WorkerXpBonusLevel, "WorkerXp" ),
                ( "Custom Site Discount (-10%/lvl)", g.Perks.CustomSiteCostDiscountLevel, "CustomSiteCost" ),
            };

            float viewH = Mathf.Max(inner.height, perks.Length * rowH + 8f);
            Rect viewRect = new Rect(0f, 0f, inner.width - 16f, viewH);

            Widgets.BeginScrollView(inner, ref _perksScroll, viewRect);
            float ly = 0f;
            int idx = 0;
            foreach (var p in perks)
            {
                Rect row = new Rect(0f, ly, viewRect.width, rowH);
                if (idx % 2 == 0) Widgets.DrawAltRect(row);
                Widgets.DrawHighlightIfMouseover(row);

                Widgets.Label(new Rect(6f, ly + 4f, viewRect.width - 100f, 22f), $"<b>{p.Item1}</b>");

                int lvl = p.Item2;
                int next = lvl + 1;
                bool maxed = lvl >= GuildPerks.MaxLevel;
                int cost = maxed ? 0 : GuildPerks.CostFor(lvl);

                GUI.color = new Color(0.85f, 0.85f, 0.85f);
                string detail = maxed
                    ? $"Lv {lvl}/{GuildPerks.MaxLevel}  ·  <color=grey>maxed</color>"
                    : $"Lv {lvl}/{GuildPerks.MaxLevel}  →  Lv {next} for <color=yellow>{cost}s</color>";
                Widgets.Label(new Rect(6f, ly + 26f, viewRect.width - 100f, 22f), detail);
                GUI.color = Color.white;

                if (!maxed && (myRank == GuildMember.GuildRanks.Admin || myRank == GuildMember.GuildRanks.Moderator || myRank == GuildMember.GuildRanks.Officer))
                {
                    int kindIdx = idx;
                    string kindStr = p.Item3;
                    if (Widgets.ButtonText(new Rect(viewRect.width - 90f, ly + 14f, 86f, 28f), "Buy"))
                        PM_GuildHall.PurchasePerk(MapKindStringToInt(kindStr));
                }

                ly += rowH;
                idx++;
            }
            Widgets.EndScrollView();
        }

        private static int MapKindStringToInt(string kind)
        {
            switch (kind)
            {
                case "SiteMaxWorkers": return 0;
                case "MarketplaceTax": return 1;
                case "WorkerXp": return 2;
                case "CustomSiteCost": return 3;
                default: return 0;
            }
        }
    }
}
