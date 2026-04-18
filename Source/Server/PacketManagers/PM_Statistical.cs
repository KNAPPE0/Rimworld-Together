using GameServer.Core;
using GameServer.Hooks.TCPNetwork;
using Shared;
using Shared.Files;
using Shared.Misc;
using System;
using System.IO;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;

namespace GameServer.PacketManager
{
    public static class PM_Statistical
    {
        public static void HandleStatsRequest(ServerClient client, PKT_Information data)
        {
            try
            {
                int tile = data._settlementTile;
                if (tile < 0) return;

                SettlementFile settlement = PM_Settlements.GetSettlementFileFromTile(tile);

                if (settlement == null)
                {
                    data._settlementStats = null;
                    client.Listener.EnqueuePacket(PacketHeader.InformationManager, data);
                    return;
                }

                MapStatsFile stats = new MapStatsFile();

                stats.Tile = tile;
                stats.Username = string.IsNullOrEmpty(settlement.Username) ? "Unknown" : settlement.Username;
                stats.SettlementName = string.IsNullOrEmpty(settlement.Name) ? "Unknown" : settlement.Name;

                // Try to load map file for additional stats
                try
                {
                    MapFile mapFile = PM_Maps.GetMapFromTile(tile);
                    if (mapFile != null)
                    {
                        stats.Wealth = mapFile.Wealth;
                        stats.WealthExact = mapFile.WealthExact;
                        stats.GameTicks = mapFile.GameTicks;
                        stats.ColonistCount = mapFile.ColonistCount;
                        stats.FactionHumanCount = mapFile.FactionHumanCount;
                        stats.NonFactionHumanCount = mapFile.NonFactionHumanCount;
                        stats.FactionAnimalCount = mapFile.FactionAnimalCount;
                        stats.NonFactionAnimalCount = mapFile.NonFactionAnimalCount;
                        stats.FactionThingCount = mapFile.FactionThingCount;
                        stats.NonFactionThingCount = mapFile.NonFactionThingCount;
                        stats.FactionName = mapFile.FactionName ?? "Unknown";
                        stats.RealPlayTimeSeconds = mapFile.RealPlayTimeSeconds;
                        stats.RealPlayTimeInteractingSeconds = mapFile.RealPlayTimeInteractingSeconds;
                        stats.LastSavedUtcTicks = mapFile.LastSavedUtcTicks;
                    }
                }
                catch { }

                // Check if player is online by matching settlement owner username
                data._isPlayerOnline = false;
                foreach (ServerClient sc in ServerNetwork.GetConnectedClients())
                {
                    if (sc.UserFile != null && sc.UserFile.Username == settlement.Username)
                    {
                        data._isPlayerOnline = true;
                        break;
                    }
                }

                data._settlementStats = stats;
                client.Listener.EnqueuePacket(PacketHeader.InformationManager, data);
            }
            catch (Exception e)
            {
                Printer.Error($"[PM_Statistical] HandleStatsRequest failed: {e}");
            }
        }
    }
}
