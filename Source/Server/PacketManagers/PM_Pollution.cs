using GameServer.Core;
using GameServer.Hooks.TCPNetwork;
using GameServer.Managers;
using GameServer.Misc;
using Shared;
using Shared.Details.Planet;
using Shared.Files;
using Shared.Files.Configs;
using Shared.Misc;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;

namespace GameServer.PacketManager
{
    public class PM_Pollution : PM_Base
    {
        [HandlesPacket(PacketHeader.PollutionManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            if (!Master.ActionConfigs.EnablePollutionSpread)
            {
                ResponseShortcutManager.SendIllegalPacket(client, "Tried to use disabled feature!");
                return;
            }

            PollutionData data = Serializer.ConvertBytesToObject<PollutionData>(bytes);
            AddPollutionToTile(data, client, true);
        }

        public static void AddPollutionToTile(PollutionData data, ServerClient client, bool shouldBroadcast)
        {
            try
            {
                bool isNewPollutedTile = false;

                PollutionDetail toSearch = Master.WorldValues.PollutedTiles.FirstOrDefault(T => T.Tile == data._pollutionData.Tile);
                if (toSearch == null)
                {
                    toSearch = new PollutionDetail();
                    isNewPollutedTile = true;
                }

                toSearch.Tile = data._pollutionData.Tile;
                toSearch.Quantity += data._pollutionData.Quantity;

                if (isNewPollutedTile)
                {
                    List<PollutionDetail> existingPollutedTiles = Master.WorldValues.PollutedTiles.ToList();
                    existingPollutedTiles.Add(toSearch);
                    Master.WorldValues.PollutedTiles = existingPollutedTiles.ToArray();
                }

                if (shouldBroadcast) ServerNetwork.SendPacketToAllClients(PacketHeader.PollutionManager, data, client);

                PlanetConfigFile.Save(PlanetConfigFile.SavePath, Master.WorldValues, true);
            }
            catch { Printer.Warning($"Could not add pollution to tile {data}. Coming from {client.UserFile.Username}"); }
        }
    }
}
