using GameClient.Dialogs.Default;
using GameClient.Files;
using GameClient.Managers;
using GameClient.PacketManagers;
using RimWorld;
using Shared.Files.Economy;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
// KMH: disambiguate from RimWorld.QuestState (vanilla quests).
using QuestState = Shared.Files.Economy.QuestState;
using QuestKind = Shared.Files.Economy.QuestKind;

namespace GameClient.Dialogs.Economy
{
    /// <summary>
    /// Quest board: browse all open + claimed quests, claim/abandon/submit/cancel,
    /// and post new quests via <see cref="DLG_PostQuest"/>.
    /// </summary>
    public class DLG_Quests : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(900f, 620f);

        private Vector2 _scroll = Vector2.zero;
        private string _filter = "";
        private bool _onlyMine;
        private bool _onlyOpen = true;
        // KMH: Visibility-scope filters — show only personal-poster or only guild-poster.
        private bool _onlyGuild;
        private bool _onlyPersonal;

        // KMH 26.5.20.1: Cache the filtered/sorted quest view so we don't
        // re-run Where/OrderBy/ToList per frame (60fps). Invalidated when
        // any input — filter text, toggles, or the underlying snapshot —
        // changes. Same pattern as DLG_Marketplace / DLG_PlayerLeaderboard.
        private List<Shared.Files.Economy.QuestFile> _visibleCache;
        private object _visibleCacheSource;
        private string _visibleCacheFilter;
        private bool _vcOnlyMine, _vcOnlyOpen, _vcOnlyGuild, _vcOnlyPersonal;

        public DLG_Quests()
        {
            Title = "Quest Board";
            closeOnCancel = true;
            absorbInputAroundWindow = true;

            PM_Quest.RequestBoard();
            QuestClientCache.OnSnapshotUpdated += MarkRedraw;
        }

        public override void PostClose()
        {
            base.PostClose();
            QuestClientCache.OnSnapshotUpdated -= MarkRedraw;
        }

        private void MarkRedraw() { /* hook */ }

        public override void DoWindowContents(Rect rect)
        {
            // KMH 26.5.20.1: Standard title + section divider via DialogLayout.
            float y = DialogLayout.DrawTitle(rect, "Quest Board");
            DialogLayout.DrawSectionDivider(rect, ref y);

            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(0f, y, rect.width, 20f),
                $"Posted: {QuestClientCache.LifetimeQuestsPosted}  |  " +
                $"Completed: {QuestClientCache.LifetimeQuestsCompleted}  |  " +
                $"Lifetime bounty: {QuestClientCache.LifetimeBountySilverPaid}s");
            GUI.color = Color.white;
            y += 24f;

