using GameClient.Dialogs.Default;
using Shared.Files.Economy;
using System;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs.Economy
{
    /// <summary>
    /// Picker for a <see cref="RewardDestination"/>. Used at site build time
    /// and from the per-site "Change Destination" gizmo.
    ///
    /// Sized for clear descriptions + selected-option summary so players
    /// understand the consequences of each choice.
    /// </summary>
    public class DLG_RewardDestination : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(560f, 480f);

        private readonly RewardDestination _initial;
        private RewardDestination _selected;
        private readonly Action<RewardDestination> _onConfirm;

        public DLG_RewardDestination(RewardDestination initial, Action<RewardDestination> onConfirm)
        {
            _initial = initial;
            _selected = initial;
            _onConfirm = onConfirm;

            Title = "Reward Destination";
            closeOnCancel = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width, 30f), "Where do rewards go?");
            Text.Font = GameFont.Small;

            float y = 36f;
            Widgets.DrawLineHorizontal(0f, y, rect.width); y += 8f;

            DrawOption(rect, ref y, RewardDestination.Caravan,
                "Caravan",
                "Rewards are delivered directly to your active caravan or home map. " +
                "This is the standard behaviour and the right choice when you're playing actively.");

            DrawOption(rect, ref y, RewardDestination.Treasury,
                "Treasury",
                "Rewards are deposited into your guild treasury (if you're in a guild) " +
                "or your personal vault. Use this when you want pooled storage that members " +
                "can withdraw from later.");

            DrawOption(rect, ref y, RewardDestination.Marketplace,
                "Marketplace",
                "Rewards are automatically listed for sale at the site's configured unit price. " +
                "Sold items pay silver into your treasury (minus marketplace tax).");

            // Selected-option summary so confirmation feels clear.
            y += 6f;
            Widgets.DrawLineHorizontal(0f, y, rect.width); y += 6f;
            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            Widgets.Label(new Rect(0f, y, rect.width, 22f),
                $"Selected: <b>{_selected}</b>");
            GUI.color = Color.white;

            // Footer buttons (always at the bottom — no overlap).
            float btnY = rect.height - 40f;
            if (Widgets.ButtonText(new Rect(0f, btnY, 140f, 32f), "Cancel"))
                Close();
            if (Widgets.ButtonText(new Rect(rect.width - 140f, btnY, 140f, 32f), "Confirm"))
            {
                try { _onConfirm?.Invoke(_selected); }
                catch { }
                Close();
            }
        }

        private void DrawOption(Rect rect, ref float y, RewardDestination opt, string name, string desc)
        {
            // Row height adapts to description (so long desc no longer overflows).
            float descHeight = Text.CalcHeight(desc, rect.width - 40f);
            float rowHeight = Mathf.Max(50f, 26f + descHeight);

            Rect row = new Rect(0f, y, rect.width, rowHeight);
            Widgets.DrawHighlightIfMouseover(row);
            if (_selected == opt) Widgets.DrawHighlight(row);

            Widgets.RadioButton(new Vector2(0f, y + 4f), _selected == opt);
            Widgets.Label(new Rect(34f, y, rect.width - 34f, 22f), $"<b>{name}</b>");
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(34f, y + 22f, rect.width - 40f, descHeight + 2f), desc);
            GUI.color = Color.white;

            if (Widgets.ButtonInvisible(row)) _selected = opt;
            y += rowHeight + 6f;
        }
    }
}
