using GameClient.Dialogs.Default;
using GameClient.Dialogs.Economy;
using GameClient.Misc;
using GameClient.Managers;
using RimWorld;
using Shared.Files.Economy;
using Shared.Files.Sites;
using System;
using System.Collections.Generic;
using System.Linq;
using TCPNetwork;
using TCPNetwork.Packets;
using UnityEngine;
using Verse;
using Shared;
using GameClient.PacketManagers;

namespace GameClient.Dialogs.Sites
{
    public class DLG_CustomSiteBuild : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(820f, 680f);

        // Reserved space for footer buttons. Right-pane scroll uses this so
        // its content never overlaps the Build/Cancel buttons.
        private const float ScrollFooterReserve = 56f;

        // Internal scroll for the right config pane.
        private Vector2 _rightScroll = Vector2.zero;

        private string _searchText = "";
        private Vector2 _itemScroll = Vector2.zero;
        private ThingDef _selectedItem = null;
        private int _amount = 10;
        private SiteAccessMode _accessMode = SiteAccessMode.GuildOnly;
        private int _taxPercent = 10;
        private int _targetTile = -1;

        // KMH: Owner's chosen reward routing for this site.
        private RewardDestination _rewardDestination = RewardDestination.Caravan;
        private int _marketplaceUnitPrice = 1;

        private List<ThingDef> _filteredItems = null;
        private string _lastSearch = null;

        private const float Pad = 10f;
        private const float RowH = 26f;

        public DLG_CustomSiteBuild(int tile)
        {
            _targetTile = tile;
            Title = "Build Custom Site";
            closeOnCancel = true;
            absorbInputAroundWindow = true;
        }

        private List<ThingDef> GetFilteredItems()
        {
            if (_filteredItems != null && _lastSearch == _searchText)
                return _filteredItems;

            _lastSearch = _searchText;
            string search = (_searchText ?? "").Trim().ToLower();

            _filteredItems = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(d => d.category == ThingCategory.Item
                    && d.BaseMarketValue > 0
                    && !d.IsCorpse
                    && d.thingCategories != null
                    && d.thingCategories.Count > 0)
                .Where(d => string.IsNullOrEmpty(search)
                    || d.label.ToLower().Contains(search)
                    || d.defName.ToLower().Contains(search))
                .OrderBy(d => d.label)
                .Take(200)
                .ToList();

            return _filteredItems;
        }

        private int CalculateCost() => CustomSiteBuildCalc.CalculateCost(_selectedItem, _amount);

        private int CalculateCycleMinutes() => CustomSiteBuildCalc.CalculateCycleMinutes(_selectedItem);

        public override void DoWindowContents(Rect rect)
        {
            // Shared title via DialogLayout, then the
            // dialog-specific KMH version badge on top of it.
            DialogLayout.DrawTitle(rect, "Build Custom Production Site");
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.5f, 0.5f, 0.5f);
            Widgets.Label(new Rect(rect.width - 120f, 4f, 120f, 16f), "KMH Custom Sites");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            Widgets.DrawLineHorizontal(0f, 34f, rect.width);

            float y = 40f;
            float leftW = rect.width * 0.45f;
            float outerRightX = leftW + Pad;
            float outerRightW = rect.width - outerRightX;

            // === LEFT: Item search + list ===
            Widgets.Label(new Rect(0f, y, leftW, 20f), "<b>Select Item to Produce</b>");
            y += 22f;

