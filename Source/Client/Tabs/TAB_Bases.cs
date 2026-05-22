using System.Linq;
using GameClient.Misc;
using GameClient.PacketManagers;
using GameClient.WorldObjects;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using static Shared.CommonEnumerators;

namespace GameClient.Tabs
{
    public class TAB_Bases : WITab
    {
        private Vector2 scrollPosition;

        private static readonly Vector2 WinSize = new Vector2(432f, 540f);

        private string tabTitle;

        public override bool IsVisible => true;

        protected override bool StillValid => true;

        public TAB_Bases()
        {
            size = WinSize;
            labelKey = "Bases";
        }

        protected override void FillTab()
        {
            tabTitle = $"Player Bases [{PM_Settlements.PlayerSettlements.Count}]";

            float horizontalLineDif = Text.CalcSize(tabTitle).y + 3f + 10f;

            Rect outRect = new Rect(0f, 0f, WinSize.x, WinSize.y).ContractedBy(10f);
            Rect rect = new Rect(10f, 10f, outRect.width - 16f, Mathf.Max(0f, outRect.height));

            Text.Font = GameFont.Medium;
            Widgets.Label(rect, tabTitle);
            Widgets.DrawLineHorizontal(rect.x, horizontalLineDif, rect.width);
            GenerateList(new Rect(new Vector2(rect.x, rect.y + 30f), new Vector2(rect.width, rect.height - 30f)));
        }

        private void GenerateList(Rect mainRect)
        {
            // Materialise once — OrderBy was previously enumerated for .Count() AND the foreach.
            var ordered = PM_Settlements.PlayerSettlements.OrderBy(x => x.Name).ToList();

            float height = 6f + ordered.Count * 30f;
            Rect viewRect = new Rect(mainRect.x, mainRect.y, mainRect.width - 16f, height);

            Widgets.BeginScrollView(mainRect, ref scrollPosition, viewRect);

            float num = 0;
            float num2 = scrollPosition.y - 30f;
            float num3 = scrollPosition.y + mainRect.height;
            int num4 = 0;

            foreach (WO_Settlement playerSettlement in ordered)
            {
                if (num > num2 && num < num3)
                {
                    Rect rect = new Rect(0f, mainRect.y + num, viewRect.width, 30f);
                    DrawCustomRow(rect, playerSettlement, num4);
                }

                num += 30f;
                num4++;
            }

            Widgets.EndScrollView();
        }

        private void DrawCustomRow(Rect rect, WO_Settlement playerSettlement, int index)
        {
            Text.Font = GameFont.Small;

            if (index % 2 == 0) Widgets.DrawLightHighlight(rect);
            Rect fixedRect = new Rect(new Vector2(rect.x + 10f, rect.y + 5f), new Vector2(rect.width - 52f, rect.height));

            const float buttonW = 47f;
            const float buttonH = 30f;
            const float smallW = 30f;

            Widgets.Label(fixedRect, $"{playerSettlement.Name} - {playerSettlement.Tile}");

            // Each button used to re-scan PlayerSettlements to "find" the same reference;
            // since playerSettlement IS the entry in that list, we can act on it directly.
            if (Widgets.ButtonText(new Rect(new Vector2(rect.xMax - buttonW, rect.y), new Vector2(buttonW, buttonH)), "Focus"))
            {
                CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(playerSettlement));
            }

            if (Widgets.ButtonText(new Rect(new Vector2(rect.xMax - smallW * 3, rect.y), new Vector2(smallW, buttonH)), "-"))
            {
                SessionHandler.ChosenSettlement = playerSettlement;
                PM_Goodwills.TryRequestGoodwill(Goodwill.Enemy, GoodwillTarget.Settlement);
            }

            if (Widgets.ButtonText(new Rect(new Vector2(rect.xMax - smallW * 4, rect.y), new Vector2(smallW, buttonH)), "="))
            {
                SessionHandler.ChosenSettlement = playerSettlement;
                PM_Goodwills.TryRequestGoodwill(Goodwill.Neutral, GoodwillTarget.Settlement);
            }

            if (Widgets.ButtonText(new Rect(new Vector2(rect.xMax - smallW * 5, rect.y), new Vector2(smallW, buttonH)), "+"))
            {
                SessionHandler.ChosenSettlement = playerSettlement;
                PM_Goodwills.TryRequestGoodwill(Goodwill.Ally, GoodwillTarget.Settlement);
            }
        }

        protected override void CloseTab()
        {
            throw new System.NotImplementedException();
        }
    }
}
