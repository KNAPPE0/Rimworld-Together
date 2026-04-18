using GameServer.Core;
using GameServer.Hooks.TCPNetwork;
using GameServer.Managers;
using Shared;
using Shared.Files;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;
using static TCPNetwork.Packets.PKT_Transfer;

namespace GameServer.PacketManager
{
    public class PM_Transfers : PM_Base
    {
        [HandlesPacket(PacketHeader.TransferManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            if (!Master.ActionConfigs.EnableTrading)
            {
                ResponseShortcutManager.SendIllegalPacket(client, "Tried to use disabled feature!");
                return;
            }

            PKT_Transfer data = Serializer.ConvertBytesToObject<PKT_Transfer>(bytes);
            if (data == null) return;

            switch (data.CurrentStepMode)
            {
                case TransferStepMode.TradeRequest:
                    TransferThings(client, data);
                    break;

                case TransferStepMode.TradeReject:
                    RejectTransfer(client, bytes);
                    break;

                case TransferStepMode.TradeReRequest:
                    TransferThingsRebound(client, bytes);
                    break;

                case TransferStepMode.TradeReAccept:
                    AcceptReboundTransfer(client, bytes);
                    break;

                case TransferStepMode.TradeReReject:
                    RejectReboundTransfer(client, bytes);
                    break;
            }
        }

        public static void TransferThings(ServerClient client, PKT_Transfer transferData)
        {
            if (!PM_Settlements.CheckIfTileIsInUse(transferData.ToTile)) ResponseShortcutManager.SendIllegalPacket(client, $"Player {client.UserFile.Username} attempted to send items to a settlement at tile {transferData.ToTile}, but no settlement could be found");
            else
            {
                SettlementFile settlement = PM_Settlements.GetSettlementFileFromTile(transferData.ToTile);

                if (!UserManagerH.CheckIfUserIsConnected(settlement.Username)) 
                {
                    transferData.CurrentStepMode = TransferStepMode.Recover;
                    client.Listener.EnqueuePacket(PacketHeader.TransferManager, transferData);
                }

                else
                {
                    if (transferData.CurrentTransferMode == TransferMode.Gift)
                    {
                        transferData.CurrentStepMode = TransferStepMode.TradeAccept;
                        client.Listener.EnqueuePacket(PacketHeader.TransferManager, transferData);
                    }

                    transferData.CurrentStepMode = TransferStepMode.TradeRequest;
                    ServerNetwork.GetConnectedClientFromUsername(settlement.Username).Listener.EnqueuePacket(PacketHeader.TransferManager, transferData);
                }
            }
        }

        public static void RejectTransfer(ServerClient client, byte[] bytes)
        {
            PKT_Transfer transferData = Serializer.ConvertBytesToObject<PKT_Transfer>(bytes);
            if (transferData == null) return;

            SettlementFile settlement = PM_Settlements.GetSettlementFileFromTile(transferData.FromTile);
            if (!UserManagerH.CheckIfUserIsConnected(settlement.Username))
            {
                transferData.CurrentStepMode = TransferStepMode.Recover;
                client.Listener.EnqueuePacket(PacketHeader.TransferManager, transferData);
            }

            else
            {
                transferData.CurrentStepMode = TransferStepMode.TradeReject;
                ServerNetwork.GetConnectedClientFromUsername(settlement.Username).Listener.EnqueuePacket(PacketHeader.TransferManager, transferData);
            }
        }

        public static void TransferThingsRebound(ServerClient client, byte[] bytes)
        {
            PKT_Transfer transferData = Serializer.ConvertBytesToObject<PKT_Transfer>(bytes);
            if (transferData == null) return;

            SettlementFile settlement = PM_Settlements.GetSettlementFileFromTile(transferData.ToTile);
            if (!UserManagerH.CheckIfUserIsConnected(settlement.Username))
            {
                transferData.CurrentStepMode = TransferStepMode.TradeReReject;
                client.Listener.EnqueuePacket(PacketHeader.TransferManager, transferData);
            }

            else
            {
                transferData.CurrentTransferMode = TransferMode.Rebound;
                transferData.CurrentStepMode = TransferStepMode.TradeReRequest;
                ServerNetwork.GetConnectedClientFromUsername(settlement.Username).Listener.EnqueuePacket(PacketHeader.TransferManager, transferData);
            }
        }

        public static void AcceptReboundTransfer(ServerClient client, byte[] bytes)
        {
            PKT_Transfer transferData = Serializer.ConvertBytesToObject<PKT_Transfer>(bytes);
            if (transferData == null) return;

            SettlementFile settlement = PM_Settlements.GetSettlementFileFromTile(transferData.FromTile);
            if (!UserManagerH.CheckIfUserIsConnected(settlement.Username))
            {
                transferData.CurrentStepMode = TransferStepMode.Recover;
                client.Listener.EnqueuePacket(PacketHeader.TransferManager, transferData);
            }

            else
            {
                transferData.CurrentStepMode = TransferStepMode.TradeReAccept;
                ServerNetwork.GetConnectedClientFromUsername(settlement.Username).Listener.EnqueuePacket(PacketHeader.TransferManager, transferData);
            }
        }

        public static void RejectReboundTransfer(ServerClient client, byte[] bytes)
        {
            PKT_Transfer transferData = Serializer.ConvertBytesToObject<PKT_Transfer>(bytes);
            if (transferData == null) return;

            SettlementFile settlement = PM_Settlements.GetSettlementFileFromTile(transferData.FromTile);
            if (!UserManagerH.CheckIfUserIsConnected(settlement.Username))
            {
                transferData.CurrentStepMode = TransferStepMode.Recover;
                client.Listener.EnqueuePacket(PacketHeader.TransferManager, transferData);
            }

            else
            {
                transferData.CurrentStepMode = TransferStepMode.TradeReReject;
                ServerNetwork.GetConnectedClientFromUsername(settlement.Username).Listener.EnqueuePacket(PacketHeader.TransferManager, transferData);
            }
        }
    }
}
