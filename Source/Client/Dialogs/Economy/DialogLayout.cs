using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace GameClient.Dialogs
{
    // Shared layout constants + helpers for the KMH dialog family.
    // Centralises the magic numbers so dialogs stay pixel-consistent.
    internal static class DialogLayout
    {
        // -- Title row --
        public const float TitleHeight = 32f;
        public const float AfterTitle = 36f;

        // -- Section divider --
        public const float DividerHeight = 8f;

        // -- Toolbar --
        public const float ToolbarHeight = 28f;
        public const float ToolbarRowStep = 32f;

        // -- List rows --
        public const float RowHeightSingle = 24f;
        public const float RowHeightDouble = 44f;

        // -- Close / footer --
        public const float CloseBtnWidth = 96f;
        public const float CloseBtnHeight = 32f;
        public const float FooterReserve = 44f;

        // -- Live indicator --
        public const float LiveBadgeWidth = 220f;
        public const float LiveBadgeY = 6f;
        public const float LiveBadgeHeight = 18f;
        public static readonly Color LiveBadgeColor = new Color(0.6f, 0.85f, 0.6f);

        // -- Auto-refresh cadence --
        // 8s is the failover heartbeat — server broadcasts already push between polls.
        public const float AutoRefreshSeconds = 8f;

        // -- Standard padding --
        public const float ListInnerPad = 4f;
        public const float ScrollbarReserveWidth = 16f;

        // -- Helpers --

        public static float DrawTitle(Rect rect, string title)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width, TitleHeight), title);
            Text.Font = GameFont.Small;
            return AfterTitle;
        }

        public static void DrawSectionDivider(Rect rect, ref float y)
        {
            Widgets.DrawLineHorizontal(0f, y, rect.width);
            y += DividerHeight;
        }

        public static void DrawLiveBadge(Rect rect, int secondsSinceRefresh)
        {
            Text.Font = GameFont.Tiny;
            Color old = GUI.color;
            GUI.color = LiveBadgeColor;
            string text = secondsSinceRefresh <= 1 ? "● live · just now" : $"● live · {secondsSinceRefresh}s ago";
            Widgets.Label(new Rect(rect.width - LiveBadgeWidth, LiveBadgeY, LiveBadgeWidth, LiveBadgeHeight), text);
            GUI.color = old;
            Text.Font = GameFont.Small;
        }

        public static bool DrawCloseButton(Rect rect)
        {
            return Widgets.ButtonText(
                new Rect(rect.width - CloseBtnWidth - 4f,
                         rect.height - FooterReserve + 4f,
                         CloseBtnWidth,
                         CloseBtnHeight),
                "Close");
        }

        public static Rect ScrollContentRect(Rect outer, float contentHeight)
        {
            return new Rect(0f, 0f, outer.width - ScrollbarReserveWidth, contentHeight);
        }

        public static void DrawCenteredLabel(Rect rect, string text)
        {
            TextAnchor old = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, text);
            Text.Anchor = old;
        }

        public static void DrawRightLabel(Rect rect, string text)
        {
            TextAnchor old = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(rect, text);
            Text.Anchor = old;
        }

        // Widgets.CheckboxLabeled pins the box to the right edge regardless of label length.
        // This version renders label and box adjacent with a fixed 4px gap, whole row clickable.
        public static float DrawTightCheckbox(float x, float y, string label, ref bool value)
        {
            const float BoxSize = 24f;
            const float GapLabelBox = 4f;
            const float GapBetweenCheckboxes = 14f;
            const float RowHeight = 24f;

            float labelWidth = Mathf.Ceil(Text.CalcSize(label).x) + 1f;
            float totalWidth = labelWidth + GapLabelBox + BoxSize;

            Rect rowRect = new Rect(x, y, totalWidth, RowHeight);
            Rect labelRect = new Rect(x, y + ((RowHeight - 22f) / 2f), labelWidth, 22f);
            Rect boxRect = new Rect(x + labelWidth + GapLabelBox,
                                    y + ((RowHeight - BoxSize) / 2f),
                                    BoxSize, BoxSize);

            if (Mouse.IsOver(rowRect))
                Widgets.DrawHighlight(rowRect);

            TextAnchor oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, label);
            Text.Anchor = oldAnchor;

            Widgets.Checkbox(boxRect.x, boxRect.y, ref value, BoxSize);

            if (Widgets.ButtonInvisible(rowRect, doMouseoverSound: false))
            {
                value = !value;
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
            }

            return x + totalWidth + GapBetweenCheckboxes;
        }

        // "LifetimeSilverIn" → "Lifetime Silver In". Inserts spaces at lower→upper boundaries.
        public static string FriendlyEnumName(System.Enum value)
        {
            if (value == null) return string.Empty;
            string s = value.ToString();
            if (string.IsNullOrEmpty(s)) return string.Empty;
            System.Text.StringBuilder sb = new System.Text.StringBuilder(s.Length + 4);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (i > 0 && char.IsUpper(c) && (char.IsLower(s[i - 1]) || (i + 1 < s.Length && char.IsLower(s[i + 1]))))
                    sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }

        // Numeric field where empty = "no value". Avoids the default echo-back UX
        // where editing 1→5 forces typing "51" then deleting "1".
        public static void DrawNumericField(Rect rect, ref string buffer, ref int parsedValue, int min = 0, int max = int.MaxValue)
        {
            buffer = Widgets.TextField(rect, buffer ?? string.Empty);
            if (string.IsNullOrEmpty(buffer)) return;
            if (int.TryParse(buffer, out int v))
            {
                if (v < min) v = min;
                if (v > max) v = max;
                parsedValue = v;
            }
        }
    }
}
