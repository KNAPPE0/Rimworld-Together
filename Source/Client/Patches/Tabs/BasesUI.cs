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
        private static readonly Vector2 WinSize = new(432f, 540f);

        public override bool IsVisible => true;

        public BasesUI()
        {
            size     = WinSize;
            labelKey = "Bases";
        }

        protected override void FillTab()
        {
            if (Network.State != ClientNetworkState.Connected) return;

            // ─── styling ─────────────────────────────────────────────
            int oldSize            = GUI.skin.label.fontSize;
            GUI.skin.label.fontSize = Mathf.RoundToInt(ChatCustomizationManager.FontSize);

            var full = new Rect(0f, 0f, WinSize.x, WinSize.y);
            Widgets.DrawBoxSolid(full, ChatCustomizationManager.BackgroundColor);

            string title          = $"Player Bases [{SettlementManager.PlayerSettlements.Count()}]";
            float  titleHeight    = Text.CalcSize(title).y;

            Rect titleRect = new Rect(10f, 10f, full.width - 20f, titleHeight);
            Text.Font = GameFont.Medium;
            GUI.color = ChatCustomizationManager.FontColor;
            Widgets.Label(titleRect, title);
            GUI.color = Color.white;

            Widgets.DrawLineHorizontal(titleRect.x, titleRect.yMax + 3f, titleRect.width);

            // list
            Rect listRect = new Rect(titleRect.x,
                                      titleRect.yMax + 10f,
                                      titleRect.width,
                                      full.height - (titleRect.yMax + 12f));
            DrawList(listRect);

            // restore
            GUI.skin.label.fontSize = oldSize;
        }

        private void DrawList(Rect mainRect)
        {
            var rows   = SettlementManager.PlayerSettlements.OrderBy(s => s.Name).ToList();
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

        private void DrawRow(Rect rect, Settlement stl)
        {
            Text.Font = GameFont.Small;
            GUI.color = ChatCustomizationManager.FontColor;

            Widgets.Label(new Rect(rect.x + 10f, rect.y + 5f,
                                   rect.width - 150f, rect.height),
                          $"{stl.Name} - {stl.Tile}");

            GUI.color = Color.white;

            // focus button (right-most 47×30)
            float btnW = 47f;
            if (Widgets.ButtonText(new Rect(rect.xMax - btnW, rect.y, btnW, 30f), "Focus"))
            {
                var worldStl = Find.World.worldObjects.Settlements
                                   .FirstOrDefault(s => s.Tile == stl.Tile);
                if (worldStl != null)
                    CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(worldStl));
            }

            // goodwill buttons  (- = enemy, = neutral, + ally)
            btnW = 30f;
            if (Widgets.ButtonText(new Rect(rect.xMax - btnW * 3, rect.y, btnW, 30f), "-"))
                RequestGoodwill(stl, Goodwill.Enemy);
            if (Widgets.ButtonText(new Rect(rect.xMax - btnW * 4, rect.y, btnW, 30f), "="))
                RequestGoodwill(stl, Goodwill.Neutral);
            if (Widgets.ButtonText(new Rect(rect.xMax - btnW * 5, rect.y, btnW, 30f), "+"))
                RequestGoodwill(stl, Goodwill.Ally);
        }

        private static void RequestGoodwill(Settlement stl, Goodwill g)
        {
            var worldStl = Find.World.worldObjects.Settlements
                               .FirstOrDefault(s => s.Tile == stl.Tile);
            if (worldStl == null) return;

            SessionValues.ChosenSettlement = worldStl;
            GoodwillManager.TryRequestGoodwill(g, GoodwillTarget.Settlement);
        }
    }
}