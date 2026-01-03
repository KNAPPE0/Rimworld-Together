using System.Linq;
using GameClient.Managers;
using GameClient.WorldObjects;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace GameClient.Patches.Tabs
{
    public class SitesUI : WITab
    {
        private Vector2 _scroll = Vector2.zero;

        private static readonly Vector2 WinSize = new Vector2(432f, 540f);

        public override bool IsVisible => true;
        protected override bool StillValid => true;

        public SitesUI()
        {
            size = WinSize;
            labelKey = "Sites";
        }

        protected override void FillTab()
        {
            Rect outer = new Rect(0f, 0f, WinSize.x, WinSize.y).ContractedBy(10f);

            int count = SiteManager.PlayerSites?.Count() ?? 0;
            string title = $"Player Sites [{count}]";

            Text.Font = GameFont.Medium;
            float titleH = Text.CalcHeight(title, outer.width);
            Rect titleRect = new Rect(outer.x, outer.y, outer.width, titleH);
            Widgets.Label(titleRect, title);

            float lineY = titleRect.yMax + 4f;
            Widgets.DrawLineHorizontal(outer.x, lineY, outer.width);

            Rect outRect = new Rect(outer.x, lineY + 6f, outer.width, outer.yMax - (lineY + 6f));
            DrawList(outRect);
        }

        private void DrawList(Rect mainRect)
        {
            RTSite[] sites = (SiteManager.PlayerSites ?? Enumerable.Empty<RTSite>())
                .OrderBy(s => s?.Label ?? string.Empty)
                .ToArray();

            const float rowH = 30f;
            float height = 6f + sites.Length * rowH;

            Rect viewRect = new Rect(0f, 0f, mainRect.width - 16f, height);

            Widgets.BeginScrollView(mainRect, ref _scroll, viewRect);
            try
            {
                float y = 0f;

                float yMin = _scroll.y - rowH;
                float yMax = _scroll.y + mainRect.height;

                for (int i = 0; i < sites.Length; i++)
                {
                    if (y > yMin && y < yMax)
                    {
                        Rect row = new Rect(0f, y, viewRect.width, rowH);
                        DrawRow(row, sites[i], i);
                    }
                    y += rowH;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private static void DrawRow(Rect row, RTSite site, int index)
        {
            Text.Font = GameFont.Small;

            if (index % 2 == 0) Widgets.DrawLightHighlight(row);
            Widgets.DrawHighlightIfMouseover(row);

            string label = site?.Label ?? "Unknown";
            int tile = site?.Tile ?? -1;

            Rect labelRect = new Rect(row.x + 10f, row.y + 5f, row.width - 62f, row.height - 5f);
            Widgets.Label(labelRect, $"{label} - {tile}");

            Rect btnRect = new Rect(row.xMax - 52f, row.y, 52f, row.height);
            if (Widgets.ButtonText(btnRect, "Focus"))
            {
                if (tile >= 0)
                {
                    foreach (Site wSite in Find.World.worldObjects.Sites)
                    {
                        if (wSite.Tile == tile)
                        {
                            CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(wSite));
                            break;
                        }
                    }
                }
            }
        }
    }
}