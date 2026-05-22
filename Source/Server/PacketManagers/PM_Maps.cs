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

        // KMH 26.5.20.1 ANTI-CHEAT: previously, this handler trusted whatever
        // MapFile the client sent — tile id, username, wealth, colonist
        // count, everything. A modded client could:
        //   1. Overwrite another player's tile (grief).
        //   2. Claim arbitrary wealth → dominate the leaderboard from a
        //      brand-new colony.
        //   3. Spoof colonist counts / play time.
        // We now (a) ignore unauthenticated requests, (b) force the
        // username to the calling client's verified identity, (c) verify
        // the tile actually belongs to one of their settlements, and (d)
        // clamp the leaderboard-feeding numeric fields to sane bounds.
        //
        // RimWorld's max realistic late-game wealth is around 500k–1M; cap
        // at 5M to leave headroom for crazy modded playthroughs without
        // letting a cheater pin the leaderboard at int.MaxValue.
        private const int MaxAllowedWealth = 5_000_000;
        private const int MaxAllowedColonists = 100;
        // MapFile.GameTicks is int — cap at int.MaxValue (effectively 68 in-game years).
        private const int MaxAllowedGameTicks = int.MaxValue;
        // MapFile.RealPlayTimeSeconds is double — 5 years is well above any realistic play time.
        private const double MaxAllowedRealPlaySeconds = 60.0 * 60.0 * 24.0 * 365.0 * 5.0;

        public static void SaveUserMap(ServerClient client, PKT_Map data)
        {
            // Reject malformed packets early.
            if (data?.File == null) return;

            string callerUsername = client?.UserFile?.Username;
            if (string.IsNullOrEmpty(callerUsername)) return;

            int tile = data.File.Tile;
            if (tile < 0) return;

            // KMH 26.5.20.1 ANTI-CHEAT: server-authoritative ownership check.
            // The tile must resolve to a settlement (or site) owned by the
            // calling client. This blocks the "send map for a tile you
            // don't own" overwrite attack.
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

            // KMH 26.5.20.1 ANTI-CHEAT: override the username field with the
            // authenticated identity, regardless of what the client sent.
            data.File.Username = callerUsername;

            // KMH 26.5.20.1 ANTI-CHEAT: clamp leaderboard-feeding fields
            // against forged values. Negative is also obviously bogus.
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
            // KMH 26.5.20.1: One stat() syscall instead of a full directory
            // listing + per-path string-allocation + LINQ scan. Maps are
            // stored by tile-id filename, so File.Exists is the right test.
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
