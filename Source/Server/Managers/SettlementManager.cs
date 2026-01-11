using GameServer.Core;
using GameServer.Misc;
using Shared;
using Shared.Files;
using System;
using System.Collections.Generic;
using System.IO;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static Shared.CommonEnumerators;

namespace GameServer.Managers
{
    public static class SettlementManager
    {
        [HandlesPacket(PacketHeader.SettlementManager)]
        private static void ParsePacket(ServerClient client, byte[] bytes)
        {
            PlayerSettlementData data = Serializer.ConvertBytesToObject<PlayerSettlementData>(bytes);

            switch (data._stepMode)
            {
                case SettlementStepMode.Add:
                    AddSettlement(client, data);
                    break;

                case SettlementStepMode.Remove:
                    RemoveSettlement(client, data);
                    break;
            }
        }

        public static void AddSettlement(ServerClient client, PlayerSettlementData settlementData)
        {
            if (client == null || settlementData == null) return;

            int tile = settlementData._settlementFile.Tile;

            if (CheckIfTileIsInUse(tile))
            {
                ResponseShortcutManager.SendIllegalPacket(
                    client,
                    $"Player {client.UserFile.Username} attempted to add a settlement at tile {tile}, but that tile already has a settlement");
                return;
            }

            MapManager.DeleteMapByTile(tile);

            SettlementFile settlementFile = new SettlementFile();
            settlementFile.Tile = tile;
            settlementFile.Username = client.UserFile.Username;
            settlementFile.Name = settlementData._settlementFile.Name ?? string.Empty;

            settlementData._settlementFile = settlementFile;

            Directory.CreateDirectory(Master.SettlementsPath);
            Serializer.SerializeToFile(Path.Combine(Master.SettlementsPath, settlementFile.Tile + CommonValues.DefaultSaveFormat), settlementFile);

            settlementData._stepMode = SettlementStepMode.Add;

            foreach (ServerClient cClient in ServerNetwork.Instance.GetConnectedClientsSafe())
            {
                if (cClient == client) continue;

                settlementData._settlementFile.Goodwill = GoodwillManager.GetSettlementGoodwill(cClient, settlementFile);
                cClient.Listener.EnqueuePacket(PacketHeader.SettlementManager, settlementData);
            }

            InformationDisplayer.DisplayAddSettlement(settlementFile.Tile.ToString());
        }

        public static void RemoveSettlement(ServerClient client, PlayerSettlementData settlementData)
        {
            if (settlementData == null) return;

            int tile = settlementData._settlementFile.Tile;

            if (!CheckIfTileIsInUse(tile))
            {
                if (client != null)
                    ResponseShortcutManager.SendIllegalPacket(client, $"Settlement at tile {tile} was attempted to be removed, but the tile doesn't contain a settlement");
                return;
            }

            SettlementFile settlementFile = GetSettlementFileFromTile(tile);
            if (settlementFile == null) return;

            if (client != null && settlementFile.Username != client.UserFile.Username)
            {
                ResponseShortcutManager.SendIllegalPacket(
                    client,
                    $"Settlement at tile {tile} attempted to be removed by {client.UserFile.Username}, but {settlementFile.Username} owns the settlement");
                return;
            }

            try
            {
                File.Delete(Path.Combine(Master.SettlementsPath, settlementFile.Tile + CommonValues.DefaultSaveFormat));
            }
            catch { }

            MapManager.DeleteMapByTile(settlementFile.Tile);

            settlementData._stepMode = SettlementStepMode.Remove;
            ServerNetwork.Instance.SendPacketToAllClients(PacketHeader.SettlementManager, settlementData, client);

            InformationDisplayer.DisplayRemoveSettlement(settlementFile.Tile.ToString());
        }

        public static bool CheckIfTileIsInUse(int tileToCheck)
        {
            if (!Directory.Exists(Master.SettlementsPath)) return false;

            string[] settlements = Directory.GetFiles(Master.SettlementsPath);
            foreach (string settlement in settlements)
            {
                SettlementFile settlementJSON = Serializer.SerializeFromFile<SettlementFile>(settlement);
                if (settlementJSON != null && settlementJSON.Tile == tileToCheck) return true;
            }

            return false;
        }

        public static SettlementFile GetSettlementFileFromTile(int tileToGet)
        {
            if (!Directory.Exists(Master.SettlementsPath)) return null;

            string[] settlements = Directory.GetFiles(Master.SettlementsPath);
            foreach (string settlement in settlements)
            {
                SettlementFile settlementFile = Serializer.SerializeFromFile<SettlementFile>(settlement);
                if (settlementFile != null && settlementFile.Tile == tileToGet) return settlementFile;
            }

            return null;
        }

        public static SettlementFile GetSettlementFileFromUsername(string usernameToGet)
        {
            if (!Directory.Exists(Master.SettlementsPath)) return null;

            string[] settlements = Directory.GetFiles(Master.SettlementsPath);
            foreach (string settlement in settlements)
            {
                SettlementFile settlementFile = Serializer.SerializeFromFile<SettlementFile>(settlement);
                if (settlementFile != null && settlementFile.Username == usernameToGet) return settlementFile;
            }

            return null;
        }

        public static SettlementFile[] GetAllSettlements()
        {
            List<SettlementFile> settlementList = new List<SettlementFile>();

            if (!Directory.Exists(Master.SettlementsPath)) return settlementList.ToArray();

            string[] settlements = Directory.GetFiles(Master.SettlementsPath);
            foreach (string settlement in settlements)
            {
                SettlementFile sf = Serializer.SerializeFromFile<SettlementFile>(settlement);
                if (sf != null) settlementList.Add(sf);
            }

            return settlementList.ToArray();
        }

        public static SettlementFile[] GetAllSettlementsFromUsername(string usernameToCheck)
        {
            List<SettlementFile> settlementList = new List<SettlementFile>();

            if (!Directory.Exists(Master.SettlementsPath)) return settlementList.ToArray();

            string[] settlements = Directory.GetFiles(Master.SettlementsPath);
            foreach (string settlement in settlements)
            {
                SettlementFile settlementFile = Serializer.SerializeFromFile<SettlementFile>(settlement);
                if (settlementFile != null && settlementFile.Username == usernameToCheck) settlementList.Add(settlementFile);
            }

            return settlementList.ToArray();
        }
    }
}
