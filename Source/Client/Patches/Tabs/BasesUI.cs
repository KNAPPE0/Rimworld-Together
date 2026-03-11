using System;
using System.Linq;
using GameClient.Managers;
using GameClient.PacketManagers;
using GameClient.WorldObjects;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace GameClient.Patches.Tabs
{
    public class BasesUI : WITab
    {
        private Vector2 _scroll = Vector2.zero;

        private static readonly Vector2 WinSize = new Vector2(432f, 540f);

        public override bool IsVisible => true;
        protected override bool StillValid => true;

        private const float Pad = 10f;
        private const float RowH = 30f;

        public BasesUI()
        {
            size = WinSize;
            labelKey = "Bases";
        }

        protected override void FillTab()
        {
            Rect outer = new Rect(0f, 0f, WinSize.x, WinSize.y).ContractedBy(Pad);

            int count = SettlementManager.PlayerSettlements?.Count() ?? 0;
            string title = $"Player Bases [{count}]";

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
            RTSettlement[] bases = (SettlementManager.PlayerSettlements ?? Enumerable.Empty<RTSettlement>())
                .Where(s => s != null)
                .OrderBy(s => s.Name ?? string.Empty)
                .ToArray();

            float viewH = Mathf.Max(mainRect.height, 6f + bases.Length * RowH);
            Rect viewRect = new Rect(0f, 0f, mainRect.width - 16f, viewH);

            Widgets.BeginScrollView(mainRect, ref _scroll, viewRect);
            try
            {
                float y = 0f;
                float yMin = _scroll.y - RowH;
                float yMax = _scroll.y + mainRect.height + RowH;

                for (int i = 0; i < bases.Length; i++)
                {
                    if (y > yMin && y < yMax)
                    {
                        Rect row = new Rect(0f, y, viewRect.width, RowH);

                        if (i % 2 == 0)
                            Widgets.DrawAltRect(row);

                        Widgets.DrawHighlightIfMouseover(row);

                        DrawRow(row, bases[i]);
                    }

                    y += RowH;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private static RTSettlement FindWorldSettlementAtTile(int tile)
        {
            if (tile < 0 || Find.World == null) return null;

            foreach (WorldObject obj in Find.World.worldObjects.AllWorldObjects)
            {
                if (obj is RTSettlement s && s.Tile == tile)
                    return s;
            }

            return null;
        }

        private static void DrawRow(Rect row, RTSettlement listed)
        {
            Text.Font = GameFont.Small;

            string name = listed?.Name ?? "Unknown";
            int tile = listed?.Tile ?? -1;

            float y = row.y + 2f;
            float h = row.height - 4f;

            float focusW = 56f;
            float gwW = 30f;
            float gap = 2f;

            float right = row.xMax - 2f;

            Rect focusRect = new Rect(right - focusW, y, focusW, h);
            right -= focusW + gap;

            Rect minusRect = new Rect(right - gwW, y, gwW, h);
            right -= gwW + gap;

            Rect equalRect = new Rect(right - gwW, y, gwW, h);
            right -= gwW + gap;

            Rect plusRect = new Rect(right - gwW, y, gwW, h);
            right -= gwW + 6f;

            Rect labelRect = new Rect(row.x + 8f, row.y + 4f, right - (row.x + 8f), row.height - 8f);
            Widgets.LabelEllipses(labelRect, $"{name}  |  Tile {tile}");

            if (Widgets.ButtonText(focusRect, "Focus"))
            {
                RTSettlement world = FindWorldSettlementAtTile(tile);
                if (world != null)
                    CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(world));
            }

            if (Widgets.ButtonText(minusRect, "-"))
            {
                RTSettlement world = FindWorldSettlementAtTile(tile);
                if (world != null)
                {
                    SessionHandler.ChosenSettlement = world;
                    PM_Goodwills.TryRequestGoodwill(CommonEnumerators.Goodwill.Enemy, CommonEnumerators.GoodwillTarget.Settlement);
                }
            }

            if (Widgets.ButtonText(equalRect, "="))
            {
                RTSettlement world = FindWorldSettlementAtTile(tile);
                if (world != null)
                {
                    SessionHandler.ChosenSettlement = world;
                    PM_Goodwills.TryRequestGoodwill(CommonEnumerators.Goodwill.Neutral, CommonEnumerators.GoodwillTarget.Settlement);
                }
            }

            if (Widgets.ButtonText(plusRect, "+"))
            {
                RTSettlement world = FindWorldSettlementAtTile(tile);
                if (world != null)
                {
                    SessionHandler.ChosenSettlement = world;
                    PM_Goodwills.TryRequestGoodwill(CommonEnumerators.Goodwill.Ally, CommonEnumerators.GoodwillTarget.Settlement);
                }
            }

            string tip =
                $"Base: {name}\n" +
                $"Tile: {tile}\n" +
                $"Actions:\n" +
                $"- Focus\n" +
                $"- Set goodwill: - = Enemy, = = Neutral, + = Ally";

            TooltipHandler.TipRegion(row, tip);
        }
    }
}