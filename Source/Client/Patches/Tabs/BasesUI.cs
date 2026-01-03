using System.Linq;
using GameClient.Managers;
using GameClient.Misc;
using GameClient.WorldObjects;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using static Shared.CommonEnumerators;

namespace GameClient.Patches.Tabs
{
    public class BasesUI : WITab
    {
        private Vector2 _scroll = Vector2.zero;

        private static readonly Vector2 WinSize = new Vector2(432f, 540f);

        public override bool IsVisible => true;
        protected override bool StillValid => true;

        public BasesUI()
        {
            size = WinSize;
            labelKey = "Bases";
        }

        protected override void FillTab()
        {
            Rect outer = new Rect(0f, 0f, WinSize.x, WinSize.y).ContractedBy(10f);

            int count = SettlementManager.PlayerSettlements?.Count() ?? 0;
            string title = $"Player Bases [{count}]";

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
            RTSettlement[] bases = (SettlementManager.PlayerSettlements ?? Enumerable.Empty<RTSettlement>())
                .OrderBy(s => s?.Name ?? string.Empty)
                .ToArray();

            const float rowH = 30f;
            float height = 6f + bases.Length * rowH;

            Rect viewRect = new Rect(0f, 0f, mainRect.width - 16f, height);

            Widgets.BeginScrollView(mainRect, ref _scroll, viewRect);
            try
            {
                float y = 0f;
                float yMin = _scroll.y - rowH;
                float yMax = _scroll.y + mainRect.height;

                for (int i = 0; i < bases.Length; i++)
                {
                    if (y > yMin && y < yMax)
                    {
                        Rect row = new Rect(0f, y, viewRect.width, rowH);
                        DrawRow(row, bases[i], i);
                    }
                    y += rowH;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private static RTSettlement FindWorldSettlementAtTile(int tile)
        {
            foreach (WorldObject obj in Find.World.worldObjects.AllWorldObjects)
            {
                if (obj is RTSettlement s && s.Tile == tile)
                    return s;
            }
            return null;
        }

        private static void DrawRow(Rect row, RTSettlement listed, int index)
        {
            Text.Font = GameFont.Small;

            if (index % 2 == 0) Widgets.DrawLightHighlight(row);
            Widgets.DrawHighlightIfMouseover(row);

            string name = listed?.Name ?? "Unknown";
            int tile = listed?.Tile ?? -1;

            Rect labelRect = new Rect(row.x + 10f, row.y + 5f, row.width - 190f, row.height - 5f);
            Widgets.Label(labelRect, $"{name} - {tile}");

            float btnH = row.height;
            float focusW = 52f;
            float gwW = 30f;

            Rect focusRect = new Rect(row.xMax - focusW, row.y, focusW, btnH);
            Rect minusRect = new Rect(row.xMax - focusW - (gwW * 1f), row.y, gwW, btnH);
            Rect equalRect = new Rect(row.xMax - focusW - (gwW * 2f), row.y, gwW, btnH);
            Rect plusRect = new Rect(row.xMax - focusW - (gwW * 3f), row.y, gwW, btnH);

            if (Widgets.ButtonText(focusRect, "Focus"))
            {
                RTSettlement world = FindWorldSettlementAtTile(tile);
                if (world != null) CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(world));
            }

            if (Widgets.ButtonText(minusRect, "-"))
            {
                RTSettlement world = FindWorldSettlementAtTile(tile);
                if (world != null)
                {
                    SessionHandler.ChosenSettlement = world;
                    GoodwillManager.TryRequestGoodwill(Goodwill.Enemy, GoodwillTarget.Settlement);
                }
            }

            if (Widgets.ButtonText(equalRect, "="))
            {
                RTSettlement world = FindWorldSettlementAtTile(tile);
                if (world != null)
                {
                    SessionHandler.ChosenSettlement = world;
                    GoodwillManager.TryRequestGoodwill(Goodwill.Neutral, GoodwillTarget.Settlement);
                }
            }

            if (Widgets.ButtonText(plusRect, "+"))
            {
                RTSettlement world = FindWorldSettlementAtTile(tile);
                if (world != null)
                {
                    SessionHandler.ChosenSettlement = world;
                    GoodwillManager.TryRequestGoodwill(Goodwill.Ally, GoodwillTarget.Settlement);
                }
            }
        }
    }
}