using System.Collections.Generic;
using System.Linq;
using GameClient.Managers;
using GameClient.TCP;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using static Shared.CommonEnumerators;

namespace GameClient.Patches.Tabs
{
    public class PlayersUI : WITab
    {
        private Vector2 _scroll;
        private static readonly Vector2 WinSize = new(432f, 540f);

        public override bool IsVisible => true;

        public PlayersUI()
        {
            size     = WinSize;
            labelKey = "Players";
        }

        protected override void FillTab()
        {
            if (Network.State != ClientNetworkState.Connected) return;

            int oldSize            = GUI.skin.label.fontSize;
            GUI.skin.label.fontSize = Mathf.RoundToInt(ChatCustomizationManager.FontSize);

            var full = new Rect(0f, 0f, WinSize.x, WinSize.y);
            Widgets.DrawBoxSolid(full, ChatCustomizationManager.BackgroundColor);

            string title       = $"Players Online [{RecountManager.CurrentPlayers}]";
            float  titleHeight = Text.CalcSize(title).y;

            Text.Font = GameFont.Medium;
            GUI.color = ChatCustomizationManager.FontColor;
            Widgets.Label(new Rect(10f, 10f, full.width - 20f, titleHeight), title);
            GUI.color = Color.white;

            Widgets.DrawLineHorizontal(10f, 10f + titleHeight + 3f, full.width - 20f);

            Rect listRect = new Rect(10f, 10f + titleHeight + 10f,
                                      full.width - 20f,
                                      full.height - (titleHeight + 20f));
            DrawList(listRect);

            GUI.skin.label.fontSize = oldSize;
        }

        private void DrawList(Rect mainRect)
        {
            List<string> list = RecountManager.CurrentPlayerNames.OrderBy(n => n).ToList();
            float rowH = 30f;
            float cont = 6f + list.Count * rowH;

            Widgets.BeginScrollView(mainRect, ref _scroll,
                                    new Rect(0, 0, mainRect.width - 16f, cont));

            float y = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                if (i % 2 == 0)
                    Widgets.DrawLightHighlight(new Rect(0, y, mainRect.width - 16f, rowH));

                Text.Font = GameFont.Small;
                GUI.color = ChatCustomizationManager.FontColor;
                Widgets.Label(new Rect(10f, y + 5f, mainRect.width - 36f, rowH), list[i]);
                GUI.color = Color.white;

                y += rowH;
            }

            Widgets.EndScrollView();
        }
    }
}