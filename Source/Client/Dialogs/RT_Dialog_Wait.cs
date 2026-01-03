using UnityEngine;
using Verse;

namespace GameClient.Dialogs
{
    public class RT_Dialog_Wait : RT_Dialog_Base
    {
        public override Vector2 InitialSize => new Vector2(300f, 100f);

        public static RT_Dialog_Base Instance { get; private set; } = null;

        public RT_Dialog_Wait(string description = "[MISSING MESSAGE]")
        {
            Instance = this;
            Title = "WAIT";
            Description = description;

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
        }
    }
}