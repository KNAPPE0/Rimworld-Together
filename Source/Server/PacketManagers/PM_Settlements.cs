using GameServer.Core;
using GameServer.Hooks.TCPNetwork;
using GameServer.Managers;
using GameServer.Misc;
using Shared;
using Shared.Files;
using Shared.Misc;
using System;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;
using static Shared.CommonEnumerators;

namespace GameServer.PacketManager
{
    public class PM_Settlements : PM_Base
    {
        // Settlement cache — invalidated on Add/Remove. Avoids per-call Directory.GetFiles + deserialize.
        private static readonly object SettlementCacheLock = new object();
        private static Dictionary<int, SettlementFile> _byTile;
        private static SettlementFile[] _allCached;

        public static void InvalidateSettlementCache()
        {
            lock (SettlementCacheLock)
            {
                _byTile = null;
                _allCached = null;
            }
        }

        private static SettlementFile[] EnsureCache()
        {
            lock (SettlementCacheLock)
            {
                if (_allCached != null) return _allCached;

                Dictionary<int, SettlementFile> dict = new Dictionary<int, SettlementFile>();
                List<SettlementFile> list = new List<SettlementFile>();
                try
                {
                    foreach (string path in Directory.GetFiles(Master.SettlementsPath))
                    {
                        try
                        {
                            SettlementFile sf = Serializer.SerializeFromFile<SettlementFile>(path);
                            if (sf == null) continue;
                            dict[sf.Tile] = sf;
                            list.Add(sf);
                        }
                        catch { /* one corrupt file shouldn't break the cache */ }
                    }
                }
                catch (Exception ex) { Printer.Error($"[Settlements] Cache build failed: {ex.Message}"); }

                _byTile = dict;
                _allCached = list.ToArray();
                return _allCached;
            }
        }

        [HandlesPacket(PacketHeader.SettlementManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_PlayerSettlement data = Serializer.ConvertBytesToObject<PKT_PlayerSettlement>(bytes);

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

        public static void AddSettlement(ServerClient client, PKT_PlayerSettlement settlementData)
        {
            if (CheckIfTileIsInUse(settlementData._settlementFile.Tile))
            {
                ResponseShortcutManager.SendIllegalPacket(client, $"Player {client.UserFile.Username} attempted to add a settlement at tile {settlementData._settlementFile.Tile}, but that tile already has a settlement");
                return;
            }

            SettlementFile settlementFile = new SettlementFile();
            settlementFile.Tile = settlementData._settlementFile.Tile;
            settlementFile.Username = client.UserFile.Username;
            settlementData._settlementFile = settlementFile;

            Serializer.SerializeToFile(Path.Combine(Master.SettlementsPath, settlementFile.Tile + CommonValues.DefaultSaveFormat), settlementFile);
            InvalidateSettlementCache();

            settlementData._stepMode = SettlementStepMode.Add;
            foreach (ServerClient cClient in ServerNetwork.GetConnectedClients())
            {
                if (cClient == client) continue;
                settlementData._settlementFile.Goodwill = PM_Goodwills.GetSettlementGoodwill(cClient, settlementFile);
                cClient.Listener.EnqueuePacket(PacketHeader.SettlementManager, settlementData);
            }

            InformationDisplayer.DisplayAddSettlement(settlementFile.Tile.ToString());
        }

        public static void RemoveSettlement(ServerClient client, PKT_PlayerSettlement settlementData)
        {
            // Was missing the early-return — control fell through and NREs on settlementFile.Username below.
            if (!CheckIfTileIsInUse(settlementData._settlementFile.Tile))
            {
                ResponseShortcutManager.SendIllegalPacket(client, $"Settlement at tile {settlementData._settlementFile.Tile} was attempted to be removed, but the tile doesn't contain a settlement");
                return;
            }

            SettlementFile settlementFile = GetSettlementFileFromTile(settlementData._settlementFile.Tile);
            if (settlementFile == null) return;

            if (client != null && settlementFile.Username != client.UserFile.Username)
            {
                ResponseShortcutManager.SendIllegalPacket(client, $"Settlement at tile {settlementData._settlementFile.Tile} attempted to be removed by " +
                    $"{client.UserFile.Username}, but {settlementFile.Username} owns the settlement");
                return;
            }

            File.Delete(Path.Combine(Master.SettlementsPath, settlementFile.Tile + CommonValues.DefaultSaveFormat));
            InvalidateSettlementCache();
            InformationDisplayer.DisplayRemoveSettlement(settlementFile.Tile.ToString());

            settlementData._stepMode = SettlementStepMode.Remove;
            ServerNetwork.SendPacketToAllClients(PacketHeader.SettlementManager, settlementData, client);
        }

        public static bool CheckIfTileIsInUse(int tileToCheck)
        {
            EnsureCache();
            lock (SettlementCacheLock) { return _byTile.ContainsKey(tileToCheck); }
        }

        public static SettlementFile GetSettlementFileFromTile(int tileToGet)
        {
            EnsureCache();
            lock (SettlementCacheLock)
            {
                return _byTile.TryGetValue(tileToGet, out SettlementFile sf) ? sf : null;
            }
        }

        public static SettlementFile GetSettlementFileFromUsername(string usernameToGet)
        {
            if (string.IsNullOrEmpty(usernameToGet)) return null;
            SettlementFile[] all = EnsureCache();
            foreach (SettlementFile sf in all)
                if (sf != null && string.Equals(sf.Username, usernameToGet, StringComparison.OrdinalIgnoreCase)) return sf;
            return null;
        }

        public static SettlementFile[] GetAllSettlements()
        {
            return EnsureCache();
        }

        public static SettlementFile[] GetAllSettlementsFromUsername(string usernameToCheck)
        {
            if (string.IsNullOrEmpty(usernameToCheck)) return Array.Empty<SettlementFile>();
            SettlementFile[] all = EnsureCache();
            List<SettlementFile> match = new List<SettlementFile>();
            foreach (SettlementFile sf in all)
                if (sf != null && string.Equals(sf.Username, usernameToCheck, StringComparison.OrdinalIgnoreCase)) match.Add(sf);
            return match.ToArray();
        }

        public static List<SettlementFile> GetSettlementsFromGoodwill(ServerClient client)
        {
            List<SettlementFile> tempList = new List<SettlementFile>();
            string caller = client?.UserFile?.Username;
            foreach (SettlementFile settlement in PM_Settlements.GetAllSettlements())
            {
                if (settlement == null) continue;
                if (string.Equals(settlement.Username, caller, StringComparison.OrdinalIgnoreCase)) continue;

                // Was: `file.Username = settlement.Username` written twice. Harmless typo.
                SettlementFile file = new SettlementFile
                {
                    Tile = settlement.Tile,
                    Username = settlement.Username,
                    Goodwill = PM_Goodwills.GetSettlementGoodwill(client, settlement)
                };
                tempList.Add(file);
            }

            return tempList;
        }
    }
}
