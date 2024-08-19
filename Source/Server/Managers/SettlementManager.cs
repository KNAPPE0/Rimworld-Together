using Shared;
using static Shared.CommonEnumerators;

namespace GameServer
{
    public static class SettlementManager
    {
        public readonly static string fileExtension = ".mpsettlement";

        public static void ParseSettlementPacket(ServerClient client, Packet packet)
        {
            SettlementData settlementData = Serializer.ConvertBytesToObject<SettlementData>(packet.Contents);

            switch (settlementData.SettlementStepMode)
            {
                case SettlementStepMode.Add:
                    AddSettlement(client, settlementData);
                    break;
                case SettlementStepMode.Remove:
                    RemoveSettlement(client, settlementData);
                    break;
                default:
                    ResponseShortcutManager.SendIllegalPacket(client, $"Unknown settlement step mode: {settlementData.SettlementStepMode}");
                    break;
            }
        }

        public static void AddSettlement(ServerClient client, SettlementData settlementData)
        {
            if (CheckIfTileIsInUse(settlementData.Tile))
            {
                ResponseShortcutManager.SendIllegalPacket(client, $"Player {client.userFile.Username} attempted to add a settlement at tile {settlementData.Tile}, but that tile already has a settlement");
                return;
            }

            settlementData.Owner = client.userFile.Username;

            SettlementFile settlementFile = new SettlementFile
            {
                tile = settlementData.Tile,
                owner = client.userFile.Username
            };
            Serializer.SerializeToFile(Path.Combine(Master.settlementsPath, settlementFile.tile + fileExtension), settlementFile);

            settlementData.SettlementStepMode = SettlementStepMode.Add;
            BroadcastSettlementAddition(client, settlementData, settlementFile);

            Logger.Warning($"[Added settlement] > {settlementFile.tile} > {client.userFile.Username}");
        }

        public static void RemoveSettlement(ServerClient client, SettlementData settlementData)
        {
            if (!CheckIfTileIsInUse(settlementData.Tile))
            {
                ResponseShortcutManager.SendIllegalPacket(client, $"Settlement at tile {settlementData.Tile} was attempted to be removed, but the tile doesn't contain a settlement");
                return;
            }

            SettlementFile settlementFile = GetSettlementFileFromTile(settlementData.Tile);
            if (settlementFile == null)
            {
                ResponseShortcutManager.SendIllegalPacket(client, $"No settlement file found for tile {settlementData.Tile}");
                return;
            }

            if (client != null && settlementFile.owner != client.userFile.Username)
            {
                ResponseShortcutManager.SendIllegalPacket(client, $"Settlement at tile {settlementData.Tile} attempted to be removed by {client.userFile.Username}, but {settlementFile.owner} owns the settlement");
                return;
            }

            DeleteSettlement(settlementFile);
            SendRemovalSignal(client, settlementData);
        }

        private static void BroadcastSettlementAddition(ServerClient client, SettlementData settlementData, SettlementFile settlementFile)
        {
            foreach (ServerClient cClient in Network.connectedClients.ToArray())
            {
                if (cClient == client) continue;

                settlementData.Goodwill = GoodwillManager.GetSettlementGoodwill(cClient, settlementFile);
                Packet rPacket = Packet.CreatePacketFromObject(nameof(PacketHandler.SettlementPacket), settlementData);
                cClient.Listener.EnqueuePacket(rPacket);
            }
        }

        private static void DeleteSettlement(SettlementFile settlementFile)
        {
            try
            {
                File.Delete(Path.Combine(Master.settlementsPath, settlementFile.tile + fileExtension));
                Logger.Warning($"[Removed settlement] > {settlementFile.tile}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to delete settlement at tile {settlementFile.tile}: {ex.Message}");
            }
        }

        private static void SendRemovalSignal(ServerClient client, SettlementData settlementData)
        {
            settlementData.SettlementStepMode = SettlementStepMode.Remove;
            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.SettlementPacket), settlementData);
            foreach (ServerClient cClient in Network.connectedClients.ToArray())
            {
                if (cClient == client) continue;
                cClient.Listener.EnqueuePacket(packet);
            }
        }

        public static bool CheckIfTileIsInUse(int tileToCheck)
        {
            string[] settlements = Directory.GetFiles(Master.settlementsPath);
            foreach (string settlement in settlements)
            {
                if (!settlement.EndsWith(fileExtension)) continue;

                SettlementFile settlementJSON = Serializer.SerializeFromFile<SettlementFile>(settlement);
                if (settlementJSON?.tile == tileToCheck) return true;
            }

            return false;
        }

        public static SettlementFile GetSettlementFileFromTile(int tileToGet)
        {
            string[] settlements = Directory.GetFiles(Master.settlementsPath);
            foreach (string settlement in settlements)
            {
                if (!settlement.EndsWith(fileExtension)) continue;

                SettlementFile settlementFile = Serializer.SerializeFromFile<SettlementFile>(settlement);
                if (settlementFile?.tile == tileToGet) return settlementFile;
            }

            return null;
        }

        public static SettlementFile GetSettlementFileFromUsername(string UsernameToGet)
        {
            string[] settlements = Directory.GetFiles(Master.settlementsPath);
            foreach (string settlement in settlements)
            {
                if (!settlement.EndsWith(fileExtension)) continue;

                SettlementFile settlementFile = Serializer.SerializeFromFile<SettlementFile>(settlement);
                if (settlementFile?.owner == UsernameToGet) return settlementFile;
            }

            return null;
        }

        public static SettlementFile[] GetAllSettlements()
        {
            List<SettlementFile> settlementList = new List<SettlementFile>();
            string[] settlements = Directory.GetFiles(Master.settlementsPath);
            foreach (string settlement in settlements)
            {
                if (!settlement.EndsWith(fileExtension)) continue;

                SettlementFile settlementFile = Serializer.SerializeFromFile<SettlementFile>(settlement);
                if (settlementFile != null)
                {
                    settlementList.Add(settlementFile);
                }
            }

            return settlementList.ToArray();
        }

        public static SettlementFile[] GetAllSettlementsFromUsername(string UsernameToCheck)
        {
            List<SettlementFile> settlementList = new List<SettlementFile>();
            string[] settlements = Directory.GetFiles(Master.settlementsPath);
            foreach (string settlement in settlements)
            {
                if (!settlement.EndsWith(fileExtension)) continue;

                SettlementFile settlementFile = Serializer.SerializeFromFile<SettlementFile>(settlement);
                if (settlementFile?.owner == UsernameToCheck)
                {
                    settlementList.Add(settlementFile);
                }
            }

            return settlementList.ToArray();
        }

        public static void DeleteAllSettlementsFromUsername(string username)
        {
            SettlementFile[] settlements = GetAllSettlementsFromUsername(username);
            foreach (SettlementFile settlement in settlements)
            {
                DeleteSettlement(settlement);
            }
        }
    }
}