            // Search bar
            Rect searchRect = new Rect(0f, y, leftW, 28f);
            _searchText = Widgets.TextField(searchRect, _searchText ?? "");
            if (string.IsNullOrWhiteSpace(_searchText))
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.3f);
                Widgets.Label(searchRect.ContractedBy(6f, 4f), "Search items...");
                GUI.color = old;
            }
            y += 32f;

            // Item list
            List<ThingDef> items = GetFilteredItems();
            float listH = rect.height - y - 50f;
            Rect listOuter = new Rect(0f, y, leftW, listH);
            Widgets.DrawMenuSection(listOuter);

            float viewH = Math.Max(listH, items.Count * RowH + 6f);
            Rect viewRect = new Rect(0f, 0f, leftW - 20f, viewH);

            Widgets.BeginScrollView(listOuter.ContractedBy(2f), ref _itemScroll, viewRect);
            float ly = 0f;
            for (int i = 0; i < items.Count; i++)
            {
                ThingDef def = items[i];
                Rect row = new Rect(0f, ly, viewRect.width, RowH);

                if (_selectedItem == def)
                    Widgets.DrawHighlight(row);

                if (i % 2 == 0)
                    Widgets.DrawAltRect(row);

                Widgets.DrawHighlightIfMouseover(row);

                string label = $"{def.label.CapitalizeFirst()}  (${def.BaseMarketValue:F0})";
                Widgets.Label(new Rect(6f, ly + 2f, viewRect.width - 12f, RowH - 4f), label);

                if (Widgets.ButtonInvisible(row))
                    _selectedItem = def;

                ly += RowH;
            }
            Widgets.EndScrollView();

            // === RIGHT: Configuration (scrollable) ===
            // Reserve space at the bottom for footer buttons. Right pane content
            // scrolls so it can never overlap the buttons regardless of how
            // tall the cost preview / reward destination sections grow.
            float rightPaneTop = 40f;
            float rightPaneHeight = rect.height - rightPaneTop - ScrollFooterReserve;
            Rect rightPaneRect = new Rect(outerRightX, rightPaneTop, outerRightW, rightPaneHeight);

            float estimatedContentHeight = _selectedItem != null ? 720f : 80f;
            Rect rightView = new Rect(0f, 0f, outerRightW - 18f, Mathf.Max(rightPaneHeight, estimatedContentHeight));
            Widgets.BeginScrollView(rightPaneRect, ref _rightScroll, rightView);

            // Inside the scroll view: shadow rightX/rightW so the existing draw
            // code keeps working at view-local coordinates.
            float rightX = 0f;
            float rightW = rightView.width;

            float ry = 0f;
            Widgets.Label(new Rect(rightX, ry, rightW, 20f), "<b>Site Configuration</b>");
            ry += 24f;

            // Selected item display
            if (_selectedItem != null)
            {
                Widgets.Label(new Rect(rightX, ry, rightW, 20f),
                    $"Item: <color=yellow>{_selectedItem.label.CapitalizeFirst()}</color>");
                ry += 22f;
                Widgets.Label(new Rect(rightX, ry, rightW, 20f),
                    $"Market Value: <color=white>${_selectedItem.BaseMarketValue:F1}</color> per unit");
                ry += 26f;

                // Amount slider
                Widgets.Label(new Rect(rightX, ry, 140f, 20f), $"Amount per cycle:");
                Widgets.Label(new Rect(rightX + 140f, ry, 50f, 20f), $"<b>{_amount}</b>");
                ry += 22f;
                _amount = (int)Widgets.HorizontalSlider(
                    new Rect(rightX, ry, rightW, 20f),
                    _amount, 1f, 50f, true, $"{_amount}x");
                if (_amount < 1) _amount = 1;
                ry += 28f;

                // Access mode
                Widgets.Label(new Rect(rightX, ry, 100f, 20f), "Access:");
                if (Widgets.ButtonText(new Rect(rightX + 100f, ry, rightW - 100f, 26f), $"{_accessMode}"))
                {
                    List<FloatMenuOption> opts = new List<FloatMenuOption>
                    {
                        new FloatMenuOption("Private (owner only)", () => _accessMode = SiteAccessMode.Private),
                        new FloatMenuOption("Guild Only", () => _accessMode = SiteAccessMode.GuildOnly),
                        new FloatMenuOption("Public (anyone, with tax)", () => _accessMode = SiteAccessMode.Public),
                    };
                    Find.WindowStack.Add(new FloatMenu(opts));
                }
                ry += 30f;

                // Tax (only for public)
                if (_accessMode == SiteAccessMode.Public)
                {
                    Widgets.Label(new Rect(rightX, ry, 140f, 20f), $"Owner tax:");
                    Widgets.Label(new Rect(rightX + 140f, ry, 50f, 20f), $"<b>{_taxPercent}%</b>");
                    ry += 22f;
                    _taxPercent = (int)Widgets.HorizontalSlider(
                        new Rect(rightX, ry, rightW, 20f),
                        _taxPercent, 0f, 50f, true, $"{_taxPercent}%");
                    ry += 28f;
                }

                // === Cost Preview ===
                Widgets.DrawLineHorizontal(rightX, ry, rightW);
                ry += 6f;

                int cost = CalculateCost();
                int cycleMin = CalculateCycleMinutes();

                Widgets.Label(new Rect(rightX, ry, rightW, 20f), "<b>Cost Preview</b>");
                ry += 22f;

                GUI.color = new Color(1f, 0.85f, 0.4f);
                Widgets.Label(new Rect(rightX, ry, rightW, 20f), $"Build Cost: {cost} silver");
                ry += 22f;
                Widgets.Label(new Rect(rightX, ry, rightW, 20f), $"Cycle Time: {cycleMin} minutes (solo)");
                ry += 22f;
                Widgets.Label(new Rect(rightX, ry, rightW, 20f),
                    $"Production: {_amount}x {_selectedItem.label} per cycle");
                ry += 22f;

                // Value per hour estimate
                double cyclesPerHour = 60.0 / cycleMin;
                double valuePerHour = _selectedItem.BaseMarketValue * _amount * cyclesPerHour;
                Widgets.Label(new Rect(rightX, ry, rightW, 20f),
                    $"Est. Value: ~${valuePerHour:F0}/hour");
                ry += 22f;

                // ROI
                double hoursToROI = cost / valuePerHour;
                Widgets.Label(new Rect(rightX, ry, rightW, 20f),
                    $"Break-even: ~{hoursToROI:F1} hours");
                GUI.color = Color.white;
                ry += 28f;

                // Relevant skill info
                string relevantSkill = Shared.Files.Sites.CustomSiteData.DetermineRelevantSkill(_selectedItem.defName);
                int bestSkill = CustomSiteBuildCalc.GetBestColonistSkill(relevantSkill);

                GUI.color = new Color(0.6f, 0.9f, 1f);
                Widgets.Label(new Rect(rightX, ry, rightW, 20f),
                    $"Relevant skill: {relevantSkill} (your best: {bestSkill})");
                ry += 22f;

                // Skill efficiency preview
                double skillEff = 0.6 + (bestSkill / 20.0) * 1.0;
                Widgets.Label(new Rect(rightX, ry, rightW, 20f),
                    $"Skill efficiency: {skillEff:P0}");
                GUI.color = Color.white;
                ry += 26f;

                // Multi-worker info
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                Widgets.Label(new Rect(rightX, ry, rightW, 48f),
                    $"Workers with high {relevantSkill} skill produce more.\n" +
                    "Skill 0-5: 60-80% efficiency | Skill 16-20: 130-160%\n" +
                    "Workers gain XP each cycle they're present.");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                ry += 52f;

                // KMH: Reward destination picker.
                Widgets.DrawLineHorizontal(rightX, ry, rightW); ry += 6f;
                Widgets.Label(new Rect(rightX, ry, rightW, 20f), "<b>Reward Destination</b>");
                ry += 22f;

                if (Widgets.ButtonText(new Rect(rightX, ry, rightW, 26f), $"{_rewardDestination}"))
                {
                    DLG_Base.PushNewDialog(new DLG_RewardDestination(
                        _rewardDestination,
                        sel => _rewardDestination = sel));
                }
                ry += 30f;

                // Marketplace unit price field — only when relevant.
                if (_rewardDestination == RewardDestination.Marketplace)
                {
                    Widgets.Label(new Rect(rightX, ry, 160f, 20f), "Unit price:");
                    string raw = Widgets.TextField(new Rect(rightX + 160f, ry, 100f, 22f), _marketplaceUnitPrice.ToString());
                    if (int.TryParse(raw, out int newPrice) && newPrice > 0) _marketplaceUnitPrice = newPrice;
                    ry += 28f;
                }
            }
            else
            {
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                Widgets.Label(new Rect(rightX, ry, rightW, 40f), "Select an item from the list to see pricing and configure your custom production site.");
                GUI.color = Color.white;
            }

            Widgets.EndScrollView();

            // === Bottom Buttons (always visible — sit below the scroll view) ===
            float btnY = rect.height - 40f;
            float btnW = 180f;

            if (_selectedItem != null)
            {
                if (Widgets.ButtonText(new Rect(rect.width / 2f - btnW - 5f, btnY, btnW, 34f), $"Build ({CalculateCost()} silver)"))
                {
                    SendCustomBuildRequest();
                    Close();
                }
            }

            if (Widgets.ButtonText(new Rect(rect.width / 2f + 5f, btnY, btnW, 34f), "Cancel"))
                Close();
        }

        private void SendCustomBuildRequest()
        {
            if (_selectedItem == null || _targetTile < 0) return;

            int cost = CalculateCost();

            // Keep client-side precheck only so the player gets immediate feedback.
            // DO NOT deduct silver here anymore. We only deduct after the server confirms success.
            if (SessionHandler.ChosenCaravan != null)
            {
                if (!RimworldManager.CheckIfHasEnoughItemInCaravan(SessionHandler.ChosenCaravan, ThingDefOf.Silver.defName, cost))
                {
                    DLG_Base.PushNewDialog(new DLG_Message("Error", new string[] { $"Not enough silver! Need {cost} silver." }));
                    return;
                }
            }

            PM_Sites.BeginPendingCustomSiteBuild(_targetTile, cost, SessionHandler.ChosenCaravan);

            PKT_Site packet = new PKT_Site();
            packet._stepMode = PKT_Site.SiteStepMode.CustomBuild;
            packet._customRequest = new CustomSiteRequest
            {
                ItemDefName = _selectedItem.defName,
                AmountPerCycle = _amount,
                MarketValuePerUnit = _selectedItem.BaseMarketValue,
                AccessMode = _accessMode,
                OwnerTaxPercent = _taxPercent,
                Tile = _targetTile,
                OwnerRewardDestination = _rewardDestination,
                MarketplaceUnitPrice = _marketplaceUnitPrice
            };

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.SiteManager, packet);

            string relevantSkill = Shared.Files.Sites.CustomSiteData.DetermineRelevantSkill(_selectedItem.defName);
            int bestSkill = CustomSiteBuildCalc.GetBestColonistSkill(relevantSkill);

            DLG_Base.PushNewDialog(new DLG_Message("Custom Site",
                new string[] {
                    $"Submitting custom site build request...\n\n" +
                    $"Item: {_selectedItem.label} x{_amount} per cycle\n" +
                    $"Cost: {cost} silver | Cycle: {CalculateCycleMinutes()} min\n" +
                    $"Skill: {relevantSkill} (your best: {bestSkill})\n" +
                    $"Access: {_accessMode}\n\n" +
                    $"Silver will only be removed if the server approves the build."
                }));
        }
    }
}