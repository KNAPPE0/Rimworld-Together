using Shared;
using static Shared.CommonEnumerators;

namespace GameServer
{
    // Class that is in charge of the online market functions for the mod
    public static class MarketManager
    {
        // Variables
        private static readonly string marketFileName = "Market.json";
        private static readonly object marketLock = new object();

        // Parses received market packets into something usable
        public static void ParseMarketPacket(ServerClient client, Packet packet)
        {
            MarketData marketData = Serializer.ConvertBytesToObject<MarketData>(packet.Contents);

            switch (marketData.MarketStepMode)
            {
                case MarketStepMode.Add:
                    AddToMarket(client, marketData);
                    break;
                case MarketStepMode.Request:
                    RemoveFromMarket(client, marketData);
                    break;
                case MarketStepMode.Reload:
                    SendMarketStock(client, marketData);
                    break;
                default:
                    Logger.Warning($"Unknown market step mode: {marketData.MarketStepMode}");
                    break;
            }
        }

        private static void AddToMarket(ServerClient client, MarketData marketData)
        {
            lock (marketLock)
            {
                foreach (ThingData item in marketData.TransferThings)
                {
                    TryCombineStackIfAvailable(client, item);
                }

                SaveMarketData();
                NotifyClients(client, marketData);
            }
        }

        private static void RemoveFromMarket(ServerClient client, MarketData marketData)
        {
            lock (marketLock)
            {
                if (marketData.QuantityToManage <= 0)
                {
                    ResponseShortcutManager.SendIllegalPacket(client, "Tried to buy illegal quantity at market");
                    return;
                }

                try
                {
                    ThingData toGet = Master.market.MarketStock[marketData.IndexToManage];
                    int reservedQuantity = toGet.Quantity;

                    if (toGet.Quantity >= marketData.QuantityToManage)
                    {
                        toGet.Quantity -= marketData.QuantityToManage;

                        if (toGet.Quantity == 0)
                        {
                            Master.market.MarketStock.RemoveAt(marketData.IndexToManage);
                        }

                        marketData.TransferThings = new List<ThingData> { toGet };
                        NotifyClient(client, marketData);
                        NotifyClients(client, marketData);
                        SaveMarketData();
                    }
                    else
                    {
                        ResponseShortcutManager.SendIllegalPacket(client, "Tried to buy more than available at market");
                    }
                }
                catch (ArgumentOutOfRangeException)
                {
                    ResponseShortcutManager.SendIllegalPacket(client, "Invalid market index");
                }
            }
        }

        private static void SendMarketStock(ServerClient client, MarketData marketData)
        {
            lock (marketLock)
            {
                marketData.TransferThings = Master.market.MarketStock;
                Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.MarketPacket), marketData);
                client.Listener.EnqueuePacket(packet);
            }
        }

        private static void TryCombineStackIfAvailable(ServerClient client, ThingData thingData)
        {
            if (thingData.Quantity <= 0)
            {
                ResponseShortcutManager.SendIllegalPacket(client, "Tried to sell illegal quantity at market");
                return;
            }

            foreach (ThingData stockedItem in Master.market.MarketStock.ToArray())
            {
                if (stockedItem.DefName == thingData.DefName && stockedItem.MaterialDefName == thingData.MaterialDefName)
                {
                    stockedItem.Quantity += thingData.Quantity;
                    return;
                }
            }

            Master.market.MarketStock.Add(thingData);
        }

        private static void NotifyClient(ServerClient client, MarketData marketData)
        {
            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.MarketPacket), marketData);
            client.Listener.EnqueuePacket(packet);
        }

        private static void NotifyClients(ServerClient exceptClient, MarketData marketData)
        {
            marketData.MarketStepMode = MarketStepMode.Reload;
            marketData.TransferThings = Master.market.MarketStock;

            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.MarketPacket), marketData);
            foreach (ServerClient sc in Network.connectedClients.ToArray())
            {
                if (sc != exceptClient)
                {
                    sc.Listener.EnqueuePacket(packet);
                }
            }
        }

        private static void SaveMarketData()
        {
            Master.SaveValueFile(ServerFileMode.Market);
            Logger.Message("Market data saved successfully.");
        }
    }
}