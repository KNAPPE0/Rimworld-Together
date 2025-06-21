using System.Linq;
using GameClient.Managers;
using GameClient.TCP;
using GameClient.Values;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using static Shared.CommonEnumerators;

namespace GameClient.Patches.Tabs
{
    public class BasesUI : WITab
    {
        private Vector2 _scroll;
        private static readonly Vector2 WinSize    = new Vector2(432f, 540f);
        private const float          Pad        = 6f;
        private const float          ScrollbarW = 16f;

        public override bool IsVisible => true;

        public BasesUI()
        {
            size     = WinSize;
            labelKey = "Bases";
        }

        protected override void FillTab()
        {
            if (Network.State != ClientNetworkState.Connected) return;

            // Parse colors
            var settings = ChatCustomizationManager.Settings;
            bool hasBg = ColorUtility.TryParseHtmlString(settings.BackgroundColor, out var bgCol) && bgCol.a >= 0.1f;
            bool hasFg = ColorUtility.TryParseHtmlString(settings.FontColor,       out var fgCol) && fgCol.a >= 0.1f;

            // Draw background only if valid/not blank
            if (hasBg)
                Widgets.DrawBoxSolid(new Rect(0, 0, WinSize.x, WinSize.y), bgCol);

            // Choose font size
            Text.Font = settings.FontSize switch
            {
                "Tiny"   => GameFont.Tiny,
                "Medium" => GameFont.Medium,
                _        => GameFont.Small,
            };

            // Draw title
            string title = $"Player Bases [{SettlementManager.PlayerSettlements.Count()}]";
            float titleH = Text.CalcSize(title).y;
            GUI.color   = hasFg ? fgCol : Color.white;
            Widgets.Label(new Rect(Pad, Pad, WinSize.x - 2*Pad, titleH), title);
            GUI.color = Color.white;

            Widgets.DrawLineHorizontal(Pad, Pad + titleH + 3f, WinSize.x - 2*Pad);

            // List area
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
            var rows       = SettlementManager.PlayerSettlements.OrderBy(s => s.Name).ToList();
            const float rowH = 30f;
            float contentH = rows.Count * rowH + Pad;
            float contentW = mainRect.width - ScrollbarW - Pad;

            Widgets.BeginScrollView(
                mainRect,
                ref _scroll,
                new Rect(0, 0, contentW, contentH)
            );

            float y = 0f;
            foreach (var stl in rows)
            {
                // Zebra
                if (((int)(y / rowH) & 1) == 0)
                    Widgets.DrawLightHighlight(new Rect(0, y, contentW, rowH));

                // Label
                Text.Font = GameFont.Small;
                GUI.color  = labelCol;
                Widgets.Label(
                    new Rect(Pad, y + 5f, contentW - 50f - Pad, rowH),
                    $"{stl.Name} - {stl.Tile}"
                );
                GUI.color = Color.white;

                // Focus button
                const float btnW = 47f;
                if (Widgets.ButtonText(
                    new Rect(contentW - btnW, y, btnW, rowH),
                    "Focus"
                ))
                {
                    var worldStl = Find.World.worldObjects.Settlements
                                     .FirstOrDefault(s => s.Tile == stl.Tile);
                    if (worldStl != null)
                        CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(worldStl));
                }

                y += rowH;
            }

            Widgets.EndScrollView();
        }
    }
}