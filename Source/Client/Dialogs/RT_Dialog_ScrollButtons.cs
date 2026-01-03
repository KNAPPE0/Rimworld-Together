using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs
{
    public class RT_Dialog_ScrollButtons : RT_Dialog_Base
    {
        public override Vector2 InitialSize => new Vector2(500f, 350f);

        private string[] ButtonNames { get; set; }

        public static int SelectedScrollButton { get; private set; }

        public static RT_Dialog_Base Instance { get; private set; } = null;

        public RT_Dialog_ScrollButtons(string title, string description, string[] buttonNames, Action actionSelect, Action actionCancel)
        {
            Title = title;
            Description = description;
            ButtonNames = buttonNames ?? Array.Empty<string>();
            OnAccept = actionSelect;
            OnCancel = actionCancel;
            Instance = this;

            closeOnAccept = false;
            closeOnCancel = true;
        }

        public override void DoWindowContents(Rect rect)
        {
            float centeredX = rect.width / 2;

            float descH = Text.CalcSize(Description).y;
            float windowDescriptionDif = descH + StandardMargin;
            float descriptionLineDif1 = windowDescriptionDif - descH * 0.25f;
            float descriptionLineDif2 = windowDescriptionDif + descH * 1.1f;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(centeredX - Text.CalcSize(Title).x / 2, rect.y, Text.CalcSize(Title).x, Text.CalcSize(Title).y), Title);

            Widgets.DrawLineHorizontal(rect.x, descriptionLineDif1, rect.width);

            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(centeredX - Text.CalcSize(Description).x / 2, windowDescriptionDif, Text.CalcSize(Description).x, descH), Description);

            Text.Font = GameFont.Medium;
            Widgets.DrawLineHorizontal(rect.x, descriptionLineDif2, rect.width);

            GenerateList(new Rect(rect.x, rect.yMax - DefaultButtonSize.y * 5 - 40, rect.width, 175f), ButtonNames);

            if (Widgets.ButtonText(new Rect(new Vector2(centeredX - DefaultButtonSize.x / 2, rect.yMax - DefaultButtonSize.y), DefaultButtonSize), "Cancel"))
                OnBack();
        }

        private void OnBack()
        {
            OnCancel?.Invoke();
            Close();
        }

        private void GenerateList(Rect mainRect, string[] buttons)
        {
            float rowH = DefaultButtonSize.y;
            float height = 6f + buttons.Length * rowH;

            Rect viewRect = new Rect(0f, 0f, mainRect.width - 16f, height);

            Widgets.BeginScrollView(mainRect, ref ScrollPosition, viewRect);
            try
            {
                float y = 0f;
                float yMin = ScrollPosition.y - rowH;
                float yMax = ScrollPosition.y + mainRect.height;

                for (int i = 0; i < buttons.Length; i++)
                {
                    if (y > yMin && y < yMax)
                    {
                        Rect row = new Rect(0f, y, viewRect.width, rowH);
                        DrawCustomRow(row, buttons[i]);
                    }
                    y += rowH;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private void DrawCustomRow(Rect rect, string buttonName)
        {
            Text.Font = GameFont.Small;
            Rect fixedRect = new Rect(rect.x + 10f, rect.y + 5f, rect.width - 36f, rect.height);

            if (Widgets.ButtonText(fixedRect, buttonName))
            {
                for (int i = 0; i < ButtonNames.Length; i++)
                {
                    if (ButtonNames[i] == buttonName)
                    {
                        SelectedScrollButton = i;
                        OnAccept?.Invoke();
                        Close();
                        break;
                    }
                }
            }
        }
    }
}