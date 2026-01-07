using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs
{
    public class RT_Dialog_Listing : RT_Dialog_Base
    {
        public override Vector2 InitialSize => new Vector2(560f, 440f);

        public string[] Elements { get; private set; }

        public RT_Dialog_Listing(string title, string description, string[] elements, Action actionOK = null)
        {
            Title = title;
            Description = description;

            Elements = elements ?? Array.Empty<string>();
            OnAccept = actionOK;

            closeOnAccept = false;
            closeOnCancel = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = DrawStandardHeader(inRect);
            if (y < 0f) return;

            float footerH = SlimButtonSize.y + FooterPad * 2f;

            Rect descOuter = new Rect(0f, y, inRect.width, 0f);
            Rect contentOuter = new Rect(0f, y, inRect.width, inRect.height - y - footerH);

            Rect content = contentOuter.ContractedBy(ContentPad);

            Text.Font = GameFont.Small;
            float descH = Text.CalcHeight(Description ?? string.Empty, content.width);
            Rect descRect = new Rect(content.x, content.y, content.width, Mathf.Min(descH, 70f));
            Widgets.Label(descRect, Description ?? string.Empty);

            float listTop = descRect.yMax + 8f;
            Rect listOuter = new Rect(content.x, listTop, content.width, content.yMax - listTop);

            Widgets.DrawMenuSection(listOuter);

            Rect inner = listOuter.ContractedBy(10f);
            FillMainRect(inner);

            Rect footer = new Rect(0f, inRect.height - footerH, inRect.width, footerH);
            float okW = Mathf.Min(SlimButtonSize.x, inRect.width - (FooterPad * 2f));
            Rect okBtn = new Rect((inRect.width - okW) / 2f, footer.y + FooterPad, okW, SlimButtonSize.y);

            if (Widgets.ButtonText(okBtn, "OK"))
            {
                OnAccept?.Invoke();
                Close();
            }
        }

        private void FillMainRect(Rect mainRect)
        {
            float rowH = 30f;
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

                        if (i % 2 == 0) Widgets.DrawAltRect(row);
                        Widgets.DrawHighlightIfMouseover(row);

                        Text.Font = GameFont.Small;
                        Rect textRect = row.ContractedBy(6f, 4f);
                        Widgets.Label(textRect, Elements[i] ?? string.Empty);
                    }
                    y += rowH;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }
    }
}