using GameClient.Managers;
using Shared.Files.Economy;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs.Economy
{
    /// <summary>
    /// Paged transaction-log viewer for the currently-cached treasury.
    /// Filterable by transaction kind and member name.
    /// </summary>
    public class DLG_TreasuryLogs : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(820f, 560f);

        private Vector2 _scroll = Vector2.zero;
        private string _userFilter = "";
        private TreasuryTransaction.TxKind? _kindFilter = null;

        // Cache the filtered + ordered transaction list so we
        // don't re-run Where/OrderBy/ToList per frame. Treasury logs can
        // legitimately have 100+ rows; sorting them every frame at 60fps
        // is needless allocation churn. Invalidates when filter / kind /
        // underlying snapshot reference changes.
        private List<TreasuryTransaction> _orderedCache;
        private object _orderedCacheSource;
        private string _orderedCacheFilter;
        private TreasuryTransaction.TxKind? _orderedCacheKind;

        public DLG_TreasuryLogs()
        {
            Title = "Treasury Logs";
            closeOnCancel = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect rect)
        {
            // Shared layout primitives so this dialog looks
            // identical to every other KMH dialog.
            string title = TreasuryClientCache.IsGuildOwned
                ? $"Logs — {TreasuryClientCache.OwnerKey}"
                : "Logs — Personal Vault";
            float y = DialogLayout.DrawTitle(rect, title);
            DialogLayout.DrawSectionDivider(rect, ref y);

            // Filter row
            Rect userRect = new Rect(0f, y, 240f, 28f);
            _userFilter = Widgets.TextField(userRect, _userFilter ?? "");
            if (string.IsNullOrWhiteSpace(_userFilter))
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.3f);
                Widgets.Label(userRect.ContractedBy(6f, 4f), "Filter by user…");
                GUI.color = old;
            }

            string kindLabel = _kindFilter == null ? "All kinds" : _kindFilter.Value.ToString();
            if (Widgets.ButtonText(new Rect(248f, y, 200f, 28f), $"Kind: {kindLabel}"))
            {
                List<FloatMenuOption> opts = new List<FloatMenuOption>
                {
                    new FloatMenuOption("All kinds", () => _kindFilter = null)
                };
                foreach (TreasuryTransaction.TxKind k in Enum.GetValues(typeof(TreasuryTransaction.TxKind)))
                {
                    var captured = k;
                    opts.Add(new FloatMenuOption(captured.ToString(), () => _kindFilter = captured));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            y += 34f;

            // Stats line
            int totalCount = TreasuryClientCache.RecentTransactions?.Count ?? 0;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(0f, y, rect.width, 20f),
                $"Showing latest {totalCount} entries · Lifetime in: {TreasuryClientCache.LifetimeSilverIn}s · out: {TreasuryClientCache.LifetimeSilverOut}s");
            GUI.color = Color.white;
            y += 24f;

            // Log table
            Rect listBox = new Rect(0f, y, rect.width, rect.height - y - 44f);
            Widgets.DrawMenuSection(listBox);
            DrawLog(listBox);

            if (DialogLayout.DrawCloseButton(rect)) Close();
        }

        private void DrawLog(Rect box)
        {
            Rect inner = box.ContractedBy(4f);
            // KMH: Bumped from 24 → 30 so timestamps + long notes don't crowd
            // each other and the alternating row colour reads cleanly.
            const float rowH = 30f;

            string filter = (_userFilter ?? "").Trim();
            object curSource = TreasuryClientCache.RecentTransactions;

            // Filter/sort cache — only rebuild when inputs
            // change. Saves the per-frame LINQ on a list that can hit 100+
            // entries in active guilds.
            bool inputsChanged =
                _orderedCache == null
                || !ReferenceEquals(_orderedCacheSource, curSource)
                || _orderedCacheFilter != filter
                || _orderedCacheKind != _kindFilter;

            if (inputsChanged)
            {
                IEnumerable<TreasuryTransaction> rows = TreasuryClientCache.RecentTransactions
                    ?? new List<TreasuryTransaction>();
                if (_kindFilter != null) rows = rows.Where(t => t.Kind == _kindFilter.Value);
                if (!string.IsNullOrEmpty(filter))
                    rows = rows.Where(t => (t.Username ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
                _orderedCache = rows.OrderByDescending(t => t.UtcTicks).ToList();
                _orderedCacheSource = curSource;
                _orderedCacheFilter = filter;
                _orderedCacheKind = _kindFilter;
            }

            List<TreasuryTransaction> ordered = _orderedCache;

            float viewH = Mathf.Max(inner.height, ordered.Count * rowH + 8f);
            Rect viewRect = new Rect(0f, 0f, inner.width - 16f, viewH);

            Widgets.BeginScrollView(inner, ref _scroll, viewRect);
            float ly = 0f;
            for (int i = 0; i < ordered.Count; i++)
            {
                TreasuryTransaction tx = ordered[i];
                Rect row = new Rect(0f, ly, viewRect.width, rowH);
                if (i % 2 == 0) Widgets.DrawAltRect(row);

                string when = new DateTime(tx.UtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("MM-dd HH:mm");
                // Show the friendly item label, not the raw defName.
                string what = string.IsNullOrEmpty(tx.ItemDefName)
                    ? $"<color=yellow>{tx.Amount}s</color>"
                    : $"<color=#bcd>{tx.Amount}× {EconomyDialogUtil.ResolveLabel(tx.ItemDefName)}</color>";

                string kindColor;
                switch (tx.Kind)
                {
                    case TreasuryTransaction.TxKind.Deposit: kindColor = "#80ff80"; break;
                    case TreasuryTransaction.TxKind.Withdraw: kindColor = "#ff8080"; break;
                    case TreasuryTransaction.TxKind.MarketplaceSale: kindColor = "#ffce4d"; break;
                    case TreasuryTransaction.TxKind.MarketplaceTax: kindColor = "#ffaa50"; break;
                    case TreasuryTransaction.TxKind.MarketplaceRefund: kindColor = "#aaffff"; break;
                    case TreasuryTransaction.TxKind.SiteRewardSilver:
                    case TreasuryTransaction.TxKind.SiteRewardItem: kindColor = "#80ddff"; break;
                    default: kindColor = "#cccccc"; break;
                }

                Widgets.Label(new Rect(6f, ly + 2f, viewRect.width - 12f, rowH - 4f),
                    $"<color=grey>{when}</color>  <color={kindColor}>{tx.Kind}</color>  {GameClient.Managers.LinkedAccountsCache.Format(tx.Username)}  {what}  <color=grey>{tx.Note}</color>");

                ly += rowH;
            }
            if (ordered.Count == 0)
                Widgets.Label(new Rect(6f, 6f, viewRect.width, 20f), "<color=grey>No matching transactions.</color>");
            Widgets.EndScrollView();
        }
    }
}
