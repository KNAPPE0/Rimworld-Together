using System.Collections.Generic;
using System.Linq;
using GameClient.Managers;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace GameClient.Patches.Tabs
{
    public class PlayersUI : WITab
    {
        private Vector2 _scroll = Vector2.zero;

        private static readonly Vector2 WinSize = new Vector2(432f, 540f);

        public override bool IsVisible => true;
        protected override bool StillValid => true;

        public PlayersUI()
        {
            size = WinSize;
            labelKey = "Players";
        }

        protected override void FillTab()
        {
            Rect outer = new Rect(0f, 0f, WinSize.x, WinSize.y).ContractedBy(10f);

            string title = $"Players Online [{RecountManager.CurrentPlayers}]";

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
            List<string> players = RecountManager.CurrentPlayerNames?.ToList() ?? new List<string>();
            players.Sort();

            const float rowH = 30f;
            float height = 6f + players.Count * rowH;

            Rect viewRect = new Rect(0f, 0f, mainRect.width - 16f, height);

            Widgets.BeginScrollView(mainRect, ref _scroll, viewRect);
            try
            {
                float y = 0f;
                float yMin = _scroll.y - rowH;
                float yMax = _scroll.y + mainRect.height;

                for (int i = 0; i < players.Count; i++)
                {
                    if (y > yMin && y < yMax)
                    {
                        Rect row = new Rect(0f, y, viewRect.width, rowH);
                        DrawRow(row, players[i], i);
                    }
                    y += rowH;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private static void DrawRow(Rect row, string name, int index)
        {
            Text.Font = GameFont.Small;

            if (index % 2 == 0) Widgets.DrawLightHighlight(row);
            Widgets.DrawHighlightIfMouseover(row);

            Rect labelRect = new Rect(row.x + 10f, row.y + 5f, row.width - 10f, row.height - 5f);
            Widgets.Label(labelRect, name ?? "Unknown");
        }
    }
}