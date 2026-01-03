using System;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs
{
    public class RT_Dialog_ListingWithButton : RT_Dialog_Base
    {
        public override Vector2 InitialSize => new Vector2(400f, 400f);

        public string[] Elements { get; private set; }

        public static string DialogButtonListingResultString { get; private set; }
        public static int DialogButtonListingResultInt { get; private set; }

        public RT_Dialog_ListingWithButton(string title, string description, string[] elements, Action actionClick = null, Action actionCancel = null)
        {
            Title = title;
            Description = description;
            Elements = elements ?? Array.Empty<string>();
            OnAccept = actionClick;
            OnCancel = actionCancel;

            closeOnAccept = false;
            closeOnCancel = false;
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

            FillMainRect(new Rect(0f, descriptionLineDif2 + 10f, rect.width, rect.height - SlimButtonSize.y - 85f));

            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(new Rect(new Vector2(centeredX - SlimButtonSize.x / 2, rect.yMax - SlimButtonSize.y), SlimButtonSize), "Close"))
            {
                OnCancel?.Invoke();
                Close();
            }
        }

        private void FillMainRect(Rect mainRect)
        {
            float rowH = 30f;
            float height = 6f + Elements.Length * rowH;

            Rect viewRect = new Rect(0f, 0f, mainRect.width - 16f, height);

            Widgets.BeginScrollView(mainRect, ref ScrollPosition, viewRect);
            try
            {
                float y = 0f;
                float yMin = ScrollPosition.y - rowH;
                float yMax = ScrollPosition.y + mainRect.height;

                for (int i = 0; i < Elements.Length; i++)
                {
                    if (y > yMin && y < yMax)
                    {
                        Rect row = new Rect(0f, y, viewRect.width, rowH);
                        DrawCustomRow(row, Elements[i], i);
                    }
                    y += rowH;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private void DrawCustomRow(Rect rect, string element, int index)
        {
            Text.Font = GameFont.Small;

            Rect fixedRect = new Rect(rect.x, rect.y + 5f, rect.width - 16f, rect.height - 5f);
            if (index % 2 == 0) Widgets.DrawHighlight(fixedRect);

            Widgets.Label(fixedRect, element);

            Rect btn = new Rect(rect.xMax - TinyButtonSize.x, rect.y + (rect.height - TinyButtonSize.y) / 2f, TinyButtonSize.x, TinyButtonSize.y);
            if (Widgets.ButtonText(btn, "Select"))
            {
                DialogButtonListingResultInt = index;
                DialogButtonListingResultString = element;
                OnAccept?.Invoke();
                Close();
            }
        }
    }
}