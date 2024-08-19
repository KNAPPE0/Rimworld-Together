using Shared;
using static Shared.CommonEnumerators;
using System.IO;
using System.Linq;
using System.Threading;

namespace GameServer
{
    public static class CaravanManager
    {
        // Constants and Variables
        private static readonly string FileExtension = ".mpcaravan";
        private static readonly double BaseMaxTimer = 86400000;

        // Packet Parsing
        public static void ParsePacket(ServerClient client, Packet packet)
        {
            var data = Serializer.ConvertBytesToObject<CaravanData>(packet.Contents);
            if (data == null)
            {
                Logger.Error("[CaravanManager] > Failed to deserialize caravan data.");
                return;
            }

            switch (data.StepMode)
            {
                case CaravanStepMode.Add:
                    AddCaravan(client, data);
                    break;
                case CaravanStepMode.Remove:
                    RemoveCaravan(client, data);
                    break;
                case CaravanStepMode.Move:
                    MoveCaravan(client, data);
                    break;
                default:
                    Logger.Warning("[CaravanManager] > Unknown CaravanStepMode.");
                    break;
            }
        }

        // Add Caravan
        private static void AddCaravan(ServerClient client, CaravanData data)
        {
            data.Details.Id = GetNewCaravanID();
            RefreshCaravanTimer(data.Details);
            SaveCaravan(data.Details);

            var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.CaravanPacket), data);
            NetworkHelper.SendPacketToAllClients(packet);

            Logger.Message($"[Add Caravan] > {data.Details.Id} > {client.userFile.Username}");
        }

        // Remove Caravan
        private static void RemoveCaravan(ServerClient client, CaravanData data)
        {
            var toRemove = GetCaravanFromID(client, data.Details.Id);
            if (toRemove == null)
            {
                Logger.Warning($"[Remove Caravan] > Caravan ID {data.Details.Id} not found.");
                return;
            }

            DeleteCaravan(toRemove);

            var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.CaravanPacket), data);
            NetworkHelper.SendPacketToAllClients(packet);

            Logger.Message($"[Remove Caravan] > {data.Details.Id} > {client.userFile.Username}");
        }

        // Move Caravan
        private static void MoveCaravan(ServerClient client, CaravanData data)
        {
            var toMove = GetCaravanFromID(client, data.Details.Id);
            if (toMove == null)
            {
                Logger.Warning($"[Move Caravan] > Caravan ID {data.Details.Id} not found.");
                return;
            }

            UpdateCaravan(toMove, data.Details);
            RefreshCaravanTimer(toMove);

            var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.CaravanPacket), data);
            NetworkHelper.SendPacketToAllClients(packet, client);
        }

        // Save Caravan
        private static void SaveCaravan(CaravanDetails details)
        {
            Serializer.SerializeToFile(Path.Combine(Master.caravansPath, $"{details.Id}{FileExtension}"), details);
        }

        // Delete Caravan
        private static void DeleteCaravan(CaravanDetails details)
        {
            File.Delete(Path.Combine(Master.caravansPath, $"{details.Id}{FileExtension}"));
        }

        // Update Caravan
        private static void UpdateCaravan(CaravanDetails current, CaravanDetails updated)
        {
            current.Tile = updated.Tile;
            SaveCaravan(current);
        }

        // Refresh Caravan Timer
        private static void RefreshCaravanTimer(CaravanDetails details)
        {
            details.TimeSinceRefresh = TimeConverter.CurrentTimeToEpoch();
            SaveCaravan(details);
        }

        // Start Caravan Ticker
        public static void StartCaravanTicker()
        {
            while (true)
            {
                Thread.Sleep(1800000); // 30 minutes
                try 
                { 
                    IdleCaravanTick(); 
                }
                catch (Exception e) 
                { 
                    Logger.Error($"[CaravanManager] > Caravan tick failed. Exception: {e}"); 
                }
            }
        }

        // Idle Caravan Tick
        private static void IdleCaravanTick()
        {
            foreach (var caravan in GetActiveCaravans())
            {
                if (TimeConverter.CheckForEpochTimer(caravan.TimeSinceRefresh, BaseMaxTimer))
                {
                    DeleteCaravan(caravan);

                    var data = new CaravanData
                    {
                        StepMode = CaravanStepMode.Remove,
                        Details = caravan
                    };

                    var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.CaravanPacket), data);
                    NetworkHelper.SendPacketToAllClients(packet);
                }
            }

            Logger.Message("[CaravanManager] > Caravan tick completed.");
        }

        // Get Active Caravans
        public static CaravanDetails[] GetActiveCaravans()
        {
            return Directory.GetFiles(Master.caravansPath)
                .Select(file => Serializer.SerializeFromFile<CaravanDetails>(file))
                .ToArray();
        }

        // Get Caravan by ID
        public static CaravanDetails GetCaravanFromID(ServerClient client, int caravanID)
        {
            return GetActiveCaravans()
                .FirstOrDefault(caravan => caravan.Id == caravanID && caravan.Owner == client.userFile.Username);
        }

        // Generate New Caravan ID
        private static int GetNewCaravanID()
        {
            return GetActiveCaravans()
                .Select(caravan => caravan.Id)
                .DefaultIfEmpty(0)
                .Max() + 1;
        }
    }
}
