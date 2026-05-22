using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace GameClient.Dialogs
{
    /// <summary>
    /// KMH 26.5.20.1: Shared layout primitives for the KMH dialog family.
    ///
    /// Every dialog under <c>Dialogs/Economy</c> (and several under
    /// <c>Dialogs/Sites</c>) was hand-coding the same magic numbers for the
    /// title row, section divider, close button, live indicator, and
    /// per-tick refresh cadence. With ~13 dialogs sharing the visual
    /// language, tiny one-pixel drifts crept in (some closed at
    /// <c>rect.width - 100f</c>, some at <c>rect.width - 104f</c>, some at
    /// <c>96f</c> wide, some at <c>100f</c>) — small drift that adds up to
    /// "everything looks slightly different from everything else."
    ///
    /// This class centralises the values + offers a few "draw this common
    /// thing here" helpers so dialogs read declaratively:
    ///
    /// <code>
    /// DialogLayout.DrawTitle(rect, "Player Leaderboard");
    /// float y = DialogLayout.AfterTitle;
    /// DialogLayout.DrawSectionDivider(rect, ref y);
    /// // ...
    /// if (DialogLayout.DrawCloseButton(rect)) Close();
    /// </code>
    ///
    /// All constants are kept here as <c>const</c> so the JIT inlines them
    /// — there's no runtime cost vs. typing the literal each time.
    /// </summary>
    internal static class DialogLayout
    {
        // -- Title row --

        /// <summary>Height of the medium-font title label at the top of every dialog.</summary>
        public const float TitleHeight = 32f;

        /// <summary>Y-cursor position immediately after the title (matches the
        /// historical <c>y = 36f</c> seed used in every dialog).</summary>
        public const float AfterTitle = 36f;

        // -- Section divider --

        /// <summary>Vertical padding consumed by a horizontal section divider
        /// (matches the historical <c>y += 8f</c> after DrawLineHorizontal).</summary>
        public const float DividerHeight = 8f;

        // -- Toolbar --

        /// <summary>Standard toolbar control height (text fields, buttons, dropdowns).</summary>
        public const float ToolbarHeight = 28f;

        /// <summary>Vertical step from one toolbar row to the next, including spacing.</summary>
        public const float ToolbarRowStep = 32f;

        // -- List rows --

        /// <summary>Default one-line list row height (sortable tables, member lists).</summary>
        public const float RowHeightSingle = 24f;

        /// <summary>Two-line list row height (used by the marketplace listings).</summary>
        public const float RowHeightDouble = 44f;

        // -- Close / footer --

        /// <summary>Width of the close button at the bottom-right of every dialog.</summary>
        public const float CloseBtnWidth = 96f;

        /// <summary>Height of the close button.</summary>
        public const float CloseBtnHeight = 32f;

        /// <summary>Vertical footer reserve (close-button area). Subtract this
        /// from <c>rect.height</c> when sizing scrollable list boxes.</summary>
        public const float FooterReserve = 44f;

        // -- Live indicator --

        /// <summary>Width of the "● live · Ns ago" badge that pins to the top-right.</summary>
        public const float LiveBadgeWidth = 220f;

        /// <summary>Y offset for the live badge (sits inside the title row).</summary>
        public const float LiveBadgeY = 6f;

        /// <summary>Height of the live badge text.</summary>
        public const float LiveBadgeHeight = 18f;

        /// <summary>RGB for the live indicator (light green).</summary>
        public static readonly Color LiveBadgeColor = new Color(0.6f, 0.85f, 0.6f);

        // -- Auto-refresh cadence --

        /// <summary>How often dialogs that poll the server should re-request.
        /// 8 seconds is the failover heartbeat; live broadcasts from the
        /// server already push between polls, so this is just a safety net.</summary>
        public const float AutoRefreshSeconds = 8f;

        // -- Standard padding --

        /// <summary>Inner padding for scrollable list boxes.</summary>
        public const float ListInnerPad = 4f;

        /// <summary>Width reserved for the vertical scrollbar inside a scroll view.</summary>
        public const float ScrollbarReserveWidth = 16f;

        // -- Helpers --

        /// <summary>
        /// Draw the medium-font dialog title at the top, then reset the font
        /// back to Small for the rest of the dialog content. Returns the
        /// next Y cursor so the caller can chain.
        /// </summary>
        public static float DrawTitle(Rect rect, string title)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width, TitleHeight), title);
            Text.Font = GameFont.Small;
            return AfterTitle;
        }

        /// <summary>
        /// Draw the horizontal divider at the current <paramref name="y"/>
        /// and advance it past the divider's vertical padding.
        /// </summary>
        public static void DrawSectionDivider(Rect rect, ref float y)
        {
            Widgets.DrawLineHorizontal(0f, y, rect.width);
            y += DividerHeight;
        }

        /// <summary>
        /// Draw the "● live · Ns ago" indicator pinned to the top-right of
        /// the title row. Pass the seconds-since-last-refresh in the dialog.
        /// </summary>
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

        /// <summary>
        /// Draw the right-pinned close button at the bottom of the dialog.
        /// Returns true if the user clicked it (caller should then call
        /// <c>Close()</c>).
        /// </summary>
        public static bool DrawCloseButton(Rect rect)
        {
            return Widgets.ButtonText(
                new Rect(rect.width - CloseBtnWidth - 4f,
                         rect.height - FooterReserve + 4f,
                         CloseBtnWidth,
                         CloseBtnHeight),
                "Close");
        }

        /// <summary>
        /// Standard scroll-view-friendly inner content rect: full width
        /// minus the scrollbar reserve.
        /// </summary>
        public static Rect ScrollContentRect(Rect outer, float contentHeight)
        {
            return new Rect(0f, 0f, outer.width - ScrollbarReserveWidth, contentHeight);
        }

        /// <summary>
        /// KMH 26.5.20.1: Draw a center-aligned label. Saves callers from
        /// the four-line GameFont/Anchor save/restore dance every time they
        /// want to center a single label. Restores TextAnchor.UpperLeft
        /// when done so subsequent labels render normally.
        /// </summary>
        public static void DrawCenteredLabel(Rect rect, string text)
        {
            TextAnchor old = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, text);
            Text.Anchor = old;
        }

        /// <summary>
        /// KMH 26.5.20.1: A right-aligned label (good for numeric cells in
        /// tables — numbers read better right-aligned).
        /// </summary>
        public static void DrawRightLabel(Rect rect, string text)
        {
            TextAnchor old = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(rect, text);
            Text.Anchor = old;
        }

        /// <summary>
        /// KMH 26.5.20.1: A compact, label-first checkbox.
        ///
        /// <para><b>Why a custom widget?</b> <c>Widgets.CheckboxLabeled</c>
        /// ALWAYS pins the ☐/☑ box to the right edge of the supplied rect,
        /// regardless of how short the label is. Passing a rect sized to
        /// the label still leaves a gap because RimWorld inserts its own
        /// padding before the box. The earlier <c>Text.CalcSize</c> attempt
        /// improved things but the rendered result still looked like:</para>
        /// <code>Only items I own              ☐</code>
        /// <para>This version renders the label and the box directly,
        /// adjacent, with a fixed 4-pixel gap between them. The result is:</para>
        /// <code>Only items I own ☐</code>
        ///
        /// <para>The whole rect is clickable (toggles the value), and a
        /// mouseover highlight is drawn so it still feels like a button.</para>
        ///
        /// Returns the X position one slot past the right edge so callers
        /// can chain multiple checkboxes left-to-right.
        /// </summary>
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

            // Hover affordance — full row is the click target so a near-miss
            // click still hits.
            if (Mouse.IsOver(rowRect))
                Widgets.DrawHighlight(rowRect);

            TextAnchor oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, label);
            Text.Anchor = oldAnchor;

            // Use the bare checkbox primitive (no internal label-side padding).
            Widgets.Checkbox(boxRect.x, boxRect.y, ref value, BoxSize);

            // Make the whole row clickable so users hitting the label still
            // toggle the value.
            if (Widgets.ButtonInvisible(rowRect, doMouseoverSound: false))
            {
                value = !value;
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
            }

            return x + totalWidth + GapBetweenCheckboxes;
        }

        /// <summary>
        /// KMH 26.5.20.1: Convert a CamelCase enum name into a human-readable
        /// label by inserting spaces at every lower-to-upper boundary.
        ///   "LifetimeSilverIn" → "Lifetime Silver In"
        ///   "WorkerXp"         → "Worker Xp"
        ///   "EconomyScore"     → "Economy Score"
        /// Apply to <c>SortMode</c> / <c>View</c> enum values before showing
        /// them to the player — never expose raw CamelCase identifiers.
        /// </summary>
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

        /// <summary>
        /// KMH 26.5.20.1: A numeric text field where the empty-string
        /// state means "no value". Default RimWorld pattern echoes the
        /// current int as a string into the field, which means to change
        /// 1 → 5 you'd have to type "51" then delete the "1" — frustrating.
        ///
        /// Pass a <c>ref string</c> buffer that the caller persists across
        /// frames (alongside the int that's the parsed value). On a valid
        /// parse the int is updated; an empty buffer leaves the int alone
        /// so the dialog can finish with whatever default it wants.
        /// </summary>
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
