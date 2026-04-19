using GameClient.Dialogs.Default;
using GameClient.Misc;
using GameClient.Managers;
using RimWorld;
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
        public override Vector2 InitialSize => new Vector2(700f, 600f);

        private string _searchText = "";
        private Vector2 _itemScroll = Vector2.zero;
        private ThingDef _selectedItem = null;
        private int _amount = 10;
        private SiteAccessMode _accessMode = SiteAccessMode.GuildOnly;
        private int _taxPercent = 10;
        private int _targetTile = -1;

        private List<ThingDef> _filteredItems = null;
        private string _lastSearch = null;

        private const float Pad = 10f;
        private const float RowH = 26f;

        // Pricing constants (must match server)
        private const double PriceMultiplier = 3.0;
        private const int MinCost = 500;

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

        private int CalculateCost()
        {
            if (_selectedItem == null) return 0;
            int cost = (int)Math.Ceiling(_selectedItem.BaseMarketValue * _amount * PriceMultiplier);
            return Math.Max(MinCost, cost);
        }

        private int CalculateCycleMinutes()
        {
            if (_selectedItem == null) return 30;
            double min = 30.0 + (_selectedItem.BaseMarketValue / 5.0);
            return (int)Math.Min(240, Math.Max(30, min));
        }

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width, 32f), "Build Custom Production Site");
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.5f, 0.5f, 0.5f);
            Widgets.Label(new Rect(rect.width - 120f, 4f, 120f, 16f), "KMH Custom Sites");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            Widgets.DrawLineHorizontal(0f, 34f, rect.width);

            float y = 40f;
            float leftW = rect.width * 0.45f;
            float rightX = leftW + Pad;
            float rightW = rect.width - rightX;

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

            // === RIGHT: Configuration ===
            float ry = 40f;
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
                int bestSkill = GetBestColonistSkill(relevantSkill);

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
                    "2 workers = 1.35x speed, 5 workers = 1.8x speed.");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }
            else
            {
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                Widgets.Label(new Rect(rightX, ry, rightW, 40f), "Select an item from the list to see pricing and configure your custom production site.");
                GUI.color = Color.white;
            }

            // === Bottom Buttons ===
            float btnY = rect.height - 38f;
            float btnW = 160f;

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

        /// <summary>Find the best skill level among all player colonists for a given skill.</summary>
        private int GetBestColonistSkill(string skillDefName)
        {
            try
            {
                if (Find.CurrentMap == null) return 0;

                SkillDef skillDef = DefDatabase<SkillDef>.AllDefsListForReading
                    .FirstOrDefault(s => s.defName == skillDefName);
                if (skillDef == null) return 0;

                int best = 0;
                foreach (Pawn pawn in Find.CurrentMap.mapPawns.FreeColonists)
                {
                    SkillRecord skill = pawn.skills?.GetSkill(skillDef);
                    if (skill != null && skill.Level > best)
                        best = skill.Level;
                }
                return best;
            }
            catch { return 0; }
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
                Tile = _targetTile
            };

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.SiteManager, packet);

            string relevantSkill = Shared.Files.Sites.CustomSiteData.DetermineRelevantSkill(_selectedItem.defName);
            int bestSkill = GetBestColonistSkill(relevantSkill);

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