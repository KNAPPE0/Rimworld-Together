using GameServer.Core;
using GameServer.Hooks.TCPNetwork;
using GameServer.Managers;
using Shared;
using Shared.Details.Planet;
using Shared.Files.Configs;
using Shared.Misc;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;

namespace GameServer.PacketManager
{
    public class PM_Pollution : PM_Base
    {
        [HandlesPacket(PacketHeader.PollutionManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            // KMH 26.5.22.1: Two-layer guard. Global toggle first (fast
            // path), then optional per-player cooldown if the admin
            // configured one. The cooldown defaults to -1 ("not used")
            // so existing installs see no behavioural change — only
            // operators who set ActionConfig.PollutionCooldown > 0 opt
            // into the rate limit. Ported from upstream's pollution
            // security check, adapted to KMH's existing IsEnabled +
            // optional-cooldown idiom.
            if (!Master.ActionConfigs.EnablePollutionSpread)
            {
                ResponseShortcutManager.SendIllegalPacket(client, "Tried to use disabled feature!");
                return;
            }

            double cooldownSeconds = Master.ActionConfigs.PollutionCooldown;
            if (cooldownSeconds > 0
                && !PlayerCooldown.CheckIfCanPollute(client.UserFile, true, cooldownSeconds))
            {
                ResponseShortcutManager.SendUnavailablePacket(client);
                return;
            }

            PKT_Pollution data = Serializer.ConvertBytesToObject<PKT_Pollution>(bytes);
            if (data == null) return;
            AddPollutionToTile(data, client, true);

            // KMH 26.5.22.1: Stamp the cooldown after a successful
            // spread (matches the road-cooldown order — never lock the
            // user out for a rejected request).
            if (cooldownSeconds > 0)
            {
                client.UserFile.Cooldowns.SetPollutionTimer(
                    Shared.TimeConverter.GetCurrentTimeToEpoch(),
                    client.UserFile);
            }
        }

        public static void AddPollutionToTile(PKT_Pollution data, ServerClient client, bool shouldBroadcast)
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
                    Master.WorldValues.PollutedTiles = existingPollutedTiles;
                }

                if (shouldBroadcast) ServerNetwork.SendPacketToAllClients(PacketHeader.PollutionManager, data, client);

                PlanetConfigFile.Save(PlanetConfigFile.SavePath, Master.WorldValues);
            }
            catch { Printer.Warning($"Could not add pollution to tile {data}. Coming from {client.UserFile.Username}"); }
        }
    }
}
