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
using static Shared.CommonEnumerators;

namespace GameServer.PacketManager
{
    public class PM_Npcs : PM_Base
    {
        [HandlesPacket(PacketHeader.NPCManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            if (!Master.ActionConfigs.EnableNPCDestruction)
            {
                ResponseShortcutManager.SendIllegalPacket(client, "Tried to use disabled feature!");
                return;
            }

            NPCSettlementData data = Serializer.ConvertBytesToObject<NPCSettlementData>(bytes);

            switch (data._stepMode)
            {
                case SettlementStepMode.Add:
                    ResponseShortcutManager.SendIllegalPacket(client, "Tried to execute unimplemented action");
                    break;

                case SettlementStepMode.Remove:
                    RemoveNPCSettlement(client, data._settlementData);
                    break;
            }
        }

        public static void RemoveNPCSettlement(ServerClient client, NPCSettlementDetail settlement)
        {
            if (!NPCSettlementManagerHelper.CheckIfSettlementFromTileExists(settlement.Tile))
            {
                ResponseShortcutManager.SendIllegalPacket(client, "Tried removing a non-existing NPC settlement");
            }

            else
            {
                DeleteSettlement(settlement);

                BroadcastSettlementDeletion(settlement);

                Printer.Warning($"[Delete NPC settlement] > {settlement.Tile} > {client.UserFile.Username}");
            }
        }

        private static void DeleteSettlement(NPCSettlementDetail settlement)
        {
            List<NPCSettlementDetail> finalSettlements = Master.WorldValues.NPCSettlements.ToList();
            finalSettlements.Remove(NPCSettlementManagerHelper.GetSettlementFromTile(settlement.Tile));
            Master.WorldValues.NPCSettlements = finalSettlements.ToArray();
            PlanetConfigFile.Save(PlanetConfigFile.SavePath, Master.WorldValues, true);
        }

        private static void BroadcastSettlementDeletion(NPCSettlementDetail settlement)
        {
            NPCSettlementData data = new NPCSettlementData();
            data._stepMode = SettlementStepMode.Remove;
            data._settlementData = settlement;

            ServerNetwork.SendPacketToAllClients(PacketHeader.NPCManager, data);
        }
    }

    public class NPCSettlementManagerHelper
    {
        public static bool CheckIfSettlementFromTileExists(int tile)
        {
            foreach (NPCSettlementDetail settlement in Master.WorldValues.NPCSettlements.ToArray())
            {
                if (settlement.Tile == tile) return true;
            }

            return false;
        }

        public static NPCSettlementDetail GetSettlementFromTile(int tile)
        {
            return Master.WorldValues.NPCSettlements.FirstOrDefault(fetch => fetch.Tile == tile); ;
        }
    }
}
