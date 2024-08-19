using Shared;
using static Shared.CommonEnumerators;

namespace GameServer
{
    public static class TransferManager
    {
        public static void ParseTransferPacket(ServerClient client, Packet packet)
        {
            TransferData transferData = Serializer.ConvertBytesToObject<TransferData>(packet.Contents);

            switch (transferData.TransferStepMode)
            {
                case TransferStepMode.TradeRequest:
                    TransferThings(client, transferData);
                    break;

                case TransferStepMode.TradeAccept:
                    // No action needed here
                    break;

                case TransferStepMode.TradeReject:
                    RejectTransfer(client, transferData);
                    break;

                case TransferStepMode.TradeReRequest:
                    TransferThingsRebound(client, transferData);
                    break;

                case TransferStepMode.TradeReAccept:
                    AcceptReboundTransfer(client, transferData);
                    break;

                case TransferStepMode.TradeReReject:
                    RejectReboundTransfer(client, transferData);
                    break;

                default:
                    Logger.Warning($"Unknown transfer step mode: {transferData.TransferStepMode}");
                    break;
            }
        }

        public static void TransferThings(ServerClient client, TransferData transferData)
        {
            if (!SettlementManager.CheckIfTileIsInUse(transferData.ToTile))
            {
                ResponseShortcutManager.SendIllegalPacket(client, $"Player {client.userFile.Username} attempted to send items to a settlement at tile {transferData.ToTile}, but no settlement could be found");
                return;
            }

            SettlementFile settlement = SettlementManager.GetSettlementFileFromTile(transferData.ToTile);
            if (settlement == null)
            {
                Logger.Error($"Failed to retrieve settlement file for tile {transferData.ToTile}");
                ResponseShortcutManager.SendIllegalPacket(client, "Settlement data could not be retrieved.");
                return;
            }

            if (!UserManager.CheckIfUserIsConnected(settlement.owner))
            {
                HandleUnavailableTransfer(client, transferData);
            }
            else
            {
                HandleTransfer(client, settlement.owner, transferData);
            }
        }

        private static void HandleUnavailableTransfer(ServerClient client, TransferData transferData)
        {
            if (transferData.TransferMode == TransferMode.Pod)
            {
                ResponseShortcutManager.SendUnavailablePacket(client);
            }
            else
            {
                transferData.TransferStepMode = TransferStepMode.Recover;
                Packet rPacket = Packet.CreatePacketFromObject(nameof(PacketHandler.TransferPacket), transferData);
                client.Listener.EnqueuePacket(rPacket);
            }
        }

        private static void HandleTransfer(ServerClient client, string owner, TransferData transferData)
        {
            transferData.TransferStepMode = TransferStepMode.TradeRequest;
            Packet rPacket = Packet.CreatePacketFromObject(nameof(PacketHandler.TransferPacket), transferData);
            UserManager.GetConnectedClientFromUsername(owner)?.Listener.EnqueuePacket(rPacket);

            if (transferData.TransferMode == TransferMode.Gift || transferData.TransferMode == TransferMode.Pod)
            {
                transferData.TransferStepMode = TransferStepMode.TradeAccept;
                client.Listener.EnqueuePacket(Packet.CreatePacketFromObject(nameof(PacketHandler.TransferPacket), transferData));
            }
        }

        public static void RejectTransfer(ServerClient client, TransferData transferData)
        {
            SettlementFile settlement = SettlementManager.GetSettlementFileFromTile(transferData.FromTile);
            if (settlement == null)
            {
                Logger.Error($"Failed to retrieve settlement file for tile {transferData.FromTile}");
                return;
            }

            if (!UserManager.CheckIfUserIsConnected(settlement.owner))
            {
                transferData.TransferStepMode = TransferStepMode.Recover;
                client.Listener.EnqueuePacket(Packet.CreatePacketFromObject(nameof(PacketHandler.TransferPacket), transferData));
            }
            else
            {
                transferData.TransferStepMode = TransferStepMode.TradeReject;
                UserManager.GetConnectedClientFromUsername(settlement.owner)?.Listener.EnqueuePacket(Packet.CreatePacketFromObject(nameof(PacketHandler.TransferPacket), transferData));
            }
        }

        public static void TransferThingsRebound(ServerClient client, TransferData transferData)
        {
            SettlementFile settlement = SettlementManager.GetSettlementFileFromTile(transferData.ToTile);
            if (settlement == null)
            {
                Logger.Error($"Failed to retrieve settlement file for tile {transferData.ToTile}");
                return;
            }

            if (!UserManager.CheckIfUserIsConnected(settlement.owner))
            {
                transferData.TransferStepMode = TransferStepMode.TradeReReject;
                client.Listener.EnqueuePacket(Packet.CreatePacketFromObject(nameof(PacketHandler.TransferPacket), transferData));
            }
            else
            {
                transferData.TransferStepMode = TransferStepMode.TradeReRequest;
                UserManager.GetConnectedClientFromUsername(settlement.owner)?.Listener.EnqueuePacket(Packet.CreatePacketFromObject(nameof(PacketHandler.TransferPacket), transferData));
            }
        }

        public static void AcceptReboundTransfer(ServerClient client, TransferData transferData)
        {
            SettlementFile settlement = SettlementManager.GetSettlementFileFromTile(transferData.FromTile);
            if (settlement == null)
            {
                Logger.Error($"Failed to retrieve settlement file for tile {transferData.FromTile}");
                return;
            }

            if (!UserManager.CheckIfUserIsConnected(settlement.owner))
            {
                transferData.TransferStepMode = TransferStepMode.Recover;
                client.Listener.EnqueuePacket(Packet.CreatePacketFromObject(nameof(PacketHandler.TransferPacket), transferData));
            }
            else
            {
                transferData.TransferStepMode = TransferStepMode.TradeReAccept;
                UserManager.GetConnectedClientFromUsername(settlement.owner)?.Listener.EnqueuePacket(Packet.CreatePacketFromObject(nameof(PacketHandler.TransferPacket), transferData));
            }
        }

        public static void RejectReboundTransfer(ServerClient client, TransferData transferData)
        {
            SettlementFile settlement = SettlementManager.GetSettlementFileFromTile(transferData.FromTile);
            if (settlement == null)
            {
                Logger.Error($"Failed to retrieve settlement file for tile {transferData.FromTile}");
                return;
            }

            if (!UserManager.CheckIfUserIsConnected(settlement.owner))
            {
                transferData.TransferStepMode = TransferStepMode.Recover;
                client.Listener.EnqueuePacket(Packet.CreatePacketFromObject(nameof(PacketHandler.TransferPacket), transferData));
            }
            else
            {
                transferData.TransferStepMode = TransferStepMode.TradeReReject;
                UserManager.GetConnectedClientFromUsername(settlement.owner)?.Listener.EnqueuePacket(Packet.CreatePacketFromObject(nameof(PacketHandler.TransferPacket), transferData));
            }
        }
    }
}