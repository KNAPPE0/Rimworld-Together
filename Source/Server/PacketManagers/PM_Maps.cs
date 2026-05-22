using GameServer.Core;
using GameServer.Managers;
using GameServer.Misc;
using Shared;
using Shared.Files;
using Shared.Misc;
using System;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;

namespace GameServer.PacketManager
{
    public class PM_Maps : PM_Base
    {
        [HandlesPacket(PacketHeader.MapManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_Map data = Serializer.ConvertBytesToObject<PKT_Map>(bytes);

            SaveUserMap(client, data);
        }

        // Server-authoritative caps — block forged MapFiles from pinning the leaderboard.
        // 5M wealth leaves headroom for modded late-game without int.MaxValue exploits.
        private const int MaxAllowedWealth = 5_000_000;
        private const int MaxAllowedColonists = 100;
        private const int MaxAllowedGameTicks = int.MaxValue;
        // 5 in-game years — well above any realistic play time.
        private const double MaxAllowedRealPlaySeconds = 60.0 * 60.0 * 24.0 * 365.0 * 5.0;

        public static void SaveUserMap(ServerClient client, PKT_Map data)
        {
            // Reject malformed packets early.
            if (data?.File == null) return;

            string callerUsername = client?.UserFile?.Username;
            if (string.IsNullOrEmpty(callerUsername)) return;

            int tile = data.File.Tile;
            if (tile < 0) return;

            // Tile must resolve to a settlement/site owned by caller — blocks overwrite griefing.
            SettlementFile settlement = PM_Settlements.GetSettlementFileFromTile(tile);
            Shared.Files.Sites.SiteFile site = SiteManagerHelper.GetSiteFileFromTile(tile);
            string ownerOnServer = settlement?.Username ?? site?.Username;

            if (string.IsNullOrEmpty(ownerOnServer))
            {
                Printer.Warning($"[Maps] {callerUsername} tried to save map for orphan tile {tile} (no settlement/site).");
                return;
            }
            if (!string.Equals(ownerOnServer, callerUsername, StringComparison.OrdinalIgnoreCase))
            {
                Printer.Warning($"[Maps] {callerUsername} attempted to overwrite map for tile {tile} owned by {ownerOnServer}. Rejected.");
                return;
            }

            // Override client-supplied username + clamp leaderboard fields against forgery.
            data.File.Username = callerUsername;

            if (data.File.Wealth < 0) data.File.Wealth = 0;
            if (data.File.Wealth > MaxAllowedWealth) data.File.Wealth = MaxAllowedWealth;
            if (data.File.WealthExact < 0) data.File.WealthExact = 0;
            if (data.File.WealthExact > MaxAllowedWealth) data.File.WealthExact = MaxAllowedWealth;
            if (data.File.ColonistCount < 0) data.File.ColonistCount = 0;
            if (data.File.ColonistCount > MaxAllowedColonists) data.File.ColonistCount = MaxAllowedColonists;
            if (data.File.GameTicks < 0) data.File.GameTicks = 0;
            // GameTicks is int — its own type cap is MaxAllowedGameTicks.
            if (data.File.RealPlayTimeSeconds < 0) data.File.RealPlayTimeSeconds = 0;
            if (data.File.RealPlayTimeSeconds > MaxAllowedRealPlaySeconds) data.File.RealPlayTimeSeconds = MaxAllowedRealPlaySeconds;
            if (data.File.RealPlayTimeInteractingSeconds < 0) data.File.RealPlayTimeInteractingSeconds = 0;
            if (data.File.RealPlayTimeInteractingSeconds > data.File.RealPlayTimeSeconds)
                data.File.RealPlayTimeInteractingSeconds = data.File.RealPlayTimeSeconds;

            data.File.LastSavedUtcTicks = System.DateTime.UtcNow.Ticks; // server time, not client

            Serializer.SerializeToFile(Path.Combine(Master.MapsPath, data.File.Tile + CommonValues.DefaultSaveFormat), data.File);
            PM_Leaderboard.UpdateLeaderboard(client, data.File);
            InformationDisplayer.DisplaySaveMap(client);
        }

        public static string[] GetAllMaps() { return Directory.GetFiles(Master.MapsPath); }

        public static bool CheckIfMapExists(int mapTileToCheck)
        {
            // Filename = tile id; one stat() beats listing + LINQ scan.
            return File.Exists(Path.Combine(Master.MapsPath, mapTileToCheck + CommonValues.DefaultSaveFormat));
        }

        public static MapFile GetMapFromTile(int mapTileToGet)
        {
            string path = Path.Combine(Master.MapsPath, mapTileToGet + CommonValues.DefaultSaveFormat);
            if (File.Exists(path)) return Serializer.SerializeFromFile<MapFile>(path);
            else return null;
        }
    }
}
