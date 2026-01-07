using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs
{
    public class RT_Dialog_ScrollButtons : RT_Dialog_Base
    {
        public override Vector2 InitialSize => new Vector2(520f, 420f);

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

            float listTop = descRect.yMax + 8f;
            Rect listOuter = new Rect(contentOuter.x, listTop, contentOuter.width, contentOuter.yMax - listTop);

            Widgets.DrawMenuSection(listOuter);
            Rect listInner = listOuter.ContractedBy(10f);

            GenerateList(listInner, ButtonNames);

            Rect footer = new Rect(0f, inRect.height - footerH, inRect.width, footerH);
            float cancelW = Mathf.Min(DefaultButtonSize.x, inRect.width - (FooterPad * 2f));
            Rect cancelBtn = new Rect((inRect.width - cancelW) / 2f, footer.y + FooterPad, cancelW, DefaultButtonSize.y);

            if (Widgets.ButtonText(cancelBtn, "Cancel"))
            {
                OnCancel?.Invoke();
                Close();
            }
        }

        private void GenerateList(Rect mainRect, string[] buttons)
        {
            float rowH = 34f;
            float height = 6f + buttons.Length * rowH;

            Rect viewRect = new Rect(0f, 0f, mainRect.width - GenUI.ScrollBarWidth, height);

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

                        if (i % 2 == 0) Widgets.DrawAltRect(row);
                        Widgets.DrawHighlightIfMouseover(row);

                        Rect btnRect = row.ContractedBy(6f, 4f);
                        if (Widgets.ButtonText(btnRect, buttons[i] ?? $"Option {i + 1}"))
                        {
                            SelectedScrollButton = i;
                            OnAccept?.Invoke();
                            Close();
                            break;
                        }
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