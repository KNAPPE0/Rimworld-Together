using System.Linq;
using GameClient.Managers;
using GameClient.TCP;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using static Shared.CommonEnumerators;

namespace GameClient.Patches.Tabs
{
    public class SitesUI : WITab
    {
        private Vector2 _scroll;
        private static readonly Vector2 WinSize = new(432f, 540f);

        public override bool IsVisible => true;

        public SitesUI()
        {
            size     = WinSize;
            labelKey = "Sites";
        }

        protected override void FillTab()
        {
            if (Network.State != ClientNetworkState.Connected) return;

            int oldSize            = GUI.skin.label.fontSize;
            GUI.skin.label.fontSize = Mathf.RoundToInt(ChatCustomizationManager.FontSize);

            var full = new Rect(0f, 0f, WinSize.x, WinSize.y);
            Widgets.DrawBoxSolid(full, ChatCustomizationManager.BackgroundColor);

            string title       = $"Player Sites [{SiteManager.PlayerSites.Count()}]";
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
            var rows  = SiteManager.PlayerSites.OrderBy(s => s.Label).ToList();
            float rowH = 30f;
            float cont = 6f + rows.Count * rowH;

            Widgets.BeginScrollView(mainRect, ref _scroll,
                                    new Rect(0, 0, mainRect.width - 16f, cont));

            float y = 0f;
            for (int i = 0; i < rows.Count; i++)
            {
                if (i % 2 == 0)
                    Widgets.DrawLightHighlight(new Rect(0, y, mainRect.width - 16f, rowH));

                DrawRow(new Rect(0, y, mainRect.width - 16f, rowH), rows[i]);
                y += rowH;
            }

            Widgets.EndScrollView();
        }

        private void DrawRow(Rect rect, Site site)
        {
            Text.Font = GameFont.Small;
            GUI.color = ChatCustomizationManager.FontColor;
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 5f,
                                   rect.width - 100f, rect.height),
                          $"{site.Label} - {site.Tile}");
            GUI.color = Color.white;

            float btnW = 47f;
            if (Widgets.ButtonText(new Rect(rect.xMax - btnW, rect.y, btnW, 30f), "Focus"))
            {
                var worldSite = Find.World.worldObjects.Sites
                                   .FirstOrDefault(s => s.Tile == site.Tile);
                if (worldSite != null)
                    CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(worldSite));
            }
        }
    }
}