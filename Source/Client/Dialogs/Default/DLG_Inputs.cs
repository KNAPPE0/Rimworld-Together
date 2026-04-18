using System;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs.Default
{
    public class DLG_Inputs : DLG_Base
    {
        private float InputWidth { get; set; } = 320f;
        private float InputHeight { get; set; } = 30f;
        private int MaxChars { get; set; } = 512;

        private string[] Labels { get; set; } = Array.Empty<string>();
        private bool[] Censors { get; set; } = Array.Empty<bool>();

        private string[] Results { get; set; } = new string[] { "", "", "" };

        public static string[] DialogInputResults { get; set; }

        private string ConfirmText { get; set; } = "Confirm";
        private string CancelText { get; set; } = "Cancel";

        public DLG_Inputs(
            string title,
            string[] labels,
            bool[] censors,
            Action onConfirm = null,
            Action onCancel = null,
            string onConfirmText = "Confirm",
            string onCancelText = "Cancel")
        {
            Title = title;
            OnAccept = onConfirm;
            OnCancel = onCancel;

            Labels = labels ?? Array.Empty<string>();
            Censors = censors ?? Array.Empty<bool>();

            ConfirmText = string.IsNullOrWhiteSpace(onConfirmText) ? "Confirm" : onConfirmText;
            CancelText = string.IsNullOrWhiteSpace(onCancelText) ? "Cancel" : onCancelText;

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

            float footerH = SmallButtonSize.y + FooterPad * 2f;

            Rect contentOuter = new Rect(0f, y, inRect.width, inRect.height - y - footerH);
            Rect content = contentOuter.ContractedBy(ContentPad);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            float inputW = Mathf.Min(InputWidth, content.width);
            float inputX = content.x + (content.width - inputW) / 2f;

            float curY = content.y;

            int blocks = Mathf.Min(3, Labels.Length);
            for (int i = 0; i < blocks; i++)
            {
                string label = Labels[i] ?? string.Empty;
                float labelH = Mathf.Max(18f, Text.CalcHeight(label, content.width));

                Rect labelRect = new Rect(content.x, curY, content.width, labelH);
                Widgets.Label(labelRect, label);
                curY = labelRect.yMax + 6f;

                Rect inputRect = new Rect(inputX, curY, inputW, InputHeight);
                DrawInputField(inputRect, i);

                curY = inputRect.yMax + 10f;
            }

            Rect footer = new Rect(0f, inRect.height - footerH, inRect.width, footerH);
            float btnAvailHalf = (footer.width - (FooterPad * 3f)) / 2f;
            Vector2 btnSize = ClampButtonSize(SmallButtonSize, btnAvailHalf);

            Rect confirmBtn = new Rect(FooterPad, footer.y + FooterPad, btnSize.x, btnSize.y);
            Rect cancelBtn = new Rect(footer.xMax - FooterPad - btnSize.x, footer.y + FooterPad, btnSize.x, btnSize.y);

            if (Widgets.ButtonText(confirmBtn, ConfirmText))
            {
                DialogInputResults = new string[]
                {
                    Results[0] ?? string.Empty,
                    Results[1] ?? string.Empty,
                    Results[2] ?? string.Empty
                };

                OnAccept?.Invoke();
                Close();
            }

            if (Widgets.ButtonText(cancelBtn, CancelText))
            {
                OnCancel?.Invoke();
                Close();
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawInputField(Rect inputRect, int index)
        {
            bool censor = index < Censors.Length && Censors[index];
            string current = Results[index] ?? string.Empty;

            if (!AcceptsInput)
            {
                if (censor) DrawPasswordField(inputRect, current);
                else Widgets.TextField(inputRect, current);
                return;
            }

            if (censor)
            {
                string next = DrawPasswordField(inputRect, current);
                if (next.Length <= MaxChars)
                    Results[index] = next;
            }
            else
            {
                string next = Widgets.TextField(inputRect, current);
                if (next.Length <= MaxChars)
                    Results[index] = next;
            }
        }

        private string DrawPasswordField(Rect rect, string value)
        {
            GUIStyle style = new GUIStyle(GUI.skin.textField)
            {
                alignment = TextAnchor.MiddleLeft
            };

            return GUI.PasswordField(rect, value ?? string.Empty, '█', MaxChars, style);
        }

        private void CalculateWindowSize()
        {
            int blocks = Mathf.Clamp(Labels?.Length ?? 0, 1, 3);

            float baseW = 460f;
            float baseH = 150f;
            float perBlock = 68f;

            Vector2 sizeVector = new Vector2(baseW, baseH + (blocks * perBlock));

            float w = Mathf.Min(sizeVector.x, UI.screenWidth * 0.92f);
            float h = Mathf.Min(sizeVector.y, UI.screenHeight * 0.92f);

            windowRect = new Rect(
                new Vector2((UI.screenWidth - w) / 2f, (UI.screenHeight - h) / 2f),
                new Vector2(w, h));

            windowRect.Rounded();
        }
    }
}