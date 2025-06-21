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
        private static readonly Vector2 WinSize    = new Vector2(432f, 540f);
        private const float          Pad        = 6f;
        private const float          ScrollbarW = 16f;

        public override bool IsVisible => true;

        public SitesUI()
        {
            size     = WinSize;
            labelKey = "Sites";
        }

        protected override void FillTab()
        {
            if (Network.State != ClientNetworkState.Connected) return;

            // parse colors
            var settings = ChatCustomizationManager.Settings;
            bool hasBg = ColorUtility.TryParseHtmlString(settings.BackgroundColor, out var bgCol) && bgCol.a >= 0.1f;
            bool hasFg = ColorUtility.TryParseHtmlString(settings.FontColor,       out var fgCol) && fgCol.a >= 0.1f;

            // draw background only if valid
            if (hasBg)
                Widgets.DrawBoxSolid(new Rect(0, 0, WinSize.x, WinSize.y), bgCol);

            // font size
            Text.Font = settings.FontSize switch
            {
                "Tiny"   => GameFont.Tiny,
                "Medium" => GameFont.Medium,
                _        => GameFont.Small,
            };

            // title
            string title = $"Player Sites [{SiteManager.PlayerSites.Count()}]";
            float  titleH = Text.CalcSize(title).y;
            GUI.color    = hasFg ? fgCol : Color.white;
            Widgets.Label(new Rect(Pad, Pad, WinSize.x - 2*Pad, titleH), title);
            GUI.color = Color.white;

            Widgets.DrawLineHorizontal(Pad, Pad + titleH + 3f, WinSize.x - 2*Pad);

            var listRect = new Rect(
                Pad,
                Pad + titleH + 10f,
                WinSize.x - 2*Pad,
                WinSize.y - (titleH + 2*Pad + 10f)
            );
            DrawList(listRect, hasFg ? fgCol : Color.white);
        }

        private void DrawList(Rect mainRect, Color labelCol)
        {
            var rows       = SiteManager.PlayerSites.OrderBy(s => s.Label).ToList();
            const float rowH = 30f;
            float contentH = rows.Count * rowH + Pad;
            float contentW = mainRect.width - ScrollbarW - Pad;

            Widgets.BeginScrollView(
                mainRect,
                ref _scroll,
                new Rect(0, 0, contentW, contentH)
            );

            float y = 0f;
            foreach (var site in rows)
            {
                // zebra
                if (((int)(y / rowH) & 1) == 0)
                    Widgets.DrawLightHighlight(new Rect(0, y, contentW, rowH));

                Text.Font = GameFont.Small;
                GUI.color  = labelCol;
                Widgets.Label(
                    new Rect(Pad, y + 5f, contentW - 50f - Pad, rowH),
                    $"{site.Label} - {site.Tile}"
                );
                GUI.color = Color.white;

                const float btnW = 47f;
                if (Widgets.ButtonText(
                    new Rect(contentW - btnW, y, btnW, rowH),
                    "Focus"
                ))
                {
                    var world = Find.World.worldObjects.Sites
                                   .FirstOrDefault(s => s.Tile == site.Tile);
                    if (world != null)
                        CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(world));
                }

                y += rowH;
            }

            Widgets.EndScrollView();
        }
    }
}