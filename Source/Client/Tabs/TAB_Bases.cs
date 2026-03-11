using System.Linq;
using GameClient.Managers;
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
            tabTitle = $"Player Bases [{PM_Settlements.PlayerSettlements.Count()}]";

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
            var orderedDictionary = PM_Settlements.PlayerSettlements.OrderBy(x => x.Name);

            float height = 6f + orderedDictionary.Count() * 30f;
            Rect viewRect = new Rect(mainRect.x, mainRect.y, mainRect.width - 16f, height);

            Widgets.BeginScrollView(mainRect, ref scrollPosition, viewRect);

            float num = 0;
            float num2 = scrollPosition.y - 30f;
            float num3 = scrollPosition.y + mainRect.height;
            int num4 = 0;

            foreach (WO_Settlement playerSettlement in orderedDictionary)
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

            float buttonX = 47f;
            float buttonY = 30f;
            Widgets.Label(fixedRect, $"{playerSettlement.Name} - {playerSettlement.Tile}");
            if (Widgets.ButtonText(new Rect(new Vector2(rect.xMax - buttonX, rect.y), new Vector2(buttonX, buttonY)), "Focus"))
            {
                foreach (WO_Settlement settlement in PM_Settlements.PlayerSettlements)
                {
                    if (settlement.Tile == playerSettlement.Tile)
                    {
                        CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(settlement));
                        break;
                    }
                }
            }

            buttonX = 30f;
            if (Widgets.ButtonText(new Rect(new Vector2(rect.xMax - buttonX * 3, rect.y), new Vector2(buttonX, buttonY)), "-"))
            {
                foreach (WO_Settlement settlement in PM_Settlements.PlayerSettlements)
                {
                    if (settlement.Tile == playerSettlement.Tile)
                    {
                        SessionHandler.ChosenSettlement = settlement;

                        PM_Goodwills.TryRequestGoodwill(Goodwill.Enemy,
                            GoodwillTarget.Settlement);

                        break;
                    }
                }
            }

            if (Widgets.ButtonText(new Rect(new Vector2(rect.xMax - buttonX * 4, rect.y), new Vector2(buttonX, buttonY)), "="))
            {
                foreach (WO_Settlement settlement in PM_Settlements.PlayerSettlements)
                {
                    if (settlement.Tile == playerSettlement.Tile)
                    {
                        SessionHandler.ChosenSettlement = settlement;

                        PM_Goodwills.TryRequestGoodwill(Goodwill.Neutral,
                            GoodwillTarget.Settlement);

                        break;
                    }
                }
            }

            if (Widgets.ButtonText(new Rect(new Vector2(rect.xMax - buttonX * 5, rect.y), new Vector2(buttonX, buttonY)), "+"))
            {
                foreach (WO_Settlement settlement in PM_Settlements.PlayerSettlements)
                {
                    if (settlement.Tile == playerSettlement.Tile)
                    {
                        SessionHandler.ChosenSettlement = settlement;

                        PM_Goodwills.TryRequestGoodwill(Goodwill.Ally,
                            GoodwillTarget.Settlement);

                        break;
                    }
                }
            }
        }

        protected override void CloseTab()
        {
            throw new System.NotImplementedException();
        }
    }
}
