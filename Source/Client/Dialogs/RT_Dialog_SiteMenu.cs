using GameClient.Defs;
using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs
{
    public class RT_Dialog_SiteMenu : RT_Dialog_Base
    {
        public override Vector2 InitialSize => new Vector2(700f, 450);

        private bool IsInConfigMode { get; set; }

        public static RT_Dialog_Base Instance { get; private set; } = null;

        public RT_Dialog_SiteMenu(bool configMode)
        {
            Instance = this;
            Title = "Choose a site";
            IsInConfigMode = configMode;
        }

        public override void DoWindowContents(Rect rect)
        {
            Widgets.DrawLineHorizontal(rect.x, rect.y - 1, rect.width);
            Widgets.DrawLineHorizontal(rect.x, rect.yMax + 1, rect.width);

            float centeredX = rect.width / 2;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(centeredX - Text.CalcSize(Title).x / 2, rect.y, Text.CalcSize(Title).x, Text.CalcSize(Title).y), Title);

            if (Widgets.CloseButtonFor(rect)) { Close(); return; }

            Rect mainRect = new Rect(0f, 50f, rect.width, rect.height - 50f);

            float rowH = 50f;
            int count = RTSitePartDefs.Defs?.Length ?? 0;

            float height = 6f + count * rowH;
            Rect viewRect = new Rect(0f, 0f, mainRect.width - 16f, height);

            Widgets.BeginScrollView(mainRect, ref ScrollPosition, viewRect);
            try
            {
                float y = 0f;
                float yMin = ScrollPosition.y - rowH;
                float yMax = ScrollPosition.y + mainRect.height;

                for (int i = 0; i < count; i++)
                {
                    if (y > yMin && y < yMax)
                    {
                        Rect row = new Rect(0f, y, viewRect.width, rowH);
                        DrawCustomRow(row, RTSitePartDefs.Defs[i], i);
                    }
                    y += rowH;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private void DrawCustomRow(Rect rect, SitePartDef thing, int index)
        {
            Text.Font = GameFont.Small;

            Rect highLightRect = new Rect(rect.x, rect.y, rect.width - 16f, rect.height);
            Rect iconRect = new Rect(rect.x, rect.y, 50f, 50f);
            Rect textRect = new Rect(rect.x + 75f, rect.y, highLightRect.width - 75f, rect.height);

            if (index % 2 == 0) Widgets.DrawHighlight(highLightRect);

            Widgets.DrawTextureFitted(iconRect, thing.ExpandingIconTexture, 1f);
            Widgets.Label(textRect, thing.description);

            if (Mouse.IsOver(highLightRect))
            {
                Widgets.DrawLineHorizontal(highLightRect.x, highLightRect.y, highLightRect.width);
                Widgets.DrawLineHorizontal(highLightRect.x, highLightRect.yMax, highLightRect.width);
                Widgets.DrawLineVertical(highLightRect.x, highLightRect.y, highLightRect.height);
                Widgets.DrawLineVertical(highLightRect.xMax - 1, highLightRect.y, highLightRect.height);
            }

            if (Widgets.ButtonInvisible(highLightRect))
            {
                if (IsInConfigMode) Find.WindowStack.Add(new RT_Dialog_SiteMenu_Config(thing));
                else Find.WindowStack.Add(new RT_Dialog_SiteMenu_Info(thing));
            }
        }
    }
}