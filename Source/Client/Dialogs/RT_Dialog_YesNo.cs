using System;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs
{
    public class RT_Dialog_YesNo : RT_Dialog_Base
    {
        public override Vector2 InitialSize => new Vector2(400f, 150f);

        private string YesText { get; set; }
        private string NoText { get; set; }
        private Color YesColor { get; set; }
        private Color NoColor { get; set; }

        public RT_Dialog_YesNo(string description, Action actionYes, Action actionNo = null,
            string yText = "Yes", string nText = "No", Color? yesColor = null, Color? noColor = null)
        {
            Title = "OPTION";
            Description = description;
            OnAccept = actionYes;
            OnCancel = actionNo;
            YesText = yText;
            NoText = nText;

            YesColor = (yesColor ?? Color.white);
            NoColor = (noColor ?? Color.white);

            closeOnAccept = false;
            closeOnCancel = false;
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

            GUI.color = YesColor;
            if (Widgets.ButtonText(GetRectForLocation(rect, SmallButtonSize, RectLocation.BottomLeft), YesText))
            {
                OnAccept?.Invoke();
                Close();
            }

            GUI.color = NoColor;
            if (Widgets.ButtonText(GetRectForLocation(rect, SmallButtonSize, RectLocation.BottomRight), NoText))
            {
                OnCancel?.Invoke();
                Close();
            }

            GUI.color = Color.white;
        }
    }
}