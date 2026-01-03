using GameClient.Managers;
using RimWorld;
using RimWorld.Planet;
using Shared;
using System;
using System.Linq;
using UnityEngine;
using Verse;
using static TCPNetwork.Packets.TransferData;
using static Shared.CommonEnumerators;
using GameClient.Misc;

namespace GameClient.Dialogs
{
    public class RT_Dialog_ItemListing : RT_Dialog_Base
    {
        public override Vector2 InitialSize => new Vector2(400f, 512f);

        private Thing[] ListedThings { get; set; }
        private TransferMode TransferMode { get; set; }

        public static RT_Dialog_Base Instance { get; private set; } = null;

        public RT_Dialog_ItemListing(Thing[] listedThings, TransferMode transferMode)
        {
            ListedThings = listedThings ?? Array.Empty<Thing>();
            TransferMode = transferMode;
            Title = "Item Listing";
            Instance = this;

            SessionHandler.IsInTransfer = true;

            closeOnAccept = false;
            closeOnCancel = false;
        }

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.width / 2 - Text.CalcSize(Title).x / 2, rect.y, rect.width, Text.CalcSize(Title).y), Title);

            FillMainRect(new Rect(0f, 35f, rect.width, rect.height - SlimButtonSize.y - 45));

            Text.Font = GameFont.Small;

            if (Widgets.ButtonText(new Rect(new Vector2(rect.x, rect.yMax - SlimButtonSize.y), SlimButtonSize), "Accept"))
                Accept();

            if (Widgets.ButtonText(new Rect(new Vector2(rect.xMax - SlimButtonSize.x, rect.yMax - SlimButtonSize.y), SlimButtonSize), "Cancel"))
                Reject();
        }

        private void FillMainRect(Rect mainRect)
        {
            Widgets.DrawLineHorizontal(mainRect.x, mainRect.y - 1, mainRect.width);

            float rowH = 30f;
            float height = 6f + ListedThings.Length * rowH;

            Rect viewRect = new Rect(0f, 0f, mainRect.width - 16f, height);

            Widgets.BeginScrollView(mainRect, ref ScrollPosition, viewRect);
            try
            {
                float y = 0f;
                float yMin = ScrollPosition.y - rowH;
                float yMax = ScrollPosition.y + mainRect.height;

                for (int i = 0; i < ListedThings.Length; i++)
                {
                    if (y > yMin && y < yMax)
                    {
                        Rect row = new Rect(0f, y, viewRect.width, rowH);
                        DrawCustomRow(row, ListedThings[i], i);
                    }
                    y += rowH;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private void DrawCustomRow(Rect rect, Thing thing, int index)
        {
            if (thing == null) return;

            Text.Font = GameFont.Small;
            Rect fixedRect = new Rect(rect.x, rect.y + 5f, rect.width - 16f, rect.height - 5f);
            if (index % 2 == 0) Widgets.DrawHighlight(fixedRect);

            string itemName = thing.LabelShort;
            if (itemName.Length > 1) itemName = char.ToUpper(itemName[0]) + itemName.Substring(1);
            else itemName = itemName.ToUpper();

            if (ScriberH.CheckIfThingIsHuman(thing)) Widgets.Label(fixedRect, $"[Human] {itemName}");
            else if (ScriberH.CheckIfThingIsAnimal(thing)) Widgets.Label(fixedRect, $"[Animal] {itemName}");
            else Widgets.Label(fixedRect, $"[Item] {itemName} (x{thing.stackCount}) ({thing.HitPoints} HP)");
        }

        private void Accept()
        {
            SessionHandler.LastTradeStep = CommonEnumerators.TradeMode.Receiving;

            if (TransferMode == TransferMode.Gift)
            {
                TransferManager.GetTransferedItemsToSettlement(ListedThings);
                Close();
            }
            else if (TransferMode == TransferMode.Trade)
            {
                if (RimworldManager.CheckIfSocialPawnInMap(Find.AnyPlayerHomeMap))
                {
                    Settlement settlement = Find.World.worldObjects.Settlements.First(fetch => fetch.Faction != Faction.OfPlayer);
                    Pawn negotiator = RimworldManager.GetNegotiatorAtMap(settlement.Map);
                    Find.WindowStack.Add(new Dialog_Trade(negotiator, settlement));
                }
                else
                {
                    RT_Dialog_Base.PushNewDialog(new RT_Dialog_Message("ERROR", new string[] { "You do not have any pawn capable of trading!" }));
                    TransferManager.RejectRequest(TransferMode);
                    Close();
                }
            }
            else if (TransferMode == TransferMode.Rebound)
            {
                SessionHandler.IncomingManifest._stepMode = TransferStepMode.TradeReAccept;

                ClientNetwork.Instance.ClientListener.EnqueuePacket(PacketHeader.TransferManager, SessionHandler.IncomingManifest);

                TransferManager.GetTransferedItemsToCaravan(ListedThings);
                Close();
            }
            else if (TransferMode == TransferMode.Pod)
            {
                TransferManager.GetTransferedItemsToSettlement(ListedThings);
                Close();
            }
        }

        private void Reject()
        {
            TransferManager.RejectRequest(TransferMode);
            Close();
        }
    }
}