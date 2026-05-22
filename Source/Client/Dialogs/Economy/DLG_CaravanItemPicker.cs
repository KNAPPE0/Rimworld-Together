using GameClient.Dialogs.Default;
using GameClient.Misc;
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
    /// KMH: Reusable searchable caravan-inventory picker.
    ///
    /// Replaces typing raw <c>defName</c> in marketplace/treasury flows.
    /// User picks an item from their caravan, then types qty (and price if
    /// the caller is a marketplace listing).
    /// </summary>
    public class DLG_CaravanItemPicker : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(720f, 540f);

        public class PickerEntry
        {
            public ThingDef Def;
            public int CountInCaravan;
            public string Label => Def?.label ?? Def?.defName ?? "?";
            public string DefName => Def?.defName ?? string.Empty;
            public float MarketValue => Def?.BaseMarketValue ?? 0f;
        }

        private readonly Caravan _caravan;
        private readonly string _title;
        private readonly string _confirmLabel;

        /// <summary>
        /// Callback once the user has confirmed an item + qty (and optionally price).
        /// If <paramref name="askForPrice"/> was false, <c>unitPrice</c> will be 0.
        /// </summary>
        private readonly Action<PickerEntry, int, int> _onConfirm;
        private readonly bool _askForPrice;

        private string _searchText = "";
        private Vector2 _scroll = Vector2.zero;
        private List<PickerEntry> _entries;
        private PickerEntry _selected;

        // Filter cache so DrawList doesn't rerun the LINQ
        // chain every frame. Invalidates on search-text change or when the
        // underlying _entries list is rebuilt.
        private List<PickerEntry> _filteredCache;
        private string _filteredCacheText;
        private List<PickerEntry> _filteredCacheSource;
        private int _qty = 1;
        private int _unitPrice = 1;

        public DLG_CaravanItemPicker(Caravan caravan, string title, string confirmLabel, bool askForPrice,
            Action<PickerEntry, int, int> onConfirm)
        {
            _caravan = caravan;
            _title = title;
            _confirmLabel = confirmLabel;
            _askForPrice = askForPrice;
            _onConfirm = onConfirm;

            Title = title;
            closeOnCancel = true;
            absorbInputAroundWindow = true;

            BuildEntries();
        }

        private void BuildEntries()
        {
            _entries = new List<PickerEntry>();
            if (_caravan == null) return;

            try
            {
                Dictionary<ThingDef, int> totals = new Dictionary<ThingDef, int>();
                foreach (Thing t in CaravanInventoryUtility.AllInventoryItems(_caravan))
                {
                    if (t?.def == null) continue;
                    if (t.def.category != ThingCategory.Item) continue;
                    if (!totals.TryGetValue(t.def, out int cur)) cur = 0;
                    totals[t.def] = cur + t.stackCount;
                }

                foreach (var kv in totals.OrderBy(p => p.Key.label))
                    _entries.Add(new PickerEntry { Def = kv.Key, CountInCaravan = kv.Value });
            }
            catch { }
        }

        public override void DoWindowContents(Rect rect)
        {
            // Shared title + divider via DialogLayout.
            float y = DialogLayout.DrawTitle(rect, _title);
            DialogLayout.DrawSectionDivider(rect, ref y);

            // Search
            Rect searchRect = new Rect(0f, y, rect.width, 28f);
            _searchText = Widgets.TextField(searchRect, _searchText ?? "");
            if (string.IsNullOrWhiteSpace(_searchText))
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.3f);
                Widgets.Label(searchRect.ContractedBy(6f, 4f), "Search caravan items by name…");
                GUI.color = old;
            }
            y += 34f;

            // Two-column: list | details
            float listH = rect.height - y - 130f;
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

            bool canConfirm = _selected != null && _qty > 0 && _qty <= _selected.CountInCaravan && (!_askForPrice || _unitPrice > 0);
            GUI.color = canConfirm ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            if (Widgets.ButtonText(new Rect(rect.width - 200f, btnY, 200f, 32f), _confirmLabel) && canConfirm)
            {
                try { _onConfirm?.Invoke(_selected, _qty, _askForPrice ? _unitPrice : 0); }
                catch { }
                Close();
            }
            GUI.color = Color.white;
        }

        private void DrawList(Rect box)
        {
            Rect inner = box.ContractedBy(4f);
            const float rowH = 26f;
            string filter = (_searchText ?? string.Empty).Trim().ToLowerInvariant();

            // Filter cache — rebuild only on input change.
            if (_filteredCache == null
                || _filteredCacheText != filter
                || !ReferenceEquals(_filteredCacheSource, _entries))
            {
                _filteredCache = _entries.Where(e =>
                    string.IsNullOrEmpty(filter)
                    || e.Label.ToLowerInvariant().Contains(filter)
                    || e.DefName.ToLowerInvariant().Contains(filter)).ToList();
                _filteredCacheText = filter;
                _filteredCacheSource = _entries;
            }
            List<PickerEntry> filtered = _filteredCache;

            float viewH = Mathf.Max(inner.height, filtered.Count * rowH + 8f);
            Rect viewRect = new Rect(0f, 0f, inner.width - 16f, viewH);

            Widgets.BeginScrollView(inner, ref _scroll, viewRect);
            float ly = 0f;
            for (int i = 0; i < filtered.Count; i++)
            {
                PickerEntry e = filtered[i];
                Rect row = new Rect(0f, ly, viewRect.width, rowH);
                if (_selected == e) Widgets.DrawHighlight(row);
                else if (i % 2 == 0) Widgets.DrawAltRect(row);
                Widgets.DrawHighlightIfMouseover(row);

                Widgets.Label(new Rect(6f, ly + 2f, viewRect.width - 100f, rowH - 4f), e.Label.CapitalizeFirst());
                Widgets.Label(new Rect(viewRect.width - 90f, ly + 2f, 86f, rowH - 4f), $"<color=grey>×{e.CountInCaravan}</color>");

                if (Widgets.ButtonInvisible(row))
                {
                    _selected = e;
                    if (_qty > e.CountInCaravan) _qty = e.CountInCaravan;
                    if (_qty < 1) _qty = 1;
                }
                ly += rowH;
            }
            if (filtered.Count == 0)
                Widgets.Label(new Rect(6f, 6f, viewRect.width, 20f), "<color=grey>No matching items in caravan.</color>");
            Widgets.EndScrollView();
        }

        private void DrawDetails(Rect box)
        {
            float y = box.y;
            if (_selected == null)
            {
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                Widgets.Label(new Rect(box.x, y, box.width, 40f),
                    "Select an item from your caravan on the left.");
                GUI.color = Color.white;
                return;
            }

            // Selected info — header with label, then a humane subtitle (no
            // raw defName front-and-centre).
            Widgets.Label(new Rect(box.x, y, box.width, 22f), $"<b>{_selected.Label.CapitalizeFirst()}</b>");
            y += 24f;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            string category = _selected.Def?.FirstThingCategory?.label?.CapitalizeFirst() ?? "Item";
            Widgets.Label(new Rect(box.x, y, box.width, 20f),
                $"{category} · Market value: {_selected.MarketValue:F1}s");
            GUI.color = Color.white;
            y += 22f;
            // Tooltip-only defName so power users can still see it for /sell etc.
            Rect defBoxRect = new Rect(box.x, y, box.width, 14f);
            GUI.color = new Color(0.5f, 0.5f, 0.5f);
            Widgets.Label(defBoxRect, $"<size=10>id: {_selected.DefName}</size>");
            GUI.color = Color.white;
            y += 18f;

            // Qty slider
            Widgets.Label(new Rect(box.x, y, box.width, 20f),
                $"Quantity: <b>{_qty}</b> / {_selected.CountInCaravan}");
            y += 22f;
            int newQty = (int)Widgets.HorizontalSlider(
                new Rect(box.x, y, box.width, 22f),
                _qty, 1f, _selected.CountInCaravan, true, _qty.ToString());
            if (newQty != _qty) _qty = newQty;
            y += 30f;

            // Optional price
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
                    $"<color=yellow>Total: {_qty * _unitPrice}s</color>");
            }
        }
    }
}
