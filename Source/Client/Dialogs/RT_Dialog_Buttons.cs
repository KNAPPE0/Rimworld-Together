using System;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs
{
    public class RT_Dialog_Buttons : RT_Dialog_Base
    {
        private string[] Labels { get; set; }
        private Action[] Actions { get; set; }

        public RT_Dialog_Buttons(string title, string description, string[] labels, Action[] actions, Action onCancel = null)
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

        public override void DoWindowContents(Rect rect)
        {
            float centeredX = rect.width / 2;

            float descH = Text.CalcSize(Description).y;
            float horizontalLineDif = descH + StandardMargin / 2;
            float windowDescriptionDif = descH + StandardMargin;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(centeredX - Text.CalcSize(Title).x / 2, rect.y, Text.CalcSize(Title).x, Text.CalcSize(Title).y), Title);

            Widgets.DrawLineHorizontal(rect.x, horizontalLineDif, rect.width);

            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(centeredX - Text.CalcSize(Description).x / 2, windowDescriptionDif, Text.CalcSize(Description).x, descH), Description);

            DrawCancelButton(centeredX, rect.yMax - DefaultButtonSize.y);

            if (Labels.Length > 0) DrawButton(centeredX, rect.yMax - DefaultButtonSize.y * 2 - 10f, 0);
            if (Labels.Length > 1) DrawButton(centeredX, rect.yMax - DefaultButtonSize.y * 3 - 20f, 1);
            if (Labels.Length > 2) DrawButton(centeredX, rect.yMax - DefaultButtonSize.y * 4 - 30f, 2);
        }

        private void CalculateWindowSize()
        {
            Vector2 sizeVector;

            if (Labels.Length <= 2)
                sizeVector = new Vector2(350f, 250f);
            else
                sizeVector = new Vector2(350f, 285f);

            windowRect = new Rect(new Vector2((UI.screenWidth - sizeVector.x) / 2f, (UI.screenHeight - sizeVector.y) / 2f), sizeVector);
            windowRect.Rounded();
        }

        private void DrawButton(float centeredX, float height, int index)
        {
            if (index < 0 || index >= Labels.Length) return;

            if (Widgets.ButtonText(new Rect(new Vector2(centeredX - DefaultButtonSize.x / 2, height), DefaultButtonSize), Labels[index]))
            {
                if (index >= 0 && index < Actions.Length)
                    Actions[index]?.Invoke();

                Close();
            }
        }

        private void DrawCancelButton(float centeredX, float height)
        {
            if (Widgets.ButtonText(new Rect(new Vector2(centeredX - DefaultButtonSize.x / 2, height), DefaultButtonSize), "Cancel"))
            {
                OnCancel?.Invoke();
                Close();
            }
        }
    }
}