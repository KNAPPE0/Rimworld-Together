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

            int count = Mathf.Min(3, Labels.Length);
            float btnW = Mathf.Min(DefaultButtonSize.x, inRect.width - (ContentPad * 2f));
            float btnH = DefaultButtonSize.y;

            float stackBottom = inRect.height - footerH - 8f;
            float stackTop = Mathf.Max(descRect.yMax + 10f, content.y);
            float stackH = stackBottom - stackTop;

            float spacing = 6f;
            float needed = (count * btnH) + ((count - 1) * spacing);
            float startY = stackTop + Mathf.Max(0f, (stackH - needed) / 2f);

            for (int i = 0; i < count; i++)
            {
                Rect btn = new Rect((inRect.width - btnW) / 2f, startY + i * (btnH + spacing), btnW, btnH);

                string label = Labels[i] ?? $"Button {i + 1}";
                if (Widgets.ButtonText(btn, label))
                {
                    if (i >= 0 && i < Actions.Length)
                        Actions[i]?.Invoke();
                    Close();
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

        private void CalculateWindowSize()
        {
            int count = Mathf.Clamp(Labels?.Length ?? 0, 0, 3);

            Vector2 sizeVector = count <= 2
                ? new Vector2(420f, 300f)
                : new Vector2(420f, 340f);

            float w = Mathf.Min(sizeVector.x, UI.screenWidth * 0.92f);
            float h = Mathf.Min(sizeVector.y, UI.screenHeight * 0.92f);

            windowRect = new Rect(
                new Vector2((UI.screenWidth - w) / 2f, (UI.screenHeight - h) / 2f),
                new Vector2(w, h));

            windowRect.Rounded();
        }
    }
}