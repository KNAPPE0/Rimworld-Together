using GameClient.Dialogs.Default;
using GameClient.Managers;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs.Economy
{
    /// <summary>
    /// KMH 2.7: Search-the-whole-catalog item picker for marketplace listings.
    ///
    /// Replaces the older two-step "caravan picker → source picker" flow with
    /// a single dialog that:
    ///   • Searches every tradeable <see cref="ThingDef"/> like the custom-site
    ///     picker does (no more typing raw defNames).
    ///   • Shows live stock counts for each item across THREE sources at once:
    ///       1. Currently selected caravan
    ///       2. Player treasury (cached snapshot)
    ///       3. All home-map storage zones (sum across colonies)
    ///   • Lets the user pick which source to pull from. The qty slider is
    ///     bounded by the selected source.
    ///
    /// "All home maps" lets a player browse what they own server-wide before
    ///  they decide to physically move stock to a caravan or treasury — but
    ///  the actual sale must come from caravan or treasury (the colony source
    ///  is shown for awareness only; selecting it tells the player to move it).
    /// </summary>
    public class DLG_MarketItemPicker : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(960f, 640f);

        public enum SourceMode
        {
            /// <summary>Pull from caravan inventory; default when a caravan is selected.</summary>
            Caravan,
            /// <summary>Pull from the player's treasury (server-side cache).</summary>
            Treasury
        }

        public class StockEntry
        {
            public ThingDef Def;
            public int InCaravan;
            public int InTreasury;
            public int InHomeMaps;
            public int TotalAvailable => InCaravan + InTreasury;
            public string Label => Def?.label?.CapitalizeFirst() ?? Def?.defName ?? "?";
            public string DefName => Def?.defName ?? string.Empty;
            public float MarketValue => Def?.BaseMarketValue ?? 0f;

            /// <summary>
            /// KMH 2.7: Per-variant breakdown of caravan stock so the dialog
            /// can show "Normal steel knife (3)" / "Excellent plasteel knife (1)"
            /// and let the user list a specific variant. Empty for items with
            /// no quality and no stuff (e.g. raw resources).
            /// </summary>
            public List<CaravanVariant> CaravanVariants = new List<CaravanVariant>();
        }

        public class CaravanVariant
        {
            public int QualityIndex;          // 0 = no quality
            public string StuffDefName;       // empty = non-stuffable
            public int Count;
            public string DisplayLabel { get; set; } // pre-rendered for UI
        }

        private readonly Caravan _caravan;
        // KMH 2.7: When no specific caravan is passed (e.g. dialog opened
        // from a settlement gizmo), the picker enumerates every player
        // caravan currently on the world map and pools their inventories
        // for display. The first matching caravan is used as the deduction
        // source on confirm.
        private readonly List<Caravan> _scannedCaravans = new List<Caravan>();
        private readonly bool _askForPrice;
        private readonly string _confirmLabel;
        private readonly Action<StockEntry, int, int, SourceMode> _onConfirm;
        // KMH 2.7: Optional richer callback that includes the chosen variant.
        // If both are set, the variant-aware one fires.
        private readonly Action<StockEntry, CaravanVariant, int, int, SourceMode> _onConfirmWithVariant;

        private string _searchText = "";
        private Vector2 _scroll = Vector2.zero;
        private List<StockEntry> _entries;
        private List<StockEntry> _filteredCache;
        private string _filteredCacheKey;
        private StockEntry _selected;
        private CaravanVariant _selectedVariant;   // KMH 2.7
        private int _qty = 1;
        private int _unitPrice = 1;
        private SourceMode _source = SourceMode.Caravan;

        // KMH: We pre-build a stock map keyed by defName so we don't loop
        // over the entire catalog while typing in the search box.
        private Dictionary<string, (int caravan, int treasury, int home)> _stockMap;

        // KMH 2.7: Per-variant caravan breakdown — keyed by defName, each
        // entry lists every quality+stuff combination present in the caravan.
        private Dictionary<string, List<CaravanVariant>> _caravanVariants;

        public DLG_MarketItemPicker(Caravan caravan, string title, string confirmLabel, bool askForPrice,
            Action<StockEntry, int, int, SourceMode> onConfirm, bool catalogOnly = false)
        {
            _caravan = caravan;
            _askForPrice = askForPrice;
            _confirmLabel = confirmLabel;
            _onConfirm = onConfirm;
            _catalogOnly = catalogOnly;
            Title = title;
            closeOnCancel = true;
            absorbInputAroundWindow = true;

            BuildStockMap();
            BuildEntries();

            // Default to whichever source actually has stock.
            _source = caravan != null ? SourceMode.Caravan : SourceMode.Treasury;
        }

        /// <summary>KMH 2.7: variant-aware overload for marketplace listings.</summary>
        public DLG_MarketItemPicker(Caravan caravan, string title, string confirmLabel, bool askForPrice,
            Action<StockEntry, CaravanVariant, int, int, SourceMode> onConfirm)
        {
            _caravan = caravan;
            _askForPrice = askForPrice;
            _confirmLabel = confirmLabel;
            _onConfirmWithVariant = onConfirm;
            Title = title;
            closeOnCancel = true;
            absorbInputAroundWindow = true;

            BuildStockMap();
            BuildEntries();
            _source = caravan != null ? SourceMode.Caravan : SourceMode.Treasury;
        }

        // -- stock collection (cheap on open, no per-frame scans) --

        private void BuildStockMap()
        {
            _stockMap = new Dictionary<string, (int, int, int)>(StringComparer.OrdinalIgnoreCase);
            _caravanVariants = new Dictionary<string, List<CaravanVariant>>(StringComparer.OrdinalIgnoreCase);

            // KMH 2.7: Build the list of caravans to pool. If a specific
            // caravan was passed in (caller had ChosenCaravan), use just
            // that one; otherwise scan every caravan owned by the player.
            // This makes the dialog usable from settlement / site contexts
            // where ChosenCaravan may not be set, instead of silently
            // showing zeros for everything.
            _scannedCaravans.Clear();
            if (_caravan != null)
                _scannedCaravans.Add(_caravan);
            else
            {
                try
                {
                    if (Find.WorldObjects?.Caravans != null)
                    {
                        foreach (Caravan c in Find.WorldObjects.Caravans)
                        {
                            if (c?.Faction != null && c.Faction.IsPlayer)
                                _scannedCaravans.Add(c);
                        }
                    }
                }
                catch { }
            }

            // Caravan(s) — also build per-variant breakdown.
            foreach (Caravan car in _scannedCaravans)
            {
                if (car == null) continue;
                foreach (Thing t in CaravanInventoryUtility.AllInventoryItems(car))
                {
                    if (t?.def == null) continue;
                    if (t.def.category != ThingCategory.Item) continue;
                    string key = t.def.defName;
                    _stockMap.TryGetValue(key, out var cur);
                    _stockMap[key] = (cur.Item1 + t.stackCount, cur.Item2, cur.Item3);

                    // Variant key: quality + stuff. Skip the variant breakdown
                    // for items that have neither (just bulks like Steel) — we
                    // don't need a "Steel (—)" sub-row.
                    int qualityIdx = 0;
                    if (t.TryGetQuality(out QualityCategory q)) qualityIdx = (int)q;
                    string stuffDef = t.Stuff?.defName ?? string.Empty;

                    if (qualityIdx == 0 && string.IsNullOrEmpty(stuffDef))
                        continue;

                    if (!_caravanVariants.TryGetValue(key, out List<CaravanVariant> variants))
                    {
                        variants = new List<CaravanVariant>();
                        _caravanVariants[key] = variants;
                    }
                    CaravanVariant existing = variants.Find(v =>
                        v.QualityIndex == qualityIdx && v.StuffDefName == stuffDef);
                    if (existing != null) existing.Count += t.stackCount;
                    else
                    {
                        variants.Add(new CaravanVariant
                        {
                            QualityIndex = qualityIdx,
                            StuffDefName = stuffDef,
                            Count = t.stackCount,
                            DisplayLabel = FormatVariant(qualityIdx, stuffDef, t.def)
                        });
                    }
                }
            }

            // Treasury — cached snapshot from server.
            if (TreasuryClientCache.Items != null)
            {
                foreach (var kv in TreasuryClientCache.Items)
                {
                    _stockMap.TryGetValue(kv.Key, out var cur);
                    _stockMap[kv.Key] = (cur.Item1, cur.Item2 + kv.Value, cur.Item3);
                }
            }

            // All home maps — best-effort scan of player storage.
            try
            {
                if (Find.Maps != null)
                {
                    foreach (Map map in Find.Maps)
                    {
                        if (map == null || !map.IsPlayerHome) continue;
                        foreach (Thing t in map.listerThings.AllThings)
                        {
                            if (t?.def == null) continue;
                            if (t.def.category != ThingCategory.Item) continue;
                            if (!t.IsInAnyStorage()) continue;
                            string key = t.def.defName;
                            _stockMap.TryGetValue(key, out var cur);
                            _stockMap[key] = (cur.Item1, cur.Item2, cur.Item3 + t.stackCount);
                        }
                    }
                }
            }
            catch { /* don't crash the dialog if a map scan throws */ }
        }

        private void BuildEntries()
        {
            // The catalog: every tradeable item the loaded mods know about.
            // Same filter the custom-site builder uses, so the two pickers
            // feel consistent.
            _entries = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(d => d != null
                    && d.category == ThingCategory.Item
                    && d.BaseMarketValue > 0f
                    && !d.IsCorpse
                    && d.thingCategories != null
                    && d.thingCategories.Count > 0)
                .OrderBy(d => d.label)
                .Select(d =>
                {
                    _stockMap.TryGetValue(d.defName, out var s);
                    StockEntry e = new StockEntry
                    {
                        Def = d,
                        InCaravan = s.caravan,
                        InTreasury = s.treasury,
                        InHomeMaps = s.home
                    };
                    if (_caravanVariants.TryGetValue(d.defName, out List<CaravanVariant> variants))
                        e.CaravanVariants = variants;
                    return e;
                })
                .ToList();
        }

        private static string FormatVariant(int qualityIdx, string stuffDef, ThingDef itemDef)
        {
            List<string> parts = new List<string>(2);
            if (qualityIdx > 0)
            {
                try { parts.Add(((QualityCategory)qualityIdx).GetLabel().CapitalizeFirst()); }
                catch { }
            }
            if (!string.IsNullOrEmpty(stuffDef))
            {
                ThingDef stuff = DefDatabase<ThingDef>.GetNamedSilentFail(stuffDef);
                parts.Add(stuff != null ? stuff.label.CapitalizeFirst() : stuffDef);
            }
            string label = itemDef?.label?.CapitalizeFirst() ?? "?";
            return parts.Count > 0 ? $"{string.Join(" ", parts)} {label}" : label;
        }

        // -- ui --

        public override void DoWindowContents(Rect rect)
        {
            // KMH 26.5.20.1: Shared title + divider via DialogLayout. Was
            // using `28f` title height — a 4px drift from the 32f used by
            // every other KMH dialog.
            float y = DialogLayout.DrawTitle(rect, Title ?? "Pick item");
            DialogLayout.DrawSectionDivider(rect, ref y);

            // KMH 26.5.20.1: Right-pinned "Only items I own" checkbox via
            // DialogLayout.DrawTightCheckbox. The label width is measured at
            // render time so the ☐ marker sits flush against the text instead
            // of floating ~80 px to the right (previous behaviour with the
            // bare Widgets.CheckboxLabeled stretched across a 200 px rect).
            //
            // We render the checkbox FIRST so we know its measured width, then
            // size the search field to consume the rest of the row.
            const string ownedLabel = "Only items I own";
            float ownedWidth = Mathf.Ceil(Text.CalcSize(ownedLabel).x) + 1f + 4f + 24f + 6f; // label+gap+box+slack
            float ownedX = rect.width - ownedWidth;
            bool showOnlyOwned = _showOnlyOwned;
            DialogLayout.DrawTightCheckbox(ownedX, y + 4f, ownedLabel, ref showOnlyOwned);
            if (showOnlyOwned != _showOnlyOwned) { _showOnlyOwned = showOnlyOwned; _filteredCacheKey = null; }

            // Search field — consumes the rest of the row, ending 12 px before
            // the checkbox so the two don't visually touch.
            Rect searchRect = new Rect(0f, y, Mathf.Max(120f, ownedX - 12f), 28f);
            _searchText = Widgets.TextField(searchRect, _searchText ?? "");
            if (string.IsNullOrWhiteSpace(_searchText))
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.3f);
                Widgets.Label(searchRect.ContractedBy(6f, 4f), "Search items… try \"plasteel\", \"meal\", \"smokeleaf\"");
                GUI.color = old;
            }
            y += 34f;

            // Two-pane: list | details
            float listH = rect.height - y - 60f;
            float listW = rect.width * 0.55f;
            float rightX = listW + 8f;
            float rightW = rect.width - rightX;

            Rect listBox = new Rect(0f, y, listW, listH);
            Widgets.DrawMenuSection(listBox);
            DrawList(listBox);

            DrawDetails(new Rect(rightX, y, rightW, listH));

            // Footer buttons
            float btnY = rect.height - 40f;
            if (Widgets.ButtonText(new Rect(0f, btnY, 120f, 32f), "Cancel"))
                Close();

            int sourceStock = StockForSource(_selected, _source);
            // When the user picks a variant from the right pane, the qty is
            // bounded by that variant's stack size, not the caravan total.
            int effectiveMax = (_selectedVariant != null && _source == SourceMode.Caravan)
                ? _selectedVariant.Count
                : sourceStock;
            // KMH 26.5.20.1: In catalog-only mode (e.g. post quest), the
            // user is naming an item, not transacting one — skip the
            // "do you actually own enough?" gate.
            bool canConfirm = _selected != null
                && _qty > 0 && (_catalogOnly || _qty <= effectiveMax)
                && (!_askForPrice || _unitPrice > 0);
            GUI.color = canConfirm ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            if (Widgets.ButtonText(new Rect(rect.width - 200f, btnY, 200f, 32f), _confirmLabel) && canConfirm)
            {
                try
                {
                    if (_onConfirmWithVariant != null)
                        _onConfirmWithVariant(_selected, _selectedVariant, _qty, _askForPrice ? _unitPrice : 0, _source);
                    else
                        _onConfirm?.Invoke(_selected, _qty, _askForPrice ? _unitPrice : 0, _source);
                }
                catch { }
                Close();
            }
            GUI.color = Color.white;
        }

        private bool _showOnlyOwned = false;

        // KMH 26.5.20.1: When true, the picker is being used to choose
        // an item REFERENCE (e.g. for posting a quest where you're asking
        // someone ELSE to deliver). Skip the "do I own enough?" gate that
        // normally blocks the confirm button.
        private bool _catalogOnly = false;

        private List<StockEntry> GetFilteredEntries()
        {
            string filter = (_searchText ?? string.Empty).Trim().ToLowerInvariant();
            string key = filter + "|" + _showOnlyOwned;
            if (_filteredCache != null && _filteredCacheKey == key) return _filteredCache;
            _filteredCacheKey = key;

            IEnumerable<StockEntry> q = _entries;
            if (_showOnlyOwned)
                q = q.Where(e => e.TotalAvailable > 0 || e.InHomeMaps > 0);
            if (!string.IsNullOrEmpty(filter))
                q = q.Where(e =>
                    e.Label.ToLowerInvariant().Contains(filter) ||
                    e.DefName.ToLowerInvariant().Contains(filter));

            // Owned items float to the top; otherwise alphabetical.
            _filteredCache = q.OrderByDescending(e => e.TotalAvailable > 0 || e.InHomeMaps > 0)
                              .ThenBy(e => e.Label).Take(400).ToList();
            return _filteredCache;
        }

        private void DrawList(Rect box)
        {
            Rect inner = box.ContractedBy(4f);
            const float rowH = 26f;
            List<StockEntry> filtered = GetFilteredEntries();

            float viewH = Mathf.Max(inner.height, filtered.Count * rowH + 8f);
            Rect viewRect = new Rect(0f, 0f, inner.width - 16f, viewH);

            Widgets.BeginScrollView(inner, ref _scroll, viewRect);
            float ly = 0f;
            for (int i = 0; i < filtered.Count; i++)
            {
                StockEntry e = filtered[i];
                Rect row = new Rect(0f, ly, viewRect.width, rowH);
                if (_selected == e) Widgets.DrawHighlight(row);
                else if (i % 2 == 0) Widgets.DrawAltRect(row);
                Widgets.DrawHighlightIfMouseover(row);

                // Label + market value
                string label = $"{e.Label}  <color=grey>(${e.MarketValue:F0})</color>";
                Widgets.Label(new Rect(6f, ly + 2f, viewRect.width - 200f, rowH - 4f), label);

                // Stock badges on the right.
                string stockText = StockBadgesShort(e);
                Widgets.Label(new Rect(viewRect.width - 188f, ly + 2f, 184f, rowH - 4f), stockText);

                if (Widgets.ButtonInvisible(row))
                {
                    _selected = e;
                    int allowed = StockForSource(e, _source);
                    if (allowed <= 0)
                    {
                        // Snap to whichever source has the stock so the slider
                        // is immediately usable.
                        if (e.InCaravan > 0) _source = SourceMode.Caravan;
                        else if (e.InTreasury > 0) _source = SourceMode.Treasury;
                        allowed = StockForSource(e, _source);
                    }
                    if (allowed > 0) { if (_qty > allowed) _qty = allowed; if (_qty < 1) _qty = 1; }
                    else _qty = 1;
                }
                ly += rowH;
            }
            if (filtered.Count == 0)
                Widgets.Label(new Rect(6f, 6f, viewRect.width, 20f), "<color=grey>No matching items.</color>");
            Widgets.EndScrollView();
        }

        private static string StockBadgesShort(StockEntry e)
        {
            string c = e.InCaravan > 0 ? $"<color=#80ff80>C×{e.InCaravan}</color>" : "<color=grey>C—</color>";
            string t = e.InTreasury > 0 ? $"<color=#ffff80>T×{e.InTreasury}</color>" : "<color=grey>T—</color>";
            string h = e.InHomeMaps > 0 ? $"<color=#80c8ff>H×{e.InHomeMaps}</color>" : "<color=grey>H—</color>";
            return $"{c}  {t}  {h}";
        }

        private void DrawDetails(Rect box)
        {
            float y = box.y;

            if (_selected == null)
            {
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                Widgets.Label(new Rect(box.x, y, box.width, 80f),
                    "Pick an item from the list on the left.\n\n" +
                    "Stock badges show what you currently own:\n" +
                    "<color=#80ff80>C</color> = caravan · <color=#ffff80>T</color> = treasury · <color=#80c8ff>H</color> = home colonies");
                GUI.color = Color.white;
                return;
            }

            Widgets.Label(new Rect(box.x, y, box.width, 22f), $"<b>{_selected.Label}</b>");
            y += 24f;

            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            string category = _selected.Def?.FirstThingCategory?.label?.CapitalizeFirst() ?? "Item";
            Widgets.Label(new Rect(box.x, y, box.width, 20f),
                $"{category} · Market value: {_selected.MarketValue:F1}s");
            GUI.color = Color.white;
            y += 22f;
            GUI.color = new Color(0.5f, 0.5f, 0.5f);
            Widgets.Label(new Rect(box.x, y, box.width, 14f), $"<size=10>id: {_selected.DefName}</size>");
            GUI.color = Color.white;
            y += 22f;

            // Stock summary block.
            Widgets.Label(new Rect(box.x, y, box.width, 20f),
                $"In caravan: <b>{_selected.InCaravan}</b>   ·   In treasury: <b>{_selected.InTreasury}</b>");
            y += 20f;
            if (_selected.InHomeMaps > 0)
            {
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                Widgets.Label(new Rect(box.x, y, box.width, 20f),
                    $"<color=#80c8ff>{_selected.InHomeMaps}</color> in home colony storage (move to caravan to sell)");
                GUI.color = Color.white;
                y += 22f;
            }
            else y += 4f;

            // Source selector — caravan label adapts to single vs pooled mode.
            Widgets.Label(new Rect(box.x, y, box.width, 20f), "Source:");
            y += 22f;
            float sw = (box.width - 8f) / 2f;
            string caravanLabel = _scannedCaravans.Count switch
            {
                0 => "No caravan",
                1 => $"From caravan ({_selected.InCaravan})",
                _ => $"From any caravan ({_selected.InCaravan} across {_scannedCaravans.Count})"
            };
            DrawSourceButton(new Rect(box.x, y, sw, 26f), SourceMode.Caravan,
                caravanLabel, _selected.InCaravan > 0);
            DrawSourceButton(new Rect(box.x + sw + 8f, y, sw, 26f), SourceMode.Treasury,
                $"From treasury ({_selected.InTreasury})", _selected.InTreasury > 0);
            y += 30f;

            // KMH 2.7: Variant picker — only meaningful for caravan source
            // (treasury currently doesn't track quality/stuff). If the
            // selected def has multiple variants in the caravan, the user
            // must pick one before confirming so the listing carries the
            // right (quality, stuff) tuple.
            int qtyMax;
            if (_source == SourceMode.Caravan && _selected.CaravanVariants != null && _selected.CaravanVariants.Count > 0)
            {
                Widgets.Label(new Rect(box.x, y, box.width, 20f), "Variant:");
                y += 22f;
                string btnLabel = _selectedVariant != null
                    ? $"{_selectedVariant.DisplayLabel}  ×{_selectedVariant.Count}"
                    : (_selected.CaravanVariants.Count == 1
                        ? $"{_selected.CaravanVariants[0].DisplayLabel}  ×{_selected.CaravanVariants[0].Count}"
                        : "Pick a variant…");
                if (Widgets.ButtonText(new Rect(box.x, y, box.width, 26f), btnLabel))
                {
                    List<FloatMenuOption> opts = new List<FloatMenuOption>();
                    foreach (CaravanVariant v in _selected.CaravanVariants)
                    {
                        CaravanVariant cap = v;
                        opts.Add(new FloatMenuOption($"{cap.DisplayLabel}  ×{cap.Count}",
                            () => { _selectedVariant = cap; if (_qty > cap.Count) _qty = cap.Count; }));
                    }
                    Find.WindowStack.Add(new FloatMenu(opts));
                }
                y += 30f;

                // Auto-select if exactly one variant exists.
                if (_selectedVariant == null && _selected.CaravanVariants.Count == 1)
                    _selectedVariant = _selected.CaravanVariants[0];

                qtyMax = _selectedVariant?.Count ?? Mathf.Max(1, _selected.InCaravan);
            }
            else
            {
                _selectedVariant = null;
                qtyMax = Mathf.Max(1, StockForSource(_selected, _source));
            }

            // Qty slider — bounded by variant if picked, else by source total.
            int max = qtyMax;
            Widgets.Label(new Rect(box.x, y, box.width, 20f), $"Quantity: <b>{_qty}</b> / {max}");
            y += 22f;
            int newQty = (int)Widgets.HorizontalSlider(
                new Rect(box.x, y, box.width, 22f),
                _qty, 1f, max, true, _qty.ToString());
            if (newQty != _qty) _qty = newQty;
            if (_qty < 1) _qty = 1;
            if (_qty > max) _qty = max;
            y += 32f;

            if (_askForPrice)
            {
                Widgets.Label(new Rect(box.x, y, box.width, 20f), "Unit price (silver):");
                y += 22f;
                string raw = Widgets.TextField(new Rect(box.x, y, 140f, 24f), _unitPrice.ToString());
                if (int.TryParse(raw, out int parsed) && parsed > 0) _unitPrice = parsed;

                int suggested = Math.Max(1, (int)Math.Ceiling(_selected.MarketValue));
                if (Widgets.ButtonText(new Rect(box.x + 150f, y, 200f, 24f), $"Use market value ({suggested}s)"))
                    _unitPrice = suggested;
                y += 30f;

                Widgets.Label(new Rect(box.x, y, box.width, 20f),
                    $"<color=yellow>Total asking: {_qty * _unitPrice}s</color>");
            }
        }

        private void DrawSourceButton(Rect r, SourceMode mode, string label, bool enabled)
        {
            bool selected = _source == mode;
            Color old = GUI.color;
            if (!enabled) GUI.color = new Color(1f, 1f, 1f, 0.4f);
            else if (selected) GUI.color = new Color(0.85f, 1f, 0.85f);
            if (Widgets.ButtonText(r, (selected ? "● " : "  ") + label) && enabled)
                _source = mode;
            GUI.color = old;
        }

        private static int StockForSource(StockEntry e, SourceMode mode)
        {
            if (e == null) return 0;
            return mode == SourceMode.Caravan ? e.InCaravan : e.InTreasury;
        }
    }
}
