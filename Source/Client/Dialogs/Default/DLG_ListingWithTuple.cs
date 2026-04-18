using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs.Default
{
    public class DLG_ListingWithTuple : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(560f, 440f);

        public string[] Keys { get; private set; }
        public string[] Values { get; private set; }

        public string[] ValueString { get; private set; }
        public int[] ValueInt { get; private set; }

        public static string[] DialogTupleListingResultString { get; private set; }
        public static int[] DialogTupleListingResultInt { get; private set; }

        public DLG_ListingWithTuple(string title, string description, string[] keys, string[] values, int[] defaultValues = null, Action actionAccept = null)
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

        public override void DoWindowContents(Rect inRect)
        {
            float y = DrawStandardHeader(inRect);
            if (y < 0f) return;

            float footerH = DefaultButtonSize.y + FooterPad * 2f;

            Rect contentOuter = new Rect(0f, y, inRect.width, inRect.height - y - footerH).ContractedBy(ContentPad);

            Text.Font = GameFont.Small;
            float descH = Text.CalcHeight(Description ?? string.Empty, contentOuter.width);
            Rect descRect = new Rect(contentOuter.x, contentOuter.y, contentOuter.width, Mathf.Min(descH, 70f));
            Widgets.Label(descRect, Description ?? string.Empty);

            Vector2 tiny = ClampButtonSize(TinyButtonSize, 60f, 40f);
            Rect globalBtn = new Rect(contentOuter.xMax - tiny.x, contentOuter.y, tiny.x, tiny.y);
            if (Widgets.ButtonText(globalBtn, "▶"))
                ShowFloatMenu(-1, true);

            float listTop = descRect.yMax + 8f;
            Rect listOuter = new Rect(contentOuter.x, listTop, contentOuter.width, contentOuter.yMax - listTop);
            Widgets.DrawMenuSection(listOuter);

            Rect listInner = listOuter.ContractedBy(10f);
            FillMainRect(listInner);

            Rect footer = new Rect(0f, inRect.height - footerH, inRect.width, footerH);
            float acceptW = Mathf.Min(DefaultButtonSize.x, inRect.width - (FooterPad * 2f));
            Rect acceptBtn = new Rect((inRect.width - acceptW) / 2f, footer.y + FooterPad, acceptW, DefaultButtonSize.y);

            if (Widgets.ButtonText(acceptBtn, "Accept"))
            {
                DialogTupleListingResultString = Keys;
                DialogTupleListingResultInt = ValueInt;
                OnAccept?.Invoke();
                Close();
            }
        }

        private void FillMainRect(Rect mainRect)
        {
            float rowH = 34f;
            float height = 6f + Keys.Length * rowH;

            Rect viewRect = new Rect(0f, 0f, mainRect.width - GenUI.ScrollBarWidth, height);

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

        private void DrawCustomRow(Rect row, string element, int index)
        {
            if (index % 2 == 0) Widgets.DrawAltRect(row);
            Widgets.DrawHighlightIfMouseover(row);

            Rect inner = row.ContractedBy(6f, 4f);

            float btnW = 120f;
            Rect btn = new Rect(inner.xMax - btnW, inner.y, btnW, inner.height);

            Rect labelRect = new Rect(inner.x, inner.y, inner.width - btnW - 8f, inner.height);

            Text.Font = GameFont.Small;
            Widgets.LabelEllipses(labelRect, element ?? string.Empty);

            string buttonLabel = ValueString[index] ?? "";
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