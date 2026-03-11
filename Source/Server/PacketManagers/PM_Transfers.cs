using GameServer.Core;
using GameServer.Hooks.TCPNetwork;
using GameServer.Managers;
using GameServer.Misc;
using Shared;
using Shared.Files;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static Shared.CommonEnumerators;
using static TCPNetwork.Packets.TransferData;

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

            TransferData data = Serializer.ConvertBytesToObject<TransferData>(bytes);

            switch (data._stepMode)
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

        public static void TransferThings(ServerClient client, TransferData transferData)
        {
            if (!PM_Settlements.CheckIfTileIsInUse(transferData._toTile)) ResponseShortcutManager.SendIllegalPacket(client, $"Player {client.UserFile.Username} attempted to send items to a settlement at tile {transferData._toTile}, but no settlement could be found");
            else
            {
                SettlementFile settlement = PM_Settlements.GetSettlementFileFromTile(transferData._toTile);

                if (!UserManagerH.CheckIfUserIsConnected(settlement.Username))
                {
                    if (transferData._transferMode == TransferMode.Pod) ResponseShortcutManager.SendUnavailablePacket(client);
                    else
                    {
                        transferData._stepMode = TransferStepMode.Recover;
                        client.Listener.EnqueuePacket(PacketHeader.TransferManager, transferData);
                    }
                }

                else
                {
                    if (transferData._transferMode == TransferMode.Gift)
                    {
                        transferData._stepMode = TransferStepMode.TradeAccept;
                        client.Listener.EnqueuePacket(PacketHeader.TransferManager, transferData);
                    }

                    else if (transferData._transferMode == TransferMode.Pod)
                    {
                        transferData._stepMode = TransferStepMode.TradeAccept;
                        client.Listener.EnqueuePacket(PacketHeader.TransferManager, transferData);
                    }

                    transferData._stepMode = TransferStepMode.TradeRequest;
                    ServerNetwork.GetConnectedClientFromUsername(settlement.Username).Listener.EnqueuePacket(PacketHeader.TransferManager, transferData);
                }
            }
        }

        public static void RejectTransfer(ServerClient client, byte[] bytes)
        {
            TransferData transferData = Serializer.ConvertBytesToObject<TransferData>(bytes);

            SettlementFile settlement = PM_Settlements.GetSettlementFileFromTile(transferData._fromTile);
            if (!UserManagerH.CheckIfUserIsConnected(settlement.Username))
            {
                transferData._stepMode = TransferStepMode.Recover;
                client.Listener.EnqueuePacket(PacketHeader.TransferManager, transferData);
            }

            else
            {
                transferData._stepMode = TransferStepMode.TradeReject;
                ServerNetwork.GetConnectedClientFromUsername(settlement.Username).Listener.EnqueuePacket(PacketHeader.TransferManager, transferData);
            }
        }

        public static void TransferThingsRebound(ServerClient client, byte[] bytes)
        {
            TransferData transferData = Serializer.ConvertBytesToObject<TransferData>(bytes);

            SettlementFile settlement = PM_Settlements.GetSettlementFileFromTile(transferData._toTile);
            if (!UserManagerH.CheckIfUserIsConnected(settlement.Username))
            {
                transferData._stepMode = TransferStepMode.TradeReReject;
                client.Listener.EnqueuePacket(PacketHeader.TransferManager, transferData);
            }

            else
            {
                transferData._stepMode = TransferStepMode.TradeReRequest;
                ServerNetwork.GetConnectedClientFromUsername(settlement.Username).Listener.EnqueuePacket(PacketHeader.TransferManager, transferData);
            }
        }

        public static void AcceptReboundTransfer(ServerClient client, byte[] bytes)
        {
            TransferData transferData = Serializer.ConvertBytesToObject<TransferData>(bytes);

            SettlementFile settlement = PM_Settlements.GetSettlementFileFromTile(transferData._fromTile);
            if (!UserManagerH.CheckIfUserIsConnected(settlement.Username))
            {
                transferData._stepMode = TransferStepMode.Recover;
                client.Listener.EnqueuePacket(PacketHeader.TransferManager, transferData);
            }

            else
            {
                transferData._stepMode = TransferStepMode.TradeReAccept;
                ServerNetwork.GetConnectedClientFromUsername(settlement.Username).Listener.EnqueuePacket(PacketHeader.TransferManager, transferData);
            }
        }

        public static void RejectReboundTransfer(ServerClient client, byte[] bytes)
        {
            TransferData transferData = Serializer.ConvertBytesToObject<TransferData>(bytes);

            SettlementFile settlement = PM_Settlements.GetSettlementFileFromTile(transferData._fromTile);
            if (!UserManagerH.CheckIfUserIsConnected(settlement.Username))
            {
                transferData._stepMode = TransferStepMode.Recover;
                client.Listener.EnqueuePacket(PacketHeader.TransferManager, transferData);
            }

            else
            {
                transferData._stepMode = TransferStepMode.TradeReReject;
                ServerNetwork.GetConnectedClientFromUsername(settlement.Username).Listener.EnqueuePacket(PacketHeader.TransferManager, transferData);
            }
        }
    }
}
