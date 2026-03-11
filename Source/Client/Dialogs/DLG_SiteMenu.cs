using GameClient.Defs;
using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs
{
    public class DLG_SiteMenu : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(720f, 480f);

        private bool IsInConfigMode { get; set; }

        public static DLG_Base Instance { get; private set; } = null;

        public DLG_SiteMenu(bool configMode)
        {
            Instance = this;
            Title = "Choose a site";
            IsInConfigMode = configMode;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = DrawStandardHeader(inRect, drawTopBorder: true, drawBottomBorder: true, closeX: true);
            if (y < 0f) return;

            Rect listOuter = new Rect(0f, y, inRect.width, inRect.height - y).ContractedBy(ContentPad);

            Widgets.DrawMenuSection(listOuter);
            Rect mainRect = listOuter.ContractedBy(10f);

            float rowH = 52f;
            int count = RTSitePartDefs.Defs?.Length ?? 0;

            float height = 6f + count * rowH;
            Rect viewRect = new Rect(0f, 0f, mainRect.width - GenUI.ScrollBarWidth, height);

            Widgets.BeginScrollView(mainRect, ref ScrollPosition, viewRect);
            try
            {
                float cy = 0f;
                float yMin = ScrollPosition.y - rowH;
                float yMax = ScrollPosition.y + mainRect.height;

                for (int i = 0; i < count; i++)
                {
                    if (cy > yMin && cy < yMax)
                    {
                        Rect row = new Rect(0f, cy, viewRect.width, rowH);
                        DrawCustomRow(row, RTSitePartDefs.Defs[i], i);
                    }
                    cy += rowH;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private void DrawCustomRow(Rect row, SitePartDef thing, int index)
        {
            if (thing == null) return;

            if (index % 2 == 0) Widgets.DrawAltRect(row);
            Widgets.DrawHighlightIfMouseover(row);

            Rect inner = row.ContractedBy(6f, 4f);

            Rect iconRect = new Rect(inner.x, inner.y, 44f, 44f);
            Rect textRect = new Rect(iconRect.xMax + 10f, inner.y, inner.width - 44f - 10f, inner.height);

            Widgets.DrawTextureFitted(iconRect, thing.ExpandingIconTexture, 1f);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(textRect, thing.description ?? string.Empty);
            Text.Anchor = TextAnchor.UpperLeft;

            if (Widgets.ButtonInvisible(row))
            {
                if (IsInConfigMode) Find.WindowStack.Add(new DLG_SiteMenuConfig(thing));
                else Find.WindowStack.Add(new DLG_SiteMenuInfo(thing));
            }
        }
    }
}