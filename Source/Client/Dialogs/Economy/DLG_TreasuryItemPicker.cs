using GameClient.Dialogs.Default;
using GameClient.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs.Economy
{
    /// <summary>
    /// KMH: Searchable picker for items currently in the player's
    /// (cached) treasury. Companion to <see cref="DLG_CaravanItemPicker"/>
    /// for treasury-source flows: withdraw, list-from-treasury, etc.
    /// </summary>
    public class DLG_TreasuryItemPicker : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(720f, 540f);

        public class PickerEntry
        {
            public string DefName;
            public int CountInTreasury;
            public ThingDef Def => DefDatabase<ThingDef>.GetNamedSilentFail(DefName);
            public string Label => Def?.label ?? DefName;
        }

        private readonly string _title;
        private readonly string _confirmLabel;
        private readonly bool _askForPrice;
        private readonly Action<PickerEntry, int, int> _onConfirm;

        private string _searchText = "";
        private Vector2 _scroll = Vector2.zero;
        private List<PickerEntry> _entries;
        private PickerEntry _selected;
        private int _qty = 1;
        private int _unitPrice = 1;

        // KMH 26.5.20.1: Filter cache so DrawList doesn't run a fresh
        // Where().ToList() every frame. Invalidates on search-text change
        // or when _entries is rebuilt.
        private List<PickerEntry> _filteredCache;
        private string _filteredCacheText;
        private List<PickerEntry> _filteredCacheSource;

        public DLG_TreasuryItemPicker(string title, string confirmLabel, bool askForPrice,
            Action<PickerEntry, int, int> onConfirm)
        {
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
            if (TreasuryClientCache.Items == null) return;
            foreach (var kv in TreasuryClientCache.Items.OrderBy(p => p.Key))
            {
                if (kv.Value <= 0) continue;
                _entries.Add(new PickerEntry { DefName = kv.Key, CountInTreasury = kv.Value });
            }
        }

        public override void DoWindowContents(Rect rect)
        {
            // KMH 26.5.20.1: Shared title + divider via DialogLayout.
            float y = DialogLayout.DrawTitle(rect, _title);
            DialogLayout.DrawSectionDivider(rect, ref y);

            Rect searchRect = new Rect(0f, y, rect.width, 28f);
            _searchText = Widgets.TextField(searchRect, _searchText ?? "");
            if (string.IsNullOrWhiteSpace(_searchText))
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.3f);
                Widgets.Label(searchRect.ContractedBy(6f, 4f), "Search treasury items by name…");
                GUI.color = old;
            }
            y += 34f;

            float listH = rect.height - y - 130f;
            float listW = rect.width * 0.55f;
            float rightX = listW + 8f;
            float rightW = rect.width - rightX;

            Rect listBox = new Rect(0f, y, listW, listH);
            Widgets.DrawMenuSection(listBox);
            DrawList(listBox);
            DrawDetails(new Rect(rightX, y, rightW, listH));

            float btnY = rect.height - 40f;
            if (Widgets.ButtonText(new Rect(0f, btnY, 120f, 32f), "Cancel"))
                Close();

            bool canConfirm = _selected != null && _qty > 0 && _qty <= _selected.CountInTreasury && (!_askForPrice || _unitPrice > 0);
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

            // KMH 26.5.20.1: Filter cache — only rebuild when the search
            // text or the underlying entries list changes. Previously did
            // a Where().ToList() every frame.
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
                Widgets.Label(new Rect(viewRect.width - 90f, ly + 2f, 86f, rowH - 4f), $"<color=grey>×{e.CountInTreasury}</color>");

                if (Widgets.ButtonInvisible(row))
                {
                    _selected = e;
                    if (_qty > e.CountInTreasury) _qty = e.CountInTreasury;
                    if (_qty < 1) _qty = 1;
                }
                ly += rowH;
            }
            if (filtered.Count == 0)
                Widgets.Label(new Rect(6f, 6f, viewRect.width, 20f), "<color=grey>No matching items in treasury.</color>");
            Widgets.EndScrollView();
        }

        private void DrawDetails(Rect box)
        {
            float y = box.y;
            if (_selected == null)
            {
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                Widgets.Label(new Rect(box.x, y, box.width, 40f), "Select an item from the treasury on the left.");
                GUI.color = Color.white;
                return;
            }

            Widgets.Label(new Rect(box.x, y, box.width, 22f), $"<b>{_selected.Label.CapitalizeFirst()}</b>");
            y += 24f;

            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            string category = _selected.Def?.FirstThingCategory?.label?.CapitalizeFirst() ?? "Item";
            float mv = _selected.Def?.BaseMarketValue ?? 0f;
            Widgets.Label(new Rect(box.x, y, box.width, 20f),
                mv > 0f ? $"{category} · Market value: {mv:F1}s" : category);
            GUI.color = Color.white;
            y += 22f;
            GUI.color = new Color(0.5f, 0.5f, 0.5f);
            Widgets.Label(new Rect(box.x, y, box.width, 14f), $"<size=10>id: {_selected.DefName}</size>");
            GUI.color = Color.white;
            y += 18f;

            Widgets.Label(new Rect(box.x, y, box.width, 20f), $"Quantity: <b>{_qty}</b> / {_selected.CountInTreasury}");
            y += 22f;
            int newQty = (int)Widgets.HorizontalSlider(
                new Rect(box.x, y, box.width, 22f),
                _qty, 1f, _selected.CountInTreasury, true, _qty.ToString());
            if (newQty != _qty) _qty = newQty;
            y += 30f;

            if (_askForPrice)
            {
                Widgets.Label(new Rect(box.x, y, box.width, 20f), "Unit price (silver):");
                y += 22f;
                string raw = Widgets.TextField(new Rect(box.x, y, 140f, 24f), _unitPrice.ToString());
                if (int.TryParse(raw, out int parsed) && parsed > 0) _unitPrice = parsed;
                y += 30f;
                Widgets.Label(new Rect(box.x, y, box.width, 20f), $"<color=yellow>Total: {_qty * _unitPrice}s</color>");
            }
        }
    }
}
