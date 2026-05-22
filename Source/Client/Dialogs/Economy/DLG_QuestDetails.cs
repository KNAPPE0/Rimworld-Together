using GameClient.Dialogs.Default;
using GameClient.Files;
using GameClient.PacketManagers;
using Shared.Files.Economy;
using System;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs.Economy
{
    /// <summary>
    /// KMH: Full-detail view for a single quest. Opened by clicking the
    /// title row in <see cref="DLG_Quests"/>. Description renders without
    /// truncation; same action buttons as the row.
    /// </summary>
    public class DLG_QuestDetails : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(700f, 540f);

        private readonly QuestFile _quest;
        private Vector2 _scroll = Vector2.zero;

        public DLG_QuestDetails(QuestFile quest)
        {
            _quest = quest;
            Title = $"Quest #{quest?.Id} — {quest?.Title ?? "?"}";
            closeOnCancel = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect rect)
        {
            if (_quest == null)
            {
                Widgets.Label(new Rect(0f, 0f, rect.width, 22f), "Quest data missing.");
                return;
            }

            QuestFile q = _quest;
            string mine = PersistentSettings.Load().UserSettings.Username ?? string.Empty;

            // KMH 26.5.20.1: Shared title + divider so this dialog matches
            // the rest of the KMH family. Previously used `30f` for the
            // title height — a 2px drift from every other dialog.
            string kindEmoji = q.Kind == QuestKind.DeliverItem ? "📦" : "⚔";
            float y = DialogLayout.DrawTitle(rect, $"{kindEmoji} {q.Title}");
            DialogLayout.DrawSectionDivider(rect, ref y);

            // Meta strip
            string state;
            switch (q.State)
            {
                case QuestState.Open: state = "<color=#80ff80>OPEN</color>"; break;
                case QuestState.Claimed: state = "<color=yellow>CLAIMED</color>"; break;
                case QuestState.Submitted: state = "<color=cyan>SUBMITTED</color>"; break;
                case QuestState.Completed: state = "<color=grey>DONE</color>"; break;
                case QuestState.Cancelled: state = "<color=grey>CANCELLED</color>"; break;
                case QuestState.Expired: state = "<color=grey>EXPIRED</color>"; break;
                default: state = q.State.ToString(); break;
            }
            string claimedBy = !string.IsNullOrEmpty(q.ClaimedByUsername) ? $" · claimed by <b>{GameClient.Managers.LinkedAccountsCache.Format(q.ClaimedByUsername)}</b>" : "";
            string time = q.State == QuestState.Open ? $" · expires in {EconomyDialogUtil.FormatTimeRemaining(q.ExpiresUtcTicks)}" : "";
            Widgets.Label(new Rect(0f, y, rect.width, 22f),
                $"State: {state} · Kind: {q.Kind} · by <b>{GameClient.Managers.LinkedAccountsCache.Format(q.PosterUsername)}</b>{claimedBy}{time}");
            y += 28f;

            // Bounty
            string bounty = q.BountySilver > 0 ? $"<color=yellow>{q.BountySilver}s</color>" : "<color=grey>no silver</color>";
            int extraItems = q.BountyItems?.Count ?? 0;
            if (extraItems > 0) bounty += $" + {extraItems} item type(s)";
            Widgets.Label(new Rect(0f, y, rect.width, 22f), $"Bounty: {bounty}");
            y += 26f;

            if (q.Kind == QuestKind.DeliverItem)
            {
                // KMH 2.7: Friendly item label instead of a raw defName.
                Widgets.Label(new Rect(0f, y, rect.width, 22f),
                    $"Deliver: <b>{q.TargetItemQty}× {EconomyDialogUtil.ResolveLabel(q.TargetItemDefName)}</b>  →  <b>{q.TargetTreasuryKey}</b>");
                y += 26f;
            }

            // Description block (scrollable in case it's long).
            // KMH 26.5.20.1: Was `y += 6f` here — drift from the 8f shared divider.
            DialogLayout.DrawSectionDivider(rect, ref y);
            Widgets.Label(new Rect(0f, y, rect.width, 22f), "<b>Description</b>");
            y += 24f;

            float descAreaH = rect.height - y - 80f;
            Rect descBox = new Rect(0f, y, rect.width, descAreaH);
            Widgets.DrawMenuSection(descBox);

            string desc = string.IsNullOrWhiteSpace(q.Description) ? "<color=grey>(no description provided)</color>" : q.Description;
            float textH = Mathf.Max(descBox.height - 8f, Text.CalcHeight(desc, descBox.width - 24f) + 8f);
            Rect viewRect = new Rect(0f, 0f, descBox.width - 18f, textH);
            Widgets.BeginScrollView(descBox.ContractedBy(4f), ref _scroll, viewRect);
            Widgets.Label(new Rect(4f, 4f, viewRect.width - 8f, textH - 8f), desc);
            Widgets.EndScrollView();

            // KMH 2.7: Action buttons — auto-wrap to a second row when the
            // accumulated width would collide with the right-pinned Close
            // button. Previously, a poster on a Submitted Bounty quest would
            // render 4× 140px action buttons on a 700px-wide dialog and the
            // last button overflowed past the Close button at the right edge.
            const float btnW = 140f;
            const float btnSpacing = 6f;
            const float closeBtnW = 96f;
            // Reserve space for Close + a small gap so action buttons can't
            // collide with it on the bottom row.
            float actionMaxX = rect.width - closeBtnW - 10f;

            float btnY = rect.height - 40f;
            float bx = 0f;

            bool isPoster = string.Equals(q.PosterUsername, mine, StringComparison.OrdinalIgnoreCase);
            bool isClaimer = string.Equals(q.ClaimedByUsername, mine, StringComparison.OrdinalIgnoreCase);

            // Local helper: if placing the next btnW-wide button at bx would
            // overflow, wrap to a row above. Two rows max — anything beyond
            // would need its own scroll.
            void EnsureBtnSpace()
            {
                if (bx + btnW > actionMaxX)
                {
                    bx = 0f;
                    btnY -= 38f;
                }
            }

            if (q.State == QuestState.Open && !isPoster)
            {
                EnsureBtnSpace();
                if (Widgets.ButtonText(new Rect(bx, btnY, btnW, 32f), "Claim"))
                {
                    PM_Quest.Claim(q.Id);
                    Close();
                }
                bx += btnW + btnSpacing;
            }

            if (q.State == QuestState.Claimed && isClaimer && q.Kind == QuestKind.DeliverItem)
            {
                EnsureBtnSpace();
                if (Widgets.ButtonText(new Rect(bx, btnY, btnW, 32f), "Submit Delivery"))
                {
                    PM_Quest.SubmitDelivery(q.Id);
                    Close();
                }
                bx += btnW + btnSpacing;
            }

            if (q.State == QuestState.Claimed && isClaimer)
            {
                EnsureBtnSpace();
                if (Widgets.ButtonText(new Rect(bx, btnY, btnW, 32f), "Abandon"))
                {
                    PM_Quest.Abandon(q.Id);
                    Close();
                }
                bx += btnW + btnSpacing;
            }

            if (isPoster && (q.State == QuestState.Open || q.State == QuestState.Claimed || q.State == QuestState.Submitted))
            {
                if (q.Kind == QuestKind.Bounty && q.State == QuestState.Submitted)
                {
                    EnsureBtnSpace();
                    if (Widgets.ButtonText(new Rect(bx, btnY, btnW, 32f), "Confirm + Pay"))
                    {
                        PM_Quest.ConfirmBountyCompletion(q.Id, q.ClaimedByUsername);
                        Close();
                    }
                    bx += btnW + btnSpacing;
                }

                EnsureBtnSpace();
                if (Widgets.ButtonText(new Rect(bx, btnY, btnW, 32f), "Cancel Quest"))
                {
                    PM_Quest.Cancel(q.Id);
                    Close();
                }
                bx += btnW + btnSpacing;
            }

            // Close button stays pinned on the bottom row regardless of where
            // the action buttons wrapped.
            if (Widgets.ButtonText(new Rect(rect.width - closeBtnW - 4f, rect.height - 40f, closeBtnW, 32f), "Close"))
                Close();
        }
    }
}
