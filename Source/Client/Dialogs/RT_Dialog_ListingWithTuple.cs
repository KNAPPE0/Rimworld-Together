using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs
{
    public class RT_Dialog_ListingWithTuple : RT_Dialog_Base
    {
        public override Vector2 InitialSize => new Vector2(500f, 400f);

        public string[] Keys { get; private set; }
        public string[] Values { get; private set; }

        public string[] ValueString { get; private set; }
        public int[] ValueInt { get; private set; }

        public static string[] DialogTupleListingResultString { get; private set; }
        public static int[] DialogTupleListingResultInt { get; private set; }

        public RT_Dialog_ListingWithTuple(string title, string description, string[] keys, string[] values, int[] defaultValues = null, Action actionAccept = null)
        {
            Title = title;
            Description = description;
            Keys = keys ?? Array.Empty<string>();
            Values = values ?? Array.Empty<string>();
            OnAccept = actionAccept;

            closeOnAccept = false;
            closeOnCancel = false;

            ValueString = new string[Keys.Length];
            ValueInt = new int[Keys.Length];

            for (int i = 0; i < Keys.Length; i++)
            {
                ValueString[i] = (Values.Length > 0) ? Values[0] : "";
                ValueInt[i] = 0;
            }

            if (defaultValues != null)
            {
                for (int i = 0; i < ValueString.Length && i < defaultValues.Length; i++)
                {
                    int dv = defaultValues[i];
                    if (dv >= 0 && dv < Values.Length)
                    {
                        ValueString[i] = Values[dv];
                        ValueInt[i] = dv;
                    }
                }
            }
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

            FillMainRect(new Rect(0f, descriptionLineDif2 + 10f, rect.width, rect.height - DefaultButtonSize.y - 85f));

            Text.Font = GameFont.Small;

            if (Widgets.ButtonText(GetRectForLocation(rect, TinyButtonSize, RectLocation.TopRight), "▶"))
                ShowFloatMenu(-1, true);

            if (Widgets.ButtonText(GetRectForLocation(rect, DefaultButtonSize, RectLocation.BottomCenter), "Accept"))
            {
                DialogTupleListingResultString = Keys;
                DialogTupleListingResultInt = ValueInt;
                OnAccept?.Invoke();
                Close();
            }
        }

        private void FillMainRect(Rect mainRect)
        {
            float rowH = 30f;
            float height = 6f + Keys.Length * rowH;

            Rect viewRect = new Rect(0f, 0f, mainRect.width - 16f, height);

            Widgets.BeginScrollView(mainRect, ref ScrollPosition, viewRect);
            try
            {
                float y = 0f;
                float yMin = ScrollPosition.y - rowH;
                float yMax = ScrollPosition.y + mainRect.height;

                for (int i = 0; i < Keys.Length; i++)
                {
                    if (y > yMin && y < yMax)
                    {
                        Rect row = new Rect(0f, y, viewRect.width, rowH);
                        DrawCustomRow(row, Keys[i], i);
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

            string buttonLabel = ValueString[index] ?? "";
            Rect btn = new Rect(rect.xMax - LongButtonSize.x, rect.y + (rect.height - LongButtonSize.y) / 2f, LongButtonSize.x, LongButtonSize.y);
            if (Widgets.ButtonText(btn, buttonLabel))
                ShowFloatMenu(index, false);
        }

        private void ShowFloatMenu(int index, bool globalChange)
        {
            List<FloatMenuOption> list = new List<FloatMenuOption>();

            for (int i = 0; i < Values.Length; i++)
            {
                string choice = Values[i];
                int choiceIndex = i;

                list.Add(new FloatMenuOption(choice, () =>
                {
                    if (globalChange)
                    {
                        for (int k = 0; k < ValueString.Length; k++)
                        {
                            ValueString[k] = choice;
                            ValueInt[k] = choiceIndex;
                        }
                    }
                    else
                    {
                        if (index >= 0 && index < ValueString.Length)
                        {
                            ValueString[index] = choice;
                            ValueInt[index] = choiceIndex;
                        }
                    }
                }));
            }

            Find.WindowStack.Add(new FloatMenu(list));
        }
    }
}