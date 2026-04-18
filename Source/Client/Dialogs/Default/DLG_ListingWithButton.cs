using System;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs.Default
{
    public class DLG_ListingWithButton : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(520f, 440f);

        public string[] Elements { get; private set; }

        public static string ResultString { get; private set; }
        public static int ResultInt { get; private set; }

        public DLG_ListingWithButton(string title, string description, string[] elements, Action actionClick = null, Action actionCancel = null)
        {
            Title = title;
            Description = description;

            Elements = elements ?? Array.Empty<string>();
            OnAccept = actionClick;
            OnCancel = actionCancel;

            closeOnAccept = false;
            closeOnCancel = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = DrawStandardHeader(inRect);
            if (y < 0f) return;

            float footerH = SlimButtonSize.y + FooterPad * 2f;

            Rect contentOuter = new Rect(0f, y, inRect.width, inRect.height - y - footerH).ContractedBy(ContentPad);

            Text.Font = GameFont.Small;
            float descH = Text.CalcHeight(Description ?? string.Empty, contentOuter.width);
            Rect descRect = new Rect(contentOuter.x, contentOuter.y, contentOuter.width, Mathf.Min(descH, 70f));
            Widgets.Label(descRect, Description ?? string.Empty);

            float listTop = descRect.yMax + 8f;
            Rect listOuter = new Rect(contentOuter.x, listTop, contentOuter.width, contentOuter.yMax - listTop);
            Widgets.DrawMenuSection(listOuter);

            Rect listInner = listOuter.ContractedBy(10f);
            FillMainRect(listInner);

            Rect footer = new Rect(0f, inRect.height - footerH, inRect.width, footerH);
            float closeW = Mathf.Min(SlimButtonSize.x, inRect.width - (FooterPad * 2f));
            Rect closeBtn = new Rect((inRect.width - closeW) / 2f, footer.y + FooterPad, closeW, SlimButtonSize.y);

            if (Widgets.ButtonText(closeBtn, "Close"))
            {
                OnCancel?.Invoke();
                Close();
            }
        }

        private void FillMainRect(Rect mainRect)
        {
            float rowH = 34f;
            float height = 6f + Elements.Length * rowH;

            Rect viewRect = new Rect(0f, 0f, mainRect.width - GenUI.ScrollBarWidth, height);

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

        private void DrawCustomRow(Rect row, string element, int index)
        {
            if (index % 2 == 0) Widgets.DrawAltRect(row);
            Widgets.DrawHighlightIfMouseover(row);

            Rect inner = row.ContractedBy(6f, 4f);

            float btnW = 90f;
            Rect btn = new Rect(inner.xMax - btnW, inner.y, btnW, inner.height);

            Rect labelRect = new Rect(inner.x, inner.y, inner.width - btnW - 8f, inner.height);

            Text.Font = GameFont.Small;
            Widgets.LabelEllipses(labelRect, element ?? string.Empty);

            if (Widgets.ButtonText(btn, "Select"))
            {
                ResultInt = index;
                ResultString = element;
                OnAccept?.Invoke();
                Close();
            }
        }
    }
}