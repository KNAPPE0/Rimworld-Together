using System;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs
{
    public class RT_Dialog_Inputs : RT_Dialog_Base
    {
        private float InputWidth { get; set; } = 300f;
        private float InputHeight { get; set; } = 30f;
        private int MaxChars { get; set; } = 512;

        private string[] Labels { get; set; } = Array.Empty<string>();
        private bool[] Censors { get; set; } = Array.Empty<bool>();

        private string[] Results { get; set; } = new string[] { "", "", "" };

        public static string[] DialogInputResults { get; set; }

        private string ConfirmText { get; set; } = "Confirm";
        private string CancelText { get; set; } = "Cancel";

        public RT_Dialog_Inputs(
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

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;

            float titleH = Text.CalcHeight(Title, rect.width);
            Rect titleRect = new Rect(rect.x, rect.y, rect.width, titleH);
            Widgets.Label(titleRect, Title);

            Text.Anchor = TextAnchor.UpperLeft;

            float sepY = titleRect.yMax + (StandardMargin / 2f);
            Widgets.DrawLineHorizontal(rect.x, sepY, rect.width);

            Text.Font = GameFont.Small;

            float y = sepY + StandardMargin;

            if (Labels.Length > 0)
            {
                y = DrawInputBlock(rect, y, 0);

                if (Labels.Length > 1)
                {
                    y = DrawInputBlock(rect, y, 1);

                    if (Labels.Length > 2)
                    {
                        y = DrawInputBlock(rect, y, 2);
                    }
                }
            }

            if (Widgets.ButtonText(GetRectForLocation(rect, SmallButtonSize, RectLocation.BottomLeft), ConfirmText))
            {
                DialogInputResults = new string[] { Results[0] ?? "", Results[1] ?? "", Results[2] ?? "" };
                OnAccept?.Invoke();
                Close();
            }

            if (Widgets.ButtonText(GetRectForLocation(rect, SmallButtonSize, RectLocation.BottomRight), CancelText))
            {
                OnCancel?.Invoke();
                Close();
            }
        }

        private float DrawInputBlock(Rect rect, float startY, int index)
        {
            if (index < 0 || index >= 3) return startY;
            if (index >= Labels.Length) return startY;

            string label = Labels[index] ?? string.Empty;

            Text.Anchor = TextAnchor.UpperCenter;
            float labelH = Text.CalcHeight(label, rect.width);
            Rect labelRect = new Rect(rect.x, startY, rect.width, labelH);
            Widgets.Label(labelRect, label);
            Text.Anchor = TextAnchor.UpperLeft;

            float inputY = labelRect.yMax + (StandardMargin / 2f);

            float inputX = rect.x + ((rect.width - InputWidth) / 2f);
            Rect inputRect = new Rect(inputX, inputY, InputWidth, InputHeight);

            DrawInputField(inputRect, index);

            return inputRect.yMax + StandardMargin;
        }

        private void DrawInputField(Rect inputRect, int index)
        {
            bool censor = (index < Censors.Length) && Censors[index];
            string current = Results[index] ?? string.Empty;

            if (!AcceptsInput)
            {
                if (censor)
                    DrawPasswordField(inputRect, current);
                else
                    Widgets.TextField(inputRect, current);
                return;
            }

            if (censor)
            {
                string next = DrawPasswordField(inputRect, current);
                if (next.Length <= MaxChars) Results[index] = next;
            }
            else
            {
                string next = Widgets.TextField(inputRect, current);
                if (next.Length <= MaxChars) Results[index] = next;
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
            Vector2 sizeVector;

            if (Labels.Length <= 1) sizeVector = new Vector2(400f, 190f);
            else if (Labels.Length == 2) sizeVector = new Vector2(400f, 280f);
            else if (Labels.Length == 3) sizeVector = new Vector2(400f, 370f);
            else throw new ArgumentOutOfRangeException();

            windowRect = new Rect(
                new Vector2((UI.screenWidth - sizeVector.x) / 2f, (UI.screenHeight - sizeVector.y) / 2f),
                sizeVector);

            windowRect.Rounded();
        }
    }
}