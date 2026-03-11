using System.Linq;
using GameClient.Managers;
using GameClient.WorldObjects;
using RimWorld;
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

        private const float Pad = 10f;
        private const float RowH = 30f;

        public SitesUI()
        {
            size = WinSize;
            labelKey = "Sites";
        }

        protected override void FillTab()
        {
            Rect outer = new Rect(0f, 0f, WinSize.x, WinSize.y).ContractedBy(Pad);

            int count = SiteManager.PlayerSites?.Count() ?? 0;
            string title = $"Player Sites [{count}]";

            Text.Font = GameFont.Medium;
            Rect titleRect = new Rect(outer.x, outer.y, outer.width, 28f);
            Widgets.Label(titleRect, title);

            Text.Font = GameFont.Small;
            Widgets.DrawLineHorizontal(outer.x, titleRect.yMax + 4f, outer.width);

            Rect listOuter = new Rect(outer.x, titleRect.yMax + 10f, outer.width, outer.yMax - (titleRect.yMax + 10f));
            Widgets.DrawMenuSection(listOuter);

            Rect listInner = listOuter.ContractedBy(6f);
            DrawList(listInner);
        }

        private void DrawList(Rect mainRect)
        {
            RTSite[] sites = (SiteManager.PlayerSites ?? Enumerable.Empty<RTSite>())
                .Where(s => s != null)
                .OrderBy(s => s.Label ?? string.Empty)
                .ToArray();

            float viewH = Mathf.Max(mainRect.height, 6f + sites.Length * RowH);
            Rect viewRect = new Rect(0f, 0f, mainRect.width - 16f, viewH);

            Widgets.BeginScrollView(mainRect, ref _scroll, viewRect);
            try
            {
                float y = 0f;
                float yMin = _scroll.y - RowH;
                float yMax = _scroll.y + mainRect.height + RowH;

                for (int i = 0; i < sites.Length; i++)
                {
                    if (y > yMin && y < yMax)
                    {
                        Rect row = new Rect(0f, y, viewRect.width, RowH);

                        if (i % 2 == 0)
                            Widgets.DrawAltRect(row);

                        Widgets.DrawHighlightIfMouseover(row);

                        DrawRow(row, sites[i]);
                    }

                    y += RowH;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private static void DrawRow(Rect row, RTSite site)
        {
            Text.Font = GameFont.Small;

            string label = site?.Label ?? "Unknown";
            int tile = site?.Tile ?? -1;

            Rect labelRect = new Rect(row.x + 8f, row.y + 4f, row.width - 66f, row.height - 8f);
            Widgets.LabelEllipses(labelRect, $"{label}  |  Tile {tile}");

            Rect btnRect = new Rect(row.xMax - 58f, row.y + 2f, 56f, row.height - 4f);

            if (Widgets.ButtonText(btnRect, "Focus"))
            {
                WorldObject target = FindWorldSiteAtTile(tile);
                if (target != null)
                    CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(target));
            }

            TooltipHandler.TipRegion(row, $"Site: {label}\nTile: {tile}\nAction: Focus");
        }

        private static WorldObject FindWorldSiteAtTile(int tile)
        {
            if (tile < 0 || Find.World == null) return null;

            foreach (WorldObject obj in Find.World.worldObjects.AllWorldObjects)
            {
                if (obj == null) continue;
                if (obj.Tile != tile) continue;

                if (obj is RTSite || obj is Site)
                    return obj;
            }

            foreach (Site s in Find.World.worldObjects.Sites)
            {
                if (s != null && s.Tile == tile)
                    return s;
            }

            return null;
        }
    }
}