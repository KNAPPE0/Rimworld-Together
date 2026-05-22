using GameClient.Dialogs.Default;
using GameClient.Files;
using GameClient.Managers;
using GameClient.Misc;
using GameClient.PacketManagers;
using RimWorld;
using RimWorld.Planet;
using Shared.Files.Economy;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs.Economy
{
    /// <summary>
    /// Browse open listings, buy, cancel own listings, post a new listing.
    /// </summary>
    public class DLG_Marketplace : DLG_Base
    {
        // KMH 26.5.20.1: 820 → 1040 wide so the per-listing row can fit
        // "Excellent plasteel knife (Excellent, Plasteel)" plus seller +
        // price + Buy button without colliding. Height bumped 580 → 620
        // for symmetric breathing room with the treasury and leaderboard
        // dialogs.
        public override Vector2 InitialSize => new Vector2(1040f, 620f);

        private Vector2 _scroll = Vector2.zero;
        private string _filter = "";
        private bool _showOnlyMine;
        private bool _showOnlyMyGuild;
        private string _categoryFilter = "All";

        // KMH 2.7: Cache for the filtered/sorted listings view so we don't
        // re-run LINQ Where/OrderBy/ToList on every redraw (60fps). Refreshes
        // when any of the inputs change OR when the snapshot reference flips.
        private List<MarketplaceListing> _visibleCache;
        private object _visibleCacheSource;
        private string _visibleCacheFilter;
        private bool _visibleCacheOnlyMine;
        private bool _visibleCacheOnlyMyGuild;
        private string _visibleCacheCategory;

        private static readonly string[] CommonCategories =
        {
            "All", "Resources", "Manufactured", "Foods", "Drugs",
            "Medicine", "Weapons", "Apparel", "Plants", "BodyParts", "Other"
        };

        public DLG_Marketplace()
        {
            Title = "Marketplace";
            closeOnCancel = true;
            absorbInputAroundWindow = true;

            PM_Marketplace.RequestListings();
            MarketplaceClientCache.OnSnapshotUpdated += MarkRedrawNeeded;
        }

        public override void PostClose()
        {
            base.PostClose();
            MarketplaceClientCache.OnSnapshotUpdated -= MarkRedrawNeeded;
        }

        private void MarkRedrawNeeded() { /* placeholder hook */ }

        public override void DoWindowContents(Rect rect)
        {
            // KMH 26.5.20.1: Shared title + section divider for visual parity
            // with every other KMH dialog.
            float y = DialogLayout.DrawTitle(rect, "Player Marketplace");
            DialogLayout.DrawSectionDivider(rect, ref y);

            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(0f, y, rect.width, 20f),
                $"House pool: {MarketplaceClientCache.HouseSilverPool} silver  |  " +
                $"Lifetime trades: {MarketplaceClientCache.LifetimeTradesCompleted}  |  " +
                $"Lifetime silver traded: {MarketplaceClientCache.LifetimeSilverTraded}");
            GUI.color = Color.white;
            y += 24f;

            // KMH 2.7: Reflowed toolbar — was overflowing on 820 px wide
            // dialogs because filter/category/checkboxes/refresh/sell were
            // packed into one row. Now: filter+category on row 1, filter
            // toggles on row 2 (with Refresh/Sell pinned to the right of
            // both rows so they never collide).
            const float colSearchW = 230f;
            const float colCatW = 130f;
            const float btnW = 96f;
            float toolbarRight = rect.width - 4f;

            // Row 1 — search + category, then Refresh + Sell on the right.
            Rect searchRect = new Rect(0f, y, colSearchW, 28f);
            _filter = Widgets.TextField(searchRect, _filter ?? "");
            if (string.IsNullOrWhiteSpace(_filter))
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.3f);
                Widgets.Label(searchRect.ContractedBy(6f, 4f), "Filter by item name…");
                GUI.color = old;
            }

            if (Widgets.ButtonText(new Rect(colSearchW + 8f, y, colCatW, 28f), $"Cat: {_categoryFilter}"))
            {
                List<FloatMenuOption> opts = new List<FloatMenuOption>();
                foreach (string c in CommonCategories)
                {
                    string cap = c;
                    opts.Add(new FloatMenuOption(cap, () => _categoryFilter = cap));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }

            // Right cluster: Refresh + Sell (Sell pinned to right edge).
            float sellX = toolbarRight - btnW;
            float refreshX = sellX - 4f - btnW;
            if (Widgets.ButtonText(new Rect(refreshX, y, btnW, 28f), "Refresh"))
                PM_Marketplace.RequestListings();
            if (Widgets.ButtonText(new Rect(sellX, y, btnW, 28f), "Sell…"))
                PromptCreateListing();

            y += 32f;

            // KMH 26.5.20.1: Row 2 filter toggles via DialogLayout.DrawTightCheckbox.
            // The ☐ now sits flush against each label instead of floating ~40 px
            // away (previous behaviour was Widgets.CheckboxLabeled pinning the
            // box to the right edge of a 160 px rect).
            float mcbx = 0f;
            mcbx = DialogLayout.DrawTightCheckbox(mcbx, y + 4f, "My listings only", ref _showOnlyMine);
            mcbx = DialogLayout.DrawTightCheckbox(mcbx, y + 4f, "My guild only", ref _showOnlyMyGuild);
            y += 30f;

            // Listings table
            Rect listBox = new Rect(0f, y, rect.width, rect.height - y - 44f);
            Widgets.DrawMenuSection(listBox);
            DrawListings(listBox);

            // Close button (shared layout for visual consistency).
            if (DialogLayout.DrawCloseButton(rect)) Close();
        }

        private void DrawListings(Rect box)
        {
            Rect inner = box.ContractedBy(4f);
            // KMH 2.7: Two-line layout — label+quality on top, count+price+seller
            // on bottom. Stops long item names colliding with the Buy/Cancel
            // button on narrow dialog widths.
            const float rowH = 44f;

            string mine = PersistentSettings.Load().UserSettings.Username ?? string.Empty;
            string filterLower = (_filter ?? "").Trim().ToLower();

            // KMH: Resolve our own guild treasury key so the "My guild only"
            // toggle can match listings whose seller is in the same guild.
            string myGuildKey = TreasuryClientCache.HasSnapshot && TreasuryClientCache.IsGuildOwned
                ? TreasuryClientCache.OwnerKey
                : null;

            // KMH 2.7: Only rebuild the filtered/sorted listing view when an
            // input changes — otherwise reuse the cached one. Previously this
            // ran Where/OrderBy/ToList every frame at 60fps.
            object curSource = MarketplaceClientCache.Listings;
            bool inputsChanged =
                _visibleCache == null
                || !ReferenceEquals(_visibleCacheSource, curSource)
                || _visibleCacheFilter != filterLower
                || _visibleCacheOnlyMine != _showOnlyMine
                || _visibleCacheOnlyMyGuild != _showOnlyMyGuild
                || _visibleCacheCategory != _categoryFilter;

            if (inputsChanged)
            {
                _visibleCache = MarketplaceClientCache.Listings.Where(l =>
                    (!_showOnlyMine || string.Equals(l.SellerUsername, mine, StringComparison.OrdinalIgnoreCase)) &&
                    (!_showOnlyMyGuild || (myGuildKey != null && string.Equals(l.SellerTreasuryKey, myGuildKey, StringComparison.OrdinalIgnoreCase))) &&
                    MatchesCategory(l, _categoryFilter) &&
                    (string.IsNullOrEmpty(filterLower)
                        || l.ItemDefName.ToLower().Contains(filterLower)
                        || l.SellerUsername.ToLower().Contains(filterLower)))
                    .OrderBy(l => l.ItemDefName).ThenBy(l => l.UnitPriceSilver)
                    .ToList();

                _visibleCacheSource = curSource;
                _visibleCacheFilter = filterLower;
                _visibleCacheOnlyMine = _showOnlyMine;
                _visibleCacheOnlyMyGuild = _showOnlyMyGuild;
                _visibleCacheCategory = _categoryFilter;
            }

            List<MarketplaceListing> visible = _visibleCache;

            float viewH = Mathf.Max(inner.height, visible.Count * rowH + 8f);
            Rect viewRect = new Rect(0f, 0f, inner.width - 16f, viewH);

            Widgets.BeginScrollView(inner, ref _scroll, viewRect);
            float ly = 0f;
            int i = 0;
            // KMH 26.5.20.1: Reserved column strip widths. Computed once
            // outside the loop so every row uses the same offsets — no
            // visual drift between rows, no overlap between qty/price/
            // seller/button.
            const float buttonStripW = 110f;
            const float qtyW = 92f;
            const float priceW = 110f;
            const float colPad = 8f;
            float infoTotalW = viewRect.width - buttonStripW - 12f;

            foreach (MarketplaceListing l in visible)
            {
                Rect row = new Rect(0f, ly, viewRect.width, rowH);
                if (i % 2 == 0) Widgets.DrawAltRect(row);
                Widgets.DrawHighlightIfMouseover(row);

                // KMH 26.5.20.1: Two-line row layout with HARD column
                // boundaries. Previously the bottom line was one giant
                // concatenated string ("×3/10  @ 50s  by Player") that
                // overflowed when the seller name was long or the price
                // was 5+ digits — the row visually collided with the Buy
                // button. Each field now gets a fixed-width rect so they
                // can never overlap.
                //
                // Column plan (left → right):
                //   [icon+label]       … flexes — eats remaining width
                //   [qty / orig]       … 92 px, right-aligned
                //   [unit price]       … 110 px, right-aligned, yellow
                //   [seller]           … shown on row 2 only, left-aligned
                //   [Buy / Cancel]     … 110 px strip, right edge
                float labelW = infoTotalW - qtyW - priceW - (colPad * 2f);
                if (labelW < 120f) labelW = 120f;

                // Row 1: item icon + label (top line takes the full label column).
                Rect labelRect = new Rect(6f, ly + 2f, labelW, 22f);
                string label = ResolveLabelWithQuality(l);
                EconomyDialogUtil.DrawItemIconAndLabel(labelRect, l.ItemDefName,
                    trailing: null, overrideLabel: label);

                // Row 1 right side: qty + unit price (right-aligned).
                Rect qtyRect = new Rect(6f + labelW + colPad, ly + 2f, qtyW, 22f);
                Rect priceRect = new Rect(qtyRect.xMax + colPad, ly + 2f, priceW, 22f);

                TextAnchor oldAnchor = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(qtyRect, $"<color=grey>×</color>{l.RemainingQty}/{l.OriginalQty}");
                Widgets.Label(priceRect, $"<color=yellow>{l.UnitPriceSilver}s</color>");
                Text.Anchor = oldAnchor;

                // Row 2: seller (left-aligned under the label column).
                Rect sellerRect = new Rect(6f, ly + 23f, infoTotalW - 12f, 18f);
                string seller = LinkedAccountsCache.Format(l.SellerUsername);
                Color sc = GUI.color;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                Widgets.Label(sellerRect, $"by {seller}");
                GUI.color = sc;

                TooltipHandler.TipRegion(row,
                    $"Remaining {l.RemainingQty} of {l.OriginalQty} • {l.UnitPriceSilver}s each • Seller: {l.SellerUsername}");

                bool isMine = string.Equals(l.SellerUsername, mine, StringComparison.OrdinalIgnoreCase);
                Rect btnRect = new Rect(viewRect.width - 104f, ly + (rowH - 30f) * 0.5f, 100f, 30f);
                if (isMine)
                {
                    if (Widgets.ButtonText(btnRect, "Cancel"))
                        PromptCancel(l);
                }
                else
                {
                    if (Widgets.ButtonText(btnRect, "Buy…"))
                        PromptBuy(l);
                }

                ly += rowH;
                i++;
            }
            if (visible.Count == 0)
                Widgets.Label(new Rect(6f, 6f, viewRect.width, 20f), "<color=grey>No listings match.</color>");

            Widgets.EndScrollView();
        }

        private static bool MatchesCategory(MarketplaceListing l, string category)
        {
            if (string.IsNullOrEmpty(category) || category == "All") return true;
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(l.ItemDefName);
            if (def?.thingCategories == null) return category == "Other";

            foreach (var tc in def.thingCategories)
            {
                if (tc?.defName == null) continue;
                if (tc.defName.IndexOf(category, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return category == "Other";
        }

        // -- prompts --

        private void PromptBuy(MarketplaceListing l)
        {
            int max = l.RemainingQty;
            int afford = SessionHandler.ChosenCaravan != null
                ? RimworldManager.GetSilverInCaravan(SessionHandler.ChosenCaravan) / Math.Max(1, l.UnitPriceSilver)
                : int.MaxValue;
            int allowed = Math.Min(max, afford);
            if (allowed <= 0)
            {
                DLG_Base.PushNewDialog(new DLG_Message("Marketplace", new[] { "Not enough silver in caravan." }));
                return;
            }

            string buyTitle = $"Buy {ResolveLabel(l.ItemDefName)}";
            DLG_Base.PushNewDialog(new DLG_Inputs(
                buyTitle,
                new[] { $"Quantity (max {allowed}, unit {l.UnitPriceSilver}s)" },
                new[] { false },
                delegate
                {
                    if (int.TryParse(DLG_Inputs.DialogInputResults[0]?.Trim(), out int qty) && qty > 0)
                    {
                        if (qty > allowed) qty = allowed;
                        // No caravan? Have it delivered to treasury.
                        bool toTreasury = SessionHandler.ChosenCaravan == null;
                        PM_Marketplace.Buy(l.Id, qty, toTreasury);
                    }
                }));
        }

        private void PromptCancel(MarketplaceListing l)
        {
            DLG_Base.PushNewDialog(new DLG_YesNo(
                $"Cancel listing of {ResolveLabel(l.ItemDefName)} ×{l.RemainingQty}? Stock returns to your treasury.",
                delegate { PM_Marketplace.CancelListing(l.Id); }));
        }

        // KMH 2.7: defName → display label, RimWorld DefDatabase first.
        private static string ResolveLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return string.Empty;
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def != null && !string.IsNullOrEmpty(def.label))
                return def.label.CapitalizeFirst();
            return defName;
        }

        /// <summary>
        /// KMH 2.7: Renders "Steel knife (Excellent, Plasteel)" style labels
        /// when the listing has quality/stuff metadata, else falls back to
        /// the plain item label.
        /// </summary>
        private static string ResolveLabelWithQuality(MarketplaceListing l)
        {
            string baseLabel = ResolveLabel(l.ItemDefName);
            List<string> qualifiers = new List<string>();
            if (l.QualityIndex > 0)
            {
                try
                {
                    QualityCategory q = (QualityCategory)l.QualityIndex;
                    qualifiers.Add(q.GetLabel().CapitalizeFirst());
                }
                catch { }
            }
            if (!string.IsNullOrEmpty(l.StuffDefName))
            {
                ThingDef stuff = DefDatabase<ThingDef>.GetNamedSilentFail(l.StuffDefName);
                if (stuff != null) qualifiers.Add(stuff.label.CapitalizeFirst());
                else qualifiers.Add(l.StuffDefName);
            }
            if (qualifiers.Count == 0) return baseLabel;
            return $"{baseLabel} <color=grey>({string.Join(", ", qualifiers)})</color>";
        }

        private void PromptCreateListing()
        {
            // KMH 2.7: Single combined catalog picker with variant support.
            // Caravan-sourced listings carry quality + stuff metadata so a
            // "Masterwork plasteel knife" stays distinct from "Normal steel
            // knife" on the board.
            Caravan caravan = SessionHandler.ChosenCaravan;
            Find.WindowStack.Add(new DLG_MarketItemPicker(
                caravan,
                title: "List item on marketplace",
                confirmLabel: "Post listing",
                askForPrice: true,
                onConfirm: (entry, variant, qty, unitPrice, source) =>
                {
                    bool fromTreasury = source == DLG_MarketItemPicker.SourceMode.Treasury;
                    int qualityIdx = variant?.QualityIndex ?? 0;
                    string stuff = variant?.StuffDefName ?? string.Empty;
                    PM_Marketplace.CreateListing(entry.DefName, qty, unitPrice, fromTreasury, qualityIdx, stuff);
                }));
        }

        private void PromptSourcePick(string defName, int qty, int unitPrice)
        {
            Caravan caravan = SessionHandler.ChosenCaravan;
            int caravanStock = caravan != null ? RimworldManager.GetItemCountInCaravan(caravan, defName) : 0;
            int treasuryStock = TreasuryClientCache.HasSnapshot && TreasuryClientCache.Items.TryGetValue(defName, out int s) ? s : 0;

            List<FloatMenuOption> opts = new List<FloatMenuOption>();

            string itemLabel = ResolveLabel(defName);

            string caravanLabel = $"From caravan ({caravanStock} available)";
            opts.Add(new FloatMenuOption(caravanLabel, () =>
            {
                if (caravanStock < qty)
                {
                    DLG_Base.PushNewDialog(new DLG_Message("Marketplace", new[] { $"Caravan only has {caravanStock} {itemLabel}, need {qty}." }));
                    return;
                }
                PM_Marketplace.CreateListing(defName, qty, unitPrice, fromTreasury: false);
            }));

            string treasuryLabel = $"From treasury ({treasuryStock} available)";
            opts.Add(new FloatMenuOption(treasuryLabel, () =>
            {
                if (treasuryStock < qty)
                {
                    DLG_Base.PushNewDialog(new DLG_Message("Marketplace", new[] { $"Treasury only has {treasuryStock} {itemLabel}, need {qty}." }));
                    return;
                }
                PM_Marketplace.CreateListing(defName, qty, unitPrice, fromTreasury: true);
            }));

            Find.WindowStack.Add(new FloatMenu(opts));
        }
    }
}
