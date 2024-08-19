using Shared;
using static Shared.CommonEnumerators;

namespace GameServer
{
    public static class AidManager
    {
        private static readonly double BaseAidTimer = 3600000;

        public static void ParsePacket(ServerClient client, Packet packet)
        {
            if (packet == null || packet.Contents == null)
            {
                Logger.Error("[AidManager] > Received null packet or Contents.");
                return;
            }

            var data = Serializer.ConvertBytesToObject<AidData>(packet.Contents);
            if (data == null)
            {
                Logger.Error("[AidManager] > Failed to deserialize aid data.");
                return;
            }

            switch (data.StepMode)
            {
                case AidStepMode.Send:
                    HandleAidRequest(client, data);
                    break;
                case AidStepMode.Receive:
                    Logger.Message("[AidManager] > Receive mode not implemented.");
                    break;
                case AidStepMode.Accept:
                    HandleAidAccept(client, data);
                    break;
                case AidStepMode.Reject:
                    HandleAidReject(client, data);
                    break;
                default:
                    Logger.Warning("[AidManager] > Unknown AidStepMode.");
                    break;
            }
        }

        private static void HandleAidRequest(ServerClient client, AidData data)
        {
            if (client == null || data == null)
            {
                Logger.Error("[AidManager] > Client or data is null in HandleAidRequest.");
                return;
            }

            if (!SettlementManager.CheckIfTileIsInUse(data.ToTile))
            {
                RejectAid(client, data, $"No settlement at tile {data.ToTile}");
                return;
            }

            var settlementFile = SettlementManager.GetSettlementFileFromTile(data.ToTile);
            if (settlementFile == null)
            {
                RejectAid(client, data, $"Failed to retrieve settlement data for tile {data.ToTile}");
                return;
            }

            if (UserManager.CheckIfUserIsConnected(settlementFile.owner))
            {
                var target = UserManager.GetConnectedClientFromUsername(settlementFile.owner);

                if (Master.serverConfig.TemporalAidProtection &&
                    !TimeConverter.CheckForEpochTimer(target.userFile.AidProtectionTime, BaseAidTimer))
                {
                    RejectAid(client, data, "Aid request rejected due to temporal protection.");
                    return;
                }

                data.StepMode = AidStepMode.Receive;
                SendPacket(target, data);
            }
            else
            {
                RejectAid(client, data, $"Owner of the settlement at tile {data.ToTile} is not connected.");
            }
        }

        private static void HandleAidAccept(ServerClient client, AidData data)
        {
            if (client == null || data == null)
            {
                Logger.Error("[AidManager] > Client or data is null in HandleAidAccept.");
                return;
            }

            if (!SettlementManager.CheckIfTileIsInUse(data.FromTile))
            {
                RejectAid(client, data, $"No settlement at tile {data.FromTile}");
                return;
            }

            var settlementFile = SettlementManager.GetSettlementFileFromTile(data.FromTile);
            if (settlementFile == null)
            {
                RejectAid(client, data, $"Failed to retrieve settlement data for tile {data.FromTile}");
                return;
            }

            if (UserManager.CheckIfUserIsConnected(settlementFile.owner))
            {
                client.userFile.UpdateAidTime();
                var target = UserManager.GetConnectedClientFromUsername(settlementFile.owner);
                SendPacket(target, data);
            }
            else
            {
                RejectAid(client, data, $"Owner of the settlement at tile {data.FromTile} is not connected.");
            }
        }

        private static void HandleAidReject(ServerClient client, AidData data)
        {
            if (client == null || data == null)
            {
                Logger.Error("[AidManager] > Client or data is null in HandleAidReject.");
                return;
            }

            if (!SettlementManager.CheckIfTileIsInUse(data.FromTile))
            {
                Logger.Warning($"[AidManager] > No settlement at tile {data.FromTile} for rejection.");
                return;
            }

            var settlementFile = SettlementManager.GetSettlementFileFromTile(data.FromTile);
            if (settlementFile != null && UserManager.CheckIfUserIsConnected(settlementFile.owner))
            {
                var target = UserManager.GetConnectedClientFromUsername(settlementFile.owner);
                SendPacket(target, data);
            }

            SendPacket(client, data);
        }

        private static void RejectAid(ServerClient client, AidData data, string reason)
        {
            if (client == null || data == null)
            {
                Logger.Error("[AidManager] > Client or data is null in RejectAid.");
                return;
            }

            Logger.Warning($"[AidManager] > Aid rejected: {reason}");
            data.StepMode = AidStepMode.Reject;
            SendPacket(client, data);
        }

        private static void SendPacket(ServerClient client, AidData data)
        {
            if (client == null || data == null)
            {
                Logger.Error("[AidManager] > Client or data is null in SendPacket.");
                return;
            }

            var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.AidPacket), data);
            client.Listener.EnqueuePacket(packet);
        }
    }
}