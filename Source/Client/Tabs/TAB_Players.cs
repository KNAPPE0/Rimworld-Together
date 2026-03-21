using System;
using System.Collections.Generic;
using System.Linq;
using GameClient.Managers;
using UnityEngine;
using Verse;
using RimWorld.Planet;
using GameClient.PacketManagers;

namespace GameClient.Tabs
{
    public class TAB_Players : WITab
    {
        private Vector2 _scroll = Vector2.zero;

        private static readonly Vector2 WinSize = new Vector2(432f, 540f);

        public override bool IsVisible => true;
        protected override bool StillValid => true;

        private const float Pad = 10f;
        private const float RowH = 28f;

        public TAB_Players()
        {
            size = WinSize;
            labelKey = "Players";
        }

        protected override void FillTab()
        {
            Rect outer = new Rect(0f, 0f, WinSize.x, WinSize.y).ContractedBy(Pad);

            string title = $"Players Online [{PM_Recount.CurrentPlayers}]";

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
            List<string> players = PM_Recount.CurrentPlayerNames?.ToList() ?? new List<string>();
            players.Sort(StringComparer.OrdinalIgnoreCase);

            float viewH = Mathf.Max(mainRect.height, 6f + players.Count * RowH);
            Rect viewRect = new Rect(0f, 0f, mainRect.width - 16f, viewH);

            Widgets.BeginScrollView(mainRect, ref _scroll, viewRect);
            try
            {
                float y = 0f;
                float yMin = _scroll.y - RowH;
                float yMax = _scroll.y + mainRect.height + RowH;

                for (int i = 0; i < players.Count; i++)
                {
                    if (y > yMin && y < yMax)
                    {
                        Rect row = new Rect(0f, y, viewRect.width, RowH);

                        if (i % 2 == 0)
                            Widgets.DrawAltRect(row);

                        Widgets.DrawHighlightIfMouseover(row);

                        Rect labelRect = row.ContractedBy(8f, 4f);

                        Text.Font = GameFont.Small;
                        Text.Anchor = TextAnchor.MiddleLeft;
                        Widgets.LabelEllipses(labelRect, players[i] ?? "Unknown");
                        Text.Anchor = TextAnchor.UpperLeft;
                    }

                    y += RowH;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }
    }
}