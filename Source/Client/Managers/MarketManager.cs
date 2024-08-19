using RimWorld;
using Shared;
using System;
using Verse;
using Verse.Sound;
using static Shared.CommonEnumerators;

namespace GameClient
{
    // Class that is in charge of the online market functions for the mod
    public static class MarketManager
    {
        // Parses received market packets into something usable
        public static void ParseMarketPacket(Packet packet)
        {
            if (packet?.Contents == null)
            {
                Logger.Error("Received null packet or packet contents in ParseMarketPacket.");
                return;
            }

            MarketData? marketData = Serializer.ConvertBytesToObject<MarketData>(packet.Contents);
            if (marketData == null)
            {
                Logger.Error("Failed to deserialize MarketData in ParseMarketPacket.");
                return;
            }

            switch (marketData.MarketStepMode)
            {
                case MarketStepMode.Add:
                    ConfirmAddStock();
                    break;

                case MarketStepMode.Request:
                    ConfirmGetStock(marketData);
                    break;

                case MarketStepMode.Reload:
                    ConfirmReloadStock(marketData);
                    break;

                default:
                    Logger.Warning($"Unknown MarketStepMode received: {marketData.MarketStepMode}");
                    break;
            }
        }

        // Add to stock functions
        public static void RequestAddStock()
        {
            var transferMenuDialog = new RT_Dialog_TransferMenu(TransferLocation.World, true, false, false, false);
            DialogManager.PushNewDialog(transferMenuDialog);
        }

        public static void ConfirmAddStock()
        {
            DialogManager.PopWaitDialog();
            DialogManager.dialogMarketListing = null;

            int silverToGet = 0;
            var sentItems = TransferManagerHelper.GetAllTransferedItems(ClientValues.outgoingManifest);
            foreach (var thing in sentItems)
            {
                silverToGet += (int)(thing.stackCount * thing.MarketValue * 0.5f);
            }

            if (silverToGet > 0)
            {
                var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = silverToGet;
                TransferManager.GetTransferedItemsToSettlement(new[] { silver }, customMap: false);
            }
            else
            {
                TransferManager.FinishTransfer(true);
                DialogManager.PushNewDialog(new RT_Dialog_OK("Transfer was a success!"));
            }
        }

        // Get from stock functions
        public static void RequestGetStock(int marketIndex, int quantity)
        {
            DialogManager.PushNewDialog(new RT_Dialog_Wait("Waiting for market response"));

            var data = new MarketData
            {
                MarketStepMode = MarketStepMode.Request,
                IndexToManage = marketIndex,
                QuantityToManage = quantity
            };

            var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.MarketPacket), data);
            Network.Listener.EnqueuePacket(packet);
        }

        public static void ConfirmGetStock(MarketData marketData)
        {
            DialogManager.PopWaitDialog();
            DialogManager.dialogMarketListing = null;

            var toReceive = ThingScribeManager.StringToItem(marketData.TransferThings[0]);
            if (toReceive == null)
            {
                Logger.Error("Failed to deserialize Thing in ConfirmGetStock.");
                return;
            }

            TransferManager.GetTransferedItemsToSettlement(new[] { toReceive }, customMap: false);

            int silverToPay = (int)(toReceive.MarketValue * toReceive.stackCount);
            RimworldManager.RemoveThingFromSettlement(ClientValues.chosenSettlement.Map, ThingDefOf.Silver, silverToPay);

            SoundDefOf.ExecuteTrade.PlayOneShotOnCamera();
        }

        // Reload stock functions
        public static void RequestReloadStock()
        {
            var marketListingDialog = new RT_Dialog_MarketListing(Array.Empty<ThingData>(), ClientValues.chosenSettlement.Map, null, null);
            DialogManager.PushNewDialog(marketListingDialog);
            DialogManager.PushNewDialog(new RT_Dialog_Wait("Waiting for market response"));

            var marketData = new MarketData { MarketStepMode = MarketStepMode.Reload };
            var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.MarketPacket), marketData);
            Network.Listener.EnqueuePacket(packet);
        }

        private static void ConfirmReloadStock(MarketData marketData)
        {
            if (DialogManager.dialogMarketListing != null && !ClientValues.isInTransfer)
            {
                DialogManager.PopWaitDialog();

                Action toDo = () => RequestGetStock(DialogManager.dialogMarketListingResult, int.Parse(DialogManager.dialog1ResultOne));
                var dialog = new RT_Dialog_MarketListing(marketData.TransferThings.ToArray(), ClientValues.chosenSettlement.Map, toDo, null);
                DialogManager.PushNewDialog(dialog);
            }
        }
    }
}