            // KMH 2.7: Two-row toolbar so the 4 checkboxes don't overlap
            // Refresh / Post Quest at narrower window widths.
            const float btnW = 110f;
            // Row 1: search box + right-pinned action buttons.
            Rect searchRect = new Rect(0f, y, 320f, 28f);
            _filter = Widgets.TextField(searchRect, _filter ?? "");
            if (string.IsNullOrWhiteSpace(_filter))
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.3f);
                Widgets.Label(searchRect.ContractedBy(6f, 4f), "Filter by title, item, poster…");
                GUI.color = old;
            }
            float postX = rect.width - btnW;
            float refreshX = postX - 4f - btnW;
            if (Widgets.ButtonText(new Rect(refreshX, y, btnW, 28f), "Refresh"))
                PM_Quest.RequestBoard();
            if (Widgets.ButtonText(new Rect(postX, y, btnW, 28f), "Post Quest…"))
                Find.WindowStack.Add(new DLG_PostQuest());
            y += 32f;

            // KMH 26.5.20.1: Row 2 filter checkboxes via DialogLayout.DrawTightCheckbox
            // so the ☐ marker sits flush against each label instead of floating
            // 80–100 px to the right of "My quests" / "Open only" / etc.
            float cbx = 0f;
            cbx = DialogLayout.DrawTightCheckbox(cbx, y + 4f, "My quests", ref _onlyMine);
            cbx = DialogLayout.DrawTightCheckbox(cbx, y + 4f, "Open only", ref _onlyOpen);
            cbx = DialogLayout.DrawTightCheckbox(cbx, y + 4f, "Guild only", ref _onlyGuild);
            cbx = DialogLayout.DrawTightCheckbox(cbx, y + 4f, "Personal only", ref _onlyPersonal);
            y += 30f;

            Rect listBox = new Rect(0f, y, rect.width, rect.height - y - 44f);
            Widgets.DrawMenuSection(listBox);
            DrawQuestList(listBox);

            if (DialogLayout.DrawCloseButton(rect)) Close();
        }

        private void DrawQuestList(Rect box)
        {
            Rect inner = box.ContractedBy(4f);
            const float rowH = 64f;

            string mine = PersistentSettings.Load().UserSettings.Username ?? string.Empty;
            string filter = (_filter ?? "").Trim().ToLower();

            // KMH 26.5.20.1: Only rebuild the filtered/sorted list when an
            // input changes. The dialog redraws at 60fps; without this we'd
            // be running Where/OrderBy/ToList every frame for every quest.
            object curSource = QuestClientCache.Quests;
            bool inputsChanged =
                _visibleCache == null
                || !ReferenceEquals(_visibleCacheSource, curSource)
                || _visibleCacheFilter != filter
                || _vcOnlyMine != _onlyMine
                || _vcOnlyOpen != _onlyOpen
                || _vcOnlyGuild != _onlyGuild
                || _vcOnlyPersonal != _onlyPersonal;

            if (inputsChanged)
            {
                _visibleCache = QuestClientCache.Quests.Where(q =>
                    (!_onlyOpen || q.State == QuestState.Open || q.State == QuestState.Claimed || q.State == QuestState.Submitted) &&
                    (!_onlyMine
                        || string.Equals(q.PosterUsername, mine, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(q.ClaimedByUsername, mine, StringComparison.OrdinalIgnoreCase)) &&
                    // KMH: Source filters — guild-posted vs personal-posted.
                    // Identified via PosterTreasuryKey: starts with "_personal:" → personal poster.
                    (!_onlyGuild
                        || (!string.IsNullOrEmpty(q.PosterTreasuryKey) && !q.PosterTreasuryKey.StartsWith("_personal:", StringComparison.OrdinalIgnoreCase))) &&
                    (!_onlyPersonal
                        || (string.IsNullOrEmpty(q.PosterTreasuryKey) || q.PosterTreasuryKey.StartsWith("_personal:", StringComparison.OrdinalIgnoreCase))) &&
                    (string.IsNullOrEmpty(filter)
                        || (q.Title ?? "").ToLower().Contains(filter)
                        || (q.PosterUsername ?? "").ToLower().Contains(filter)
                        || (q.TargetItemDefName ?? "").ToLower().Contains(filter)))
                    .OrderByDescending(q => q.PostedUtcTicks)
                    .ToList();

                _visibleCacheSource = curSource;
                _visibleCacheFilter = filter;
                _vcOnlyMine = _onlyMine;
                _vcOnlyOpen = _onlyOpen;
                _vcOnlyGuild = _onlyGuild;
                _vcOnlyPersonal = _onlyPersonal;
            }

            List<Shared.Files.Economy.QuestFile> visible = _visibleCache;

            float viewH = Mathf.Max(inner.height, visible.Count * rowH + 8f);
            Rect viewRect = new Rect(0f, 0f, inner.width - 16f, viewH);

            Widgets.BeginScrollView(inner, ref _scroll, viewRect);
            float ly = 0f;
            int idx = 0;
            foreach (QuestFile q in visible)
            {
                Rect row = new Rect(0f, ly, viewRect.width, rowH);
                if (idx % 2 == 0) Widgets.DrawAltRect(row);
                Widgets.DrawHighlightIfMouseover(row);

                DrawQuestRow(row, q, mine);

                ly += rowH;
                idx++;
            }
            if (visible.Count == 0)
                Widgets.Label(new Rect(6f, 6f, viewRect.width, 20f), "<color=grey>No quests match.</color>");

            Widgets.EndScrollView();
        }

        private void DrawQuestRow(Rect row, QuestFile q, string myUsername)
        {
            Rect inner = row.ContractedBy(6f);
            float btnW = 100f;
            float btnsX = inner.width - btnW - 6f;

            // Title + state
            string stateTag;
            switch (q.State)
            {
                case QuestState.Open: stateTag = "<color=#80ff80>OPEN</color>"; break;
                case QuestState.Claimed: stateTag = "<color=yellow>CLAIMED</color>"; break;
                case QuestState.Submitted: stateTag = "<color=cyan>SUBMITTED</color>"; break;
                case QuestState.Completed: stateTag = "<color=grey>DONE</color>"; break;
                case QuestState.Cancelled: stateTag = "<color=grey>CANCELLED</color>"; break;
                case QuestState.Expired: stateTag = "<color=grey>EXPIRED</color>"; break;
                default: stateTag = q.State.ToString(); break;
            }

            string kindTag = q.Kind == QuestKind.DeliverItem ? "📦 Deliver" : "⚔ Bounty";

            // KMH: Ownership badge — solo player vs guild quest.
            bool isGuildPosted = !string.IsNullOrEmpty(q.PosterTreasuryKey) &&
                                 !q.PosterTreasuryKey.StartsWith("_personal:", StringComparison.OrdinalIgnoreCase);
            string ownership = isGuildPosted
                ? $"<color=#79b8ff>🏰 {q.PosterTreasuryKey}</color>"
                : "<color=#cccccc>👤 personal</color>";

            // KMH: Visibility tag if the quest is guild-only.
            string visTag = q.Visibility == QuestVisibility.GuildOnly
                ? " <color=#ffce4d>🔒 guild-only</color>"
                : string.Empty;

            Rect titleRect = new Rect(inner.x, inner.y, btnsX - inner.x, 18f);
            Widgets.Label(titleRect, $"<b>#{q.Id}  {q.Title}</b>  {stateTag}{visTag}  <color=grey>{kindTag} • by</color> {LinkedAccountsCache.Format(q.PosterUsername)} <color=grey>• {ownership}</color>");
            // KMH: Click the title to pop the full quest details dialog.
            if (Widgets.ButtonInvisible(titleRect))
                Find.WindowStack.Add(new DLG_QuestDetails(q));

            // KMH: Detail line — show what the quest asks for. Both kinds get
            // the description (when present) underneath. The previous version
            // dropped Description entirely on DeliverItem quests.
            string detail;
            if (q.Kind == QuestKind.DeliverItem)
                // KMH 2.7: Friendly item label in the quest row instead of a raw defName.
                detail = $"Deliver {q.TargetItemQty}× {EconomyDialogUtil.ResolveLabel(q.TargetItemDefName)}  →  {q.TargetTreasuryKey}";
            else
                detail = "Bounty";

            string desc = q.Description ?? string.Empty;
            if (desc.Length > 0)
            {
                string trimmed = desc.Length > 90 ? desc.Substring(0, 90) + "…" : desc;
                detail += "  ·  " + trimmed;
            }

            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            Widgets.Label(new Rect(inner.x, inner.y + 18f, btnsX - inner.x, 18f), detail);
            GUI.color = Color.white;

            // Bounty + expiry
            string bounty = q.BountySilver > 0 ? $"<color=yellow>{q.BountySilver}s</color>" : "<color=grey>no silver</color>";
            int extraItems = q.BountyItems?.Count ?? 0;
            if (extraItems > 0) bounty += $" + {extraItems} item type(s)";
            string time = q.State == QuestState.Open ? $"  •  expires in {EconomyDialogUtil.FormatTimeRemaining(q.ExpiresUtcTicks)}" : "";
            string claimedBy = !string.IsNullOrEmpty(q.ClaimedByUsername) ? $"  •  claimed by {LinkedAccountsCache.Format(q.ClaimedByUsername)}" : "";
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(inner.x, inner.y + 36f, btnsX - inner.x, 18f), $"Bounty: {bounty}{claimedBy}{time}");
            GUI.color = Color.white;

            // Buttons
            DrawRowButtons(new Rect(btnsX, inner.y + 4f, btnW, 26f), new Rect(btnsX, inner.y + 32f, btnW, 26f), q, myUsername);
        }

        private void DrawRowButtons(Rect topBtn, Rect bottomBtn, QuestFile q, string mine)
        {
            bool isPoster = string.Equals(q.PosterUsername, mine, StringComparison.OrdinalIgnoreCase);
            bool isClaimer = string.Equals(q.ClaimedByUsername, mine, StringComparison.OrdinalIgnoreCase);

            // Top button
            if (q.State == QuestState.Open)
            {
                if (!isPoster && Widgets.ButtonText(topBtn, "Claim"))
                    PM_Quest.Claim(q.Id);
            }
            else if ((q.State == QuestState.Claimed || q.State == QuestState.Submitted) && isClaimer)
            {
                if (q.Kind == QuestKind.DeliverItem)
                {
                    if (Widgets.ButtonText(topBtn, "Submit"))
                        PromptSubmitDelivery(q);
                }
                else
                {
                    GUI.color = new Color(0.7f, 0.7f, 0.7f);
                    Widgets.Label(topBtn, " awaiting poster");
                    GUI.color = Color.white;
                }
            }

            // Bottom button
            if (isPoster && (q.State == QuestState.Open || q.State == QuestState.Claimed || q.State == QuestState.Submitted))
            {
                if (q.Kind == QuestKind.Bounty && q.State == QuestState.Claimed)
                {
                    if (Widgets.ButtonText(bottomBtn, "Confirm Done"))
                        PM_Quest.ConfirmBountyCompletion(q.Id, q.ClaimedByUsername);
                }
                else
                {
                    if (Widgets.ButtonText(bottomBtn, "Cancel"))
                        PM_Quest.Cancel(q.Id);
                }
            }
            else if (isClaimer && (q.State == QuestState.Claimed || q.State == QuestState.Submitted))
            {
                if (Widgets.ButtonText(bottomBtn, "Abandon"))
                    PM_Quest.Abandon(q.Id);
            }
        }

        private void PromptSubmitDelivery(QuestFile q)
        {
            // Items must already be in the claimer's treasury.
            DLG_Base.PushNewDialog(new DLG_YesNo(
                $"Submit {q.TargetItemQty}× {q.TargetItemDefName} from your treasury for quest #{q.Id}?\n" +
                $"(Make sure the items are deposited there first.)",
                delegate { PM_Quest.SubmitDelivery(q.Id); }));
        }
    }
}
