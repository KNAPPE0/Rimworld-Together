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
        // KMH 26.5.20.1: In-memory cache for settlement files.
        // Pre-cache, every lookup helper (CheckIfTileIsInUse,
        // GetSettlementFileFromTile, GetAllSettlements, etc.) did a full
        // Directory.GetFiles + deserialize-every-file scan. These get called
        // on EVERY settlement add (to validate the tile isn't in use), on
        // EVERY goodwill broadcast, on EVERY world refresh — and several
        // dialogs poll them. On a server with 50 settlements that's 50
        // disk reads + 50 JSON parses per call.
        //
        // The cache is invalidated on AddSettlement / RemoveSettlement
        // (the only paths that write a settlement file). All other helpers
        // are pure reads through the cache.
        private static readonly object SettlementCacheLock = new object();
        private static Dictionary<int, SettlementFile> _byTile; // tile → file
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
                    string[] settlements = Directory.GetFiles(Master.SettlementsPath);
                    foreach (string path in settlements)
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
            if (CheckIfTileIsInUse(settlementData._settlementFile.Tile)) ResponseShortcutManager.SendIllegalPacket(client, $"Player {client.UserFile.Username} attempted to add a settlement at tile {settlementData._settlementFile.Tile}, but that tile already has a settlement");
            else
            {
                SettlementFile settlementFile = new SettlementFile();
                settlementFile.Tile = settlementData._settlementFile.Tile;
                settlementFile.Username = client.UserFile.Username;
                settlementFile.Username = client.UserFile.Username;
                settlementData._settlementFile = settlementFile;

                Serializer.SerializeToFile(Path.Combine(Master.SettlementsPath, settlementFile.Tile + CommonValues.DefaultSaveFormat), settlementFile);
                InvalidateSettlementCache();

                settlementData._stepMode = SettlementStepMode.Add;
                foreach (ServerClient cClient in ServerNetwork.GetConnectedClients())
                {
                    if (cClient == client) continue;
                    else
                    {
                        settlementData._settlementFile.Goodwill = PM_Goodwills.GetSettlementGoodwill(cClient, settlementFile);

                        cClient.Listener.EnqueuePacket(PacketHeader.SettlementManager, settlementData);
                    }
                }

                InformationDisplayer.DisplayAddSettlement(settlementFile.Tile.ToString());
            }
        }

        public static void RemoveSettlement(ServerClient client, PKT_PlayerSettlement settlementData)
        {
            if (!CheckIfTileIsInUse(settlementData._settlementFile.Tile)) ResponseShortcutManager.SendIllegalPacket(client, $"Settlement at tile {settlementData._settlementFile.Tile} was attempted to be removed, but the tile doesn't contain a settlement");

            SettlementFile settlementFile = GetSettlementFileFromTile(settlementData._settlementFile.Tile);

            if (client != null)
            {
                if (settlementFile.Username != client.UserFile.Username)
                {
                    ResponseShortcutManager.SendIllegalPacket(client, $"Settlement at tile {settlementData._settlementFile.Tile} attempted to be removed by " +
                        $"{client.UserFile.Username}, but {settlementFile.Username} owns the settlement");
                }

                else
                {
                    Delete();
                    SendRemovalSignal();
                }
            }

            else
            {
                Delete();
                SendRemovalSignal();
            }

            void Delete()
            {
                File.Delete(Path.Combine(Master.SettlementsPath, settlementFile.Tile + CommonValues.DefaultSaveFormat));
                InvalidateSettlementCache();

                InformationDisplayer.DisplayRemoveSettlement(settlementFile.Tile.ToString());
            }

            void SendRemovalSignal()
            {
                settlementData._stepMode = SettlementStepMode.Remove;

                ServerNetwork.SendPacketToAllClients(PacketHeader.SettlementManager, settlementData, client);
            }
        }

        // KMH 26.5.20.1: All helpers now read through the cache instead of
        // re-scanning disk. The cache lives until AddSettlement /
        // RemoveSettlement / explicit InvalidateSettlementCache call.

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
                if (sf != null && sf.Username == usernameToGet) return sf;
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
                if (sf != null && sf.Username == usernameToCheck) match.Add(sf);
            return match.ToArray();
        }

        public static List<SettlementFile> GetSettlementsFromGoodwill(ServerClient client)
        {
            List<SettlementFile> tempList = new List<SettlementFile>();
            foreach (SettlementFile settlement in PM_Settlements.GetAllSettlements())
            {
                SettlementFile file = new SettlementFile();

                if (settlement.Username == client.UserFile.Username) continue;
                else
                {
                    file.Tile = settlement.Tile;
                    file.Username = settlement.Username;
                    file.Username = settlement.Username;
                    file.Goodwill = PM_Goodwills.GetSettlementGoodwill(client, settlement);

                    tempList.Add(file);
                }
            }

            return tempList;
        }
    }
}
