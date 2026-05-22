using System;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs.Default
{
    public class DLG_Buttons : DLG_Base
    {
        private string[] Labels { get; set; }
        private Action[] Actions { get; set; }

        public DLG_Buttons(string title, string description, string[] labels, Action[] actions, Action onCancel = null)
        {
            Title = title;
            Description = description;

            Labels = labels ?? Array.Empty<string>();
            Actions = actions ?? Array.Empty<Action>();
            OnCancel = onCancel;

            closeOnAccept = false;
            closeOnCancel = false;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            CalculateWindowSize();
        }

        // Layout constants — single source of truth so the
        // size calculation in CalculateWindowSize and the renderer here never
        // drift apart. Earlier the size calc assumed 38px per button but the
        // renderer added 6px spacing on top, so a 6-button stack overflowed
        // the window and pushed "Leave" under the Cancel footer.
        private const float ButtonHeight = 38f;
        private const float ButtonSpacing = 8f;
        private const float ButtonStride = ButtonHeight + ButtonSpacing; // = 46
        private const float HeaderReserve = 44f;   // Title + horizontal line
        private const float DescriptionReserve = 60f; // Up to ~2 lines of description
        private const float FooterReserve = 58f;   // 38 button + 2×10 footer pad
        private const float StackPad = 12f;        // Gap below description, above footer

        public override void DoWindowContents(Rect inRect)
        {
            float y = DrawStandardHeader(inRect);
            if (y < 0f) return;

            float footerH = DefaultButtonSize.y + FooterPad * 2f;
            Rect content = new Rect(0f, y, inRect.width, inRect.height - y - footerH).ContractedBy(ContentPad);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            float descH = Text.CalcHeight(Description ?? string.Empty, content.width);
            Rect descRect = new Rect(content.x, content.y, content.width, Mathf.Min(descH, content.height));
            Widgets.Label(descRect, Description ?? string.Empty);

            // cap raised from 3 → 6 so menus that need more
            // options (e.g. Guild Management → Hall/Leaderboards/Members/
            // Delete/Leave) can use this same dialog. We also reserve the
            // scrollbar gutter unconditionally, so when 6+ buttons would
            // overflow the available stack height they scroll cleanly
            // instead of trampling the Cancel footer.
            int count = Mathf.Min(6, Labels.Length);
            float btnW = Mathf.Min(DefaultButtonSize.x, inRect.width - (ContentPad * 2f) - 20f);
            float btnH = ButtonHeight;

            float stackBottom = inRect.height - footerH - StackPad;
            float stackTop = Mathf.Max(descRect.yMax + StackPad, content.y);
            float stackH = Mathf.Max(0f, stackBottom - stackTop);

            float needed = (count * btnH) + ((count - 1) * ButtonSpacing);
            Rect stackOuter = new Rect(0f, stackTop, inRect.width, stackH);

            bool needScroll = needed > stackH + 1f;
            if (needScroll)
            {
                // Edge-case safety net — if the window was
                // sized to fit the buttons but desktop scale clipped it
                // (UI.screenHeight × 0.92 cap), the stack scrolls instead
                // of clipping behind the Cancel footer.
                Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, needed);
                Widgets.BeginScrollView(stackOuter, ref ScrollPosition, viewRect);
                for (int i = 0; i < count; i++)
                {
                    Rect btn = new Rect((viewRect.width - btnW) / 2f, i * ButtonStride, btnW, btnH);
                    DrawActionButton(i, btn);
                }
                Widgets.EndScrollView();
            }
            else
            {
                float startY = stackTop + Mathf.Max(0f, (stackH - needed) / 2f);
                for (int i = 0; i < count; i++)
                {
                    Rect btn = new Rect((inRect.width - btnW) / 2f, startY + i * ButtonStride, btnW, btnH);
                    DrawActionButton(i, btn);
                }
            }

            Rect footer = new Rect(0f, inRect.height - footerH, inRect.width, footerH);
            float cancelW = Mathf.Min(DefaultButtonSize.x, inRect.width - (FooterPad * 2f));
            Rect cancelBtn = new Rect((inRect.width - cancelW) / 2f, footer.y + FooterPad, cancelW, DefaultButtonSize.y);

            if (Widgets.ButtonText(cancelBtn, "Cancel"))
            {
                OnCancel?.Invoke();
                Close();
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawActionButton(int i, Rect btn)
        {
            string label = Labels[i] ?? $"Button {i + 1}";
            if (Widgets.ButtonText(btn, label))
            {
                if (i >= 0 && i < Actions.Length)
                    Actions[i]?.Invoke();
                Close();
            }
        }

        private void CalculateWindowSize()
        {
            // Permanent fix for the Guild Management overlap
            // (Leave button hidden under Cancel). The previous formula used
            // perButton = 38 with no spacing budget — for 6 buttons that
            // under-counted by 6 × 8 = 48 px AND chrome (180) didn't include
            // enough for the description block.
            //
            // New formula matches the renderer EXACTLY:
            //   header(44) + descBudget(60) + footer(58) + 2×StackPad(24)
            //   = 186 chrome
            // Plus per-button: btn(38) + spacing(8) = 46 each (last button
            // skips the trailing spacing, so we subtract one ButtonSpacing).
            int count = Mathf.Clamp(Labels?.Length ?? 0, 0, 6);

            const float chrome = HeaderReserve + DescriptionReserve + FooterReserve + (StackPad * 2f); // 186
            float buttonsBudget = (count * ButtonStride) - (count > 0 ? ButtonSpacing : 0f); // n×46 − 8
            float computedH = chrome + buttonsBudget + 12f /* safety margin */;

            // Width: 480 px gives ~440 px of usable inside the content rect
            // for a 250px button, the Cancel footer, and any descriptive text.
            Vector2 sizeVector = new Vector2(480f, Mathf.Max(360f, computedH));

            float w = Mathf.Min(sizeVector.x, UI.screenWidth * 0.92f);
            float h = Mathf.Min(sizeVector.y, UI.screenHeight * 0.92f);

            windowRect = new Rect(
                new Vector2((UI.screenWidth - w) / 2f, (UI.screenHeight - h) / 2f),
                new Vector2(w, h));

            windowRect.Rounded();
        }
    }
}