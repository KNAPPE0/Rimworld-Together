using GameClient.Managers;
using RimWorld;
using RimWorld.Planet;
using Shared;
using System;
using System.Linq;
using UnityEngine;
using Verse;
using static TCPNetwork.Packets.PKT_Transfer;
using static Shared.CommonEnumerators;
using GameClient.Misc;
using GameClient.Hooks.TCPNetwork;
using TCPNetwork;
using GameClient.PacketManagers;

namespace GameClient.Dialogs
{
    public class DLG_ItemListing : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(460f, 560f);

        private Thing[] ListedThings { get; set; }
        private TransferMode TransferMode { get; set; }

        public static DLG_Base Instance { get; private set; } = null;

        public DLG_ItemListing(Thing[] listedThings, TransferMode transferMode)
        {
            ListedThings = listedThings ?? Array.Empty<Thing>();
            TransferMode = transferMode;
            Title = "Item Listing";
            Instance = this;

            SessionHandler.IsInTransfer = true;

            closeOnAccept = false;
            closeOnCancel = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = DrawStandardHeader(inRect);
            if (y < 0f) return;

            float footerH = SlimButtonSize.y + FooterPad * 2f;

            Rect listOuter = new Rect(0f, y, inRect.width, inRect.height - y - footerH).ContractedBy(ContentPad);
            Widgets.DrawMenuSection(listOuter);

            Rect listInner = listOuter.ContractedBy(10f);
            FillMainRect(listInner);

            Rect footer = new Rect(0f, inRect.height - footerH, inRect.width, footerH);

            float btnAvailHalf = (footer.width - (FooterPad * 3f)) / 2f;
            Vector2 btnSize = ClampButtonSize(SlimButtonSize, btnAvailHalf);

            Rect acceptBtn = new Rect(FooterPad, footer.y + FooterPad, btnSize.x, btnSize.y);
            Rect cancelBtn = new Rect(footer.xMax - FooterPad - btnSize.x, footer.y + FooterPad, btnSize.x, btnSize.y);

            if (Widgets.ButtonText(acceptBtn, "Accept"))
                Accept();

            if (Widgets.ButtonText(cancelBtn, "Cancel"))
                Reject();
        }

        private void FillMainRect(Rect mainRect)
        {
            float rowH = 30f;
            float height = 6f + ListedThings.Length * rowH;

            Rect viewRect = new Rect(0f, 0f, mainRect.width - GenUI.ScrollBarWidth, height);

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

        private void DrawCustomRow(Rect row, Thing thing, int index)
        {
            if (thing == null) return;

            if (index % 2 == 0)
                Widgets.DrawAltRect(row);

            Widgets.DrawHighlightIfMouseover(row);

            Text.Font = GameFont.Small;
            Rect textRect = row.ContractedBy(6f, 4f);

            string itemName = thing.LabelShort ?? "Unknown";
            if (itemName.Length > 1) itemName = char.ToUpper(itemName[0]) + itemName.Substring(1);
            else itemName = itemName.ToUpperInvariant();

            if (IsHumanThing(thing))
                Widgets.Label(textRect, $"[Human] {itemName}");
            else if (IsAnimalThing(thing))
                Widgets.Label(textRect, $"[Animal] {itemName}");
            else
                Widgets.Label(textRect, $"[Item] {itemName} (x{thing.stackCount}) ({thing.HitPoints} HP)");
        }

        private void Accept()
        {
            SessionHandler.LastTradeStep = CommonEnumerators.TradeMode.Receiving;

            if (TransferMode == TransferMode.Gift)
            {
                PM_Transfers.GetTransferedItemsToSettlement(ListedThings);
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
                    DLG_Base.PushNewDialog(new DLG_Message("ERROR", new string[] { "You do not have any pawn capable of trading!" }));
                    PM_Transfers.RejectRequest(TransferMode);
                    Close();
                }
            }
            else if (TransferMode == TransferMode.Rebound)
            {
                SessionHandler.IncomingManifest._stepMode = TransferStepMode.TradeReAccept;

                Network.ServerEndpoint.EnqueuePacket(PacketHeader.TransferManager, SessionHandler.IncomingManifest);

                PM_Transfers.GetTransferedItemsToCaravan(ListedThings);

                Close();
            }
            else if (TransferMode == TransferMode.Pod)
            {
                PM_Transfers.GetTransferedItemsToSettlement(ListedThings);
                Close();
            }
        }

        private void Reject()
        {
            PM_Transfers.RejectRequest(TransferMode);
            Close();
        }

        private static bool IsHumanThing(Thing thing)
        {
            if (thing == null) return false;

            if (thing is Pawn pawn)
                return pawn.RaceProps != null && pawn.RaceProps.Humanlike;

            if (thing is Corpse corpse && corpse.InnerPawn != null)
                return corpse.InnerPawn.RaceProps != null && corpse.InnerPawn.RaceProps.Humanlike;

            return false;
        }

        private static bool IsAnimalThing(Thing thing)
        {
            if (thing == null) return false;

            if (thing is Pawn pawn)
                return pawn.RaceProps != null && pawn.RaceProps.Animal;

            if (thing is Corpse corpse && corpse.InnerPawn != null)
                return corpse.InnerPawn.RaceProps != null && corpse.InnerPawn.RaceProps.Animal;

            return false;
        }
    }
}