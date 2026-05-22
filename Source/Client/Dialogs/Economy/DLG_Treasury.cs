using GameClient.Dialogs.Default;
using GameClient.Managers;
using GameClient.Misc;
using GameClient.PacketManagers;
using RimWorld;
using RimWorld.Planet;
using Shared.Files.Economy;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs.Economy
{
    /// <summary>
    /// Browse + deposit + withdraw a treasury vault. Works for both guild
    /// and personal treasuries — the server resolves which one based on the
    /// caller's GuildName.
    /// </summary>
    public class DLG_Treasury : DLG_Base
    {
        // KMH 26.5.20.1: 720 → 980 wide so the Recent Activity right pane
        // gets ~440 px (was ~316 px) — enough to render the longest
        // transaction strings ("Withdraw  <player>  ×123 Packaged survival
        // meal  · note text") on a single line without truncation.
        public override Vector2 InitialSize => new Vector2(980f, 580f);

        private Vector2 _itemScroll = Vector2.zero;
        private Vector2 _txScroll = Vector2.zero;

        public DLG_Treasury()
        {
            Title = "Treasury";
            closeOnCancel = true;
            absorbInputAroundWindow = true;

            // Refresh from server on open + subscribe so any inbound snapshot redraws.
            PM_Treasury.RequestSnapshot();
            TreasuryClientCache.OnSnapshotUpdated += MarkRedrawNeeded;
        }

        public override void PostClose()
        {
            base.PostClose();
            TreasuryClientCache.OnSnapshotUpdated -= MarkRedrawNeeded;
        }

        private void MarkRedrawNeeded() { /* Verse redraws every frame; just need cache refresh */ }

        public override void DoWindowContents(Rect rect)
        {
            // KMH 26.5.20.1: Shared title + section divider via DialogLayout.
            string title = TreasuryClientCache.IsGuildOwned
                ? $"Guild Treasury — {TreasuryClientCache.OwnerKey}"
                : "Personal Vault";
            float y = DialogLayout.DrawTitle(rect, title);

            if (!TreasuryClientCache.HasSnapshot)
            {
                Widgets.Label(new Rect(0f, 40f, rect.width, 20f), "Loading treasury…");
                return;
            }

            DialogLayout.DrawSectionDivider(rect, ref y);

            // Top row: silver + lifetime stats
            Widgets.Label(new Rect(0f, y, rect.width, 24f),
                $"<b>Silver:</b> {TreasuryClientCache.SilverBalance}    " +
                $"<color=grey>(in: {TreasuryClientCache.LifetimeSilverIn} | out: {TreasuryClientCache.LifetimeSilverOut})</color>");
            y += 28f;

            // Permission banner
            string perm = "";
            if (TreasuryClientCache.CanDeposit) perm += "Deposit ";
            if (TreasuryClientCache.CanWithdraw) perm += "Withdraw ";
            if (string.IsNullOrEmpty(perm)) perm = "Read-only";
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(0f, y, rect.width, 20f), $"Permissions: {perm.Trim()}");
            GUI.color = Color.white;
            y += 24f;

            // KMH 26.5.20.1: 50/50 split instead of 55/45 — the items list
            // doesn't need extra width since rows are icon+name+button, but
            // Recent Activity benefits enormously from the extra ~30 px.
            float paneH = rect.height - y - 80f;
            float leftW = rect.width * 0.5f - 4f;
            float rightX = leftW + 8f;
            float rightW = rect.width - rightX;

            // Items pane
            Widgets.Label(new Rect(0f, y, leftW, 20f), "<b>Items</b>");
            Rect itemsBox = new Rect(0f, y + 22f, leftW, paneH - 22f);
            Widgets.DrawMenuSection(itemsBox);
            DrawItemsList(itemsBox);

            // Transactions pane
            Widgets.Label(new Rect(rightX, y, rightW, 20f), "<b>Recent Activity</b>");
            Rect txBox = new Rect(rightX, y + 22f, rightW, paneH - 22f);
            Widgets.DrawMenuSection(txBox);
            DrawTransactionsList(txBox);

            // KMH 2.7: Consolidated button row. The original row had up to 7
            // buttons at 140 px each which overran the dialog and overlapped
            // the close button. Now Deposit/Withdraw open a sub-menu so the
            // bar always fits regardless of permissions.
            float btnY = rect.height - 40f;
            const float btnW = 140f;
            float bx = 0f;

            if (TreasuryClientCache.CanDeposit)
            {
                if (Widgets.ButtonText(new Rect(bx, btnY, btnW, 32f), "Deposit ▾"))
                    OpenDepositMenu();
                bx += btnW + 8f;
            }

            if (TreasuryClientCache.CanWithdraw)
            {
                if (Widgets.ButtonText(new Rect(bx, btnY, btnW, 32f), "Withdraw ▾"))
                    OpenWithdrawMenu();
                bx += btnW + 8f;
            }

            if (Widgets.ButtonText(new Rect(bx, btnY, btnW, 32f), "Activity Log"))
                Find.WindowStack.Add(new DLG_TreasuryLogs());

            // Close pinned to the right edge so it never collides with the
            // left-side action buttons even when the dialog is at minimum width.
            if (Widgets.ButtonText(new Rect(rect.width - btnW, btnY, btnW, 32f), "Close"))
                Close();
        }

        private void OpenDepositMenu()
        {
            List<FloatMenuOption> opts = new List<FloatMenuOption>
            {
                new FloatMenuOption("Deposit silver…", PromptDepositSilver),
                new FloatMenuOption("Deposit items…", PromptDepositItems),
                new FloatMenuOption("Stash all caravan silver", StashAllSilver)
            };
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        private void OpenWithdrawMenu()
        {
            List<FloatMenuOption> opts = new List<FloatMenuOption>
            {
                new FloatMenuOption("Withdraw silver…", PromptWithdrawSilver),
                new FloatMenuOption("Withdraw items…", PromptWithdrawItemPicker)
            };
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        private void StashAllSilver()
        {
            Caravan caravan = SessionHandler.ChosenCaravan;
            if (caravan == null) return;
            int amount = RimworldManager.GetItemCountInCaravan(caravan, ThingDefOf.Silver.defName);
            if (amount <= 0)
            {
                DLG_Base.PushNewDialog(new DLG_Message("Treasury", new[] { "No silver in caravan." }));
                return;
            }
            PM_Treasury.Deposit(TreasuryClientCache.OwnerKey, amount, null);
        }

        private void DrawItemsList(Rect box)
        {
            Rect inner = box.ContractedBy(4f);
            const float rowH = 28f;

            var items = TreasuryClientCache.Items;
            float viewH = Mathf.Max(inner.height, items.Count * rowH + 4f);
            Rect viewRect = new Rect(0f, 0f, inner.width - 16f, viewH);

            Widgets.BeginScrollView(inner, ref _itemScroll, viewRect);
            float ly = 0f;
            int i = 0;
            foreach (var kv in items)
            {
                Rect row = new Rect(0f, ly, viewRect.width, rowH);
                if (i % 2 == 0) Widgets.DrawAltRect(row);
                Widgets.DrawHighlightIfMouseover(row);

                Rect labelRect = new Rect(4f, ly, viewRect.width - 100f, rowH);
                EconomyDialogUtil.DrawItemIconAndLabel(labelRect, kv.Key, $"<color=grey>x{kv.Value}</color>");

                if (TreasuryClientCache.CanWithdraw)
                {
                    Rect btn = new Rect(viewRect.width - 86f, ly + 3f, 80f, rowH - 6f);
                    if (Widgets.ButtonText(btn, "Withdraw…"))
                        PromptWithdrawItem(kv.Key, kv.Value);
                }

                ly += rowH;
                i++;
            }
            if (items.Count == 0)
            {
                Widgets.Label(new Rect(4f, 4f, viewRect.width, 20f), "<color=grey>No items in vault.</color>");
            }
            Widgets.EndScrollView();
        }

        private void DrawTransactionsList(Rect box)
        {
            Rect inner = box.ContractedBy(4f);
            // KMH 26.5.20.1: 42px two-line rows. Top line is a coloured chip
            // (Deposit/Withdraw/Sale/Buy etc) + actor on the left and
            // amount/item on the right. Bottom line carries the note in
            // muted grey, with WordWrap on so longer notes wrap to fit
            // the pane instead of running off the right edge.
            const float rowH = 42f;
            const float chipW = 64f;     // coloured kind tag
            const float chipPad = 6f;    // gap between chip and actor

            var txs = TreasuryClientCache.RecentTransactions;
            float viewH = Mathf.Max(inner.height, txs.Count * rowH + 4f);
            Rect viewRect = new Rect(0f, 0f, inner.width - 16f, viewH);

            Widgets.BeginScrollView(inner, ref _txScroll, viewRect);
            float ly = 0f;
            // Render newest-first.
            for (int i = txs.Count - 1; i >= 0; i--)
            {
                TreasuryTransaction tx = txs[i];
                Rect row = new Rect(0f, ly, viewRect.width, rowH);
                if (i % 2 == 0) Widgets.DrawAltRect(row);

                string what = tx.Kind.ToString();
                // KMH 2.7: Friendly item label in the transaction list.
                string detail = !string.IsNullOrEmpty(tx.ItemDefName)
                    ? $"{tx.Amount}× {EconomyDialogUtil.ResolveLabel(tx.ItemDefName)}"
                    : $"{tx.Amount} silver";

                // Top line — split into LEFT (chip + actor) and RIGHT (detail)
                // so the detail can right-align and never collide with the
                // actor's name. Detail right-aligned reads better for
                // sequential glances ("how much was this?").
                Color oldColor = GUI.color;
                GUI.color = ChipColorFor(tx.Kind);
                Widgets.Label(new Rect(4f, ly + 2f, chipW, 18f), what);
                GUI.color = oldColor;

                Widgets.Label(
                    new Rect(4f + chipW + chipPad, ly + 2f, viewRect.width - (chipW + chipPad + 8f) - 200f, 18f),
                    LinkedAccountsCache.Format(tx.Username));

                TextAnchor prevAnchor = Text.Anchor;
                Text.Anchor = TextAnchor.UpperRight;
                Widgets.Label(new Rect(viewRect.width - 204f, ly + 2f, 200f, 18f), detail);
                Text.Anchor = prevAnchor;

                // Bottom line — full-width note, wrapping on. WordWrap is
                // toggled because RimWorld's default Label uses WordWrap = true
                // already, but we make it explicit for clarity.
                if (!string.IsNullOrEmpty(tx.Note))
                {
                    GUI.color = new Color(0.7f, 0.7f, 0.7f);
                    Widgets.Label(new Rect(4f, ly + 22f, viewRect.width - 8f, 18f), $"· {tx.Note}");
                    GUI.color = oldColor;
                }

                TooltipHandler.TipRegion(row, $"{what} · {tx.Username} · {detail}" +
                    (string.IsNullOrEmpty(tx.Note) ? string.Empty : $"\n{tx.Note}"));

                ly += rowH;
            }
            if (txs.Count == 0)
                Widgets.Label(new Rect(4f, 4f, viewRect.width, 20f), "<color=grey>No recent activity.</color>");
            Widgets.EndScrollView();
        }

        /// <summary>
        /// KMH 26.5.20.1: Colour-code the kind chip on the left of each
        /// transaction so the player can scan deposits vs withdrawals at a
        /// glance without reading the text. Deposit = green, Withdraw = red,
        /// Marketplace (Sale/Buy) = yellow, everything else = neutral grey.
        /// </summary>
        private static Color ChipColorFor(TreasuryTransaction.TxKind kind)
        {
            string n = kind.ToString();
            if (n.IndexOf("Deposit", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Income", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Sale", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Refund", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return new Color(0.6f, 0.95f, 0.6f);
            if (n.IndexOf("Withdraw", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Spend", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Tax", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Buy", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return new Color(1f, 0.7f, 0.7f);
            return new Color(0.85f, 0.85f, 0.85f);
        }

        // -- prompts --

        private void PromptDepositItems()
        {
            // KMH 26.5.20.1: Smarter caravan resolution. Pre-existing flow
            // hard-required `SessionHandler.ChosenCaravan` to be set —
            // which from the treasury dialog often isn't (you opened the
            // treasury from a settlement gizmo, not from a caravan). So
            // we'd just throw an error and the user had no way forward.
            //
            // New flow:
            //   * If a caravan IS selected → use it directly (one click).
            //   * If not, list all the player's caravans in a FloatMenu so
            //     the user can pick one without leaving the treasury.
            //   * If they have no caravans → friendly error.
            Caravan caravan = SessionHandler.ChosenCaravan;
            if (caravan != null)
            {
                OpenCaravanDepositPicker(caravan);
                return;
            }

            System.Collections.Generic.List<Caravan> playerCaravans = new System.Collections.Generic.List<Caravan>();
            if (Find.WorldObjects?.Caravans != null)
            {
                foreach (Caravan c in Find.WorldObjects.Caravans)
                {
                    if (c?.Faction != null && c.Faction.IsPlayer) playerCaravans.Add(c);
                }
            }

            if (playerCaravans.Count == 0)
            {
                DLG_Base.PushNewDialog(new DLG_Message("Treasury",
                    new[] { "You don't have any caravans on the world map. Form a caravan first, then come back." }));
                return;
            }

            if (playerCaravans.Count == 1)
            {
                // No need to ask — there's only one.
                OpenCaravanDepositPicker(playerCaravans[0]);
                return;
            }

            // Multiple caravans → let the user pick which one.
            var opts = new System.Collections.Generic.List<FloatMenuOption>();
            foreach (Caravan c in playerCaravans)
            {
                Caravan cap = c;
                int itemCount = 0;
                try { itemCount = RimWorld.Planet.CaravanInventoryUtility.AllInventoryItems(c)?.Count ?? 0; } catch { }
                string label = $"{cap.Name}  ({itemCount} stacks)";
                opts.Add(new FloatMenuOption(label, () => OpenCaravanDepositPicker(cap)));
            }
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        private void OpenCaravanDepositPicker(Caravan caravan)
        {
            Find.WindowStack.Add(new DLG_CaravanItemPicker(
                caravan,
                title: $"Deposit from {caravan.Name}",
                confirmLabel: "Deposit",
                askForPrice: false,
                onConfirm: (entry, qty, _) =>
                {
                    System.Collections.Generic.Dictionary<string, int> bundle = new System.Collections.Generic.Dictionary<string, int>
                    {
                        [entry.DefName] = qty
                    };
                    PM_Treasury.Deposit(TreasuryClientCache.OwnerKey, 0, bundle);
                }));
        }

        private void PromptDepositSilver()
        {
            Caravan caravan = SessionHandler.ChosenCaravan;
            int max = caravan != null
                ? RimworldManager.GetItemCountInCaravan(caravan, ThingDefOf.Silver.defName)
                : 0;
            if (max <= 0)
            {
                DLG_Base.PushNewDialog(new DLG_Message("Treasury", new[] { "No silver in caravan to deposit." }));
                return;
            }

            DLG_Base.PushNewDialog(new DLG_Inputs(
                "Deposit Silver",
                new[] { $"Amount (max {max})" },
                new[] { false },
                delegate
                {
                    if (int.TryParse(DLG_Inputs.DialogInputResults[0]?.Trim(), out int amount) && amount > 0)
                    {
                        if (amount > max) amount = max;
                        PM_Treasury.Deposit(TreasuryClientCache.OwnerKey, amount, null);
                    }
                }));
        }

        private void PromptWithdrawItemPicker()
        {
            Find.WindowStack.Add(new DLG_TreasuryItemPicker(
                title: "Withdraw Item from Treasury",
                confirmLabel: "Withdraw",
                askForPrice: false,
                onConfirm: (entry, qty, _) =>
                {
                    System.Collections.Generic.Dictionary<string, int> bundle =
                        new System.Collections.Generic.Dictionary<string, int> { [entry.DefName] = qty };
                    PM_Treasury.Withdraw(TreasuryClientCache.OwnerKey, 0, bundle);
                }));
        }

        private void PromptWithdrawSilver()
        {
            int max = TreasuryClientCache.SilverBalance;
            if (max <= 0)
            {
                DLG_Base.PushNewDialog(new DLG_Message("Treasury", new[] { "Treasury silver balance is zero." }));
                return;
            }

            DLG_Base.PushNewDialog(new DLG_Inputs(
                "Withdraw Silver",
                new[] { $"Amount (max {max})" },
                new[] { false },
                delegate
                {
                    if (int.TryParse(DLG_Inputs.DialogInputResults[0]?.Trim(), out int amount) && amount > 0)
                    {
                        if (amount > max) amount = max;
                        PM_Treasury.Withdraw(TreasuryClientCache.OwnerKey, amount, null);
                    }
                }));
        }

        private void PromptWithdrawItem(string defName, int max)
        {
            // KMH 2.7: Friendly label in the prompt title so the player sees
            // "Withdraw Plasteel" instead of "Withdraw Plasteel" (which was
            // fine for raw items but ugly for `Apparel_FlakVest` etc).
            DLG_Base.PushNewDialog(new DLG_Inputs(
                $"Withdraw {EconomyDialogUtil.ResolveLabel(defName)}",
                new[] { $"Amount (max {max})" },
                new[] { false },
                delegate
                {
                    if (int.TryParse(DLG_Inputs.DialogInputResults[0]?.Trim(), out int amount) && amount > 0)
                    {
                        if (amount > max) amount = max;
                        PM_Treasury.Withdraw(TreasuryClientCache.OwnerKey, 0,
                            new Dictionary<string, int> { [defName] = amount });
                    }
                }));
        }
    }
}
