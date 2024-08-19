using Shared;
using static Shared.CommonEnumerators;

namespace GameServer
{
    public static class NPCSettlementManager
    {
        public static void ParsePacket(ServerClient client, Packet packet)
        {
            NPCSettlementData data = Serializer.ConvertBytesToObject<NPCSettlementData>(packet.Contents);

            switch (data.StepMode)
            {
                case SettlementStepMode.Add:
                    ResponseShortcutManager.SendIllegalPacket(client, "Tried to execute unimplemented action");
                    break;

                case SettlementStepMode.Remove:
                    RemoveNPCSettlement(client, data.Details);
                    break;
            }
        }

        public static void RemoveNPCSettlement(ServerClient client, PlanetNPCSettlement settlement)
        {
            if (!Master.serverConfig.AllowNPCDestruction) return;
            else
            {
                if (!CheckIfSettlementFromTileExists(settlement.Tile)) ResponseShortcutManager.SendIllegalPacket(client, "Tried removing a non-existing NPC settlement");
                else
                {
                    DeleteSettlement(settlement);

                    BroadcastSettlementDeletion(settlement);

                    Logger.Warning($"[Delete NPC settlement] > {settlement.Tile} > {client.userFile.Username}");
                }
            }
        }

        private static void DeleteSettlement(PlanetNPCSettlement settlement)
        {
            List<PlanetNPCSettlement> finalSettlements = Master.worldValues.NPCSettlements.ToList();
            finalSettlements.Remove(GetSettlementFromTile(settlement.Tile));
            Master.worldValues.NPCSettlements = finalSettlements.ToArray();

            Master.SaveValueFile(ServerFileMode.World);
        }

        private static void BroadcastSettlementDeletion(PlanetNPCSettlement settlement)
        {
            NPCSettlementData data = new NPCSettlementData();
            data.StepMode = SettlementStepMode.Remove;
            data.Details = settlement;

            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.NPCSettlementPacket), data);
            NetworkHelper.SendPacketToAllClients(packet);
        }

        private static bool CheckIfSettlementFromTileExists(int tile)
        {
            foreach (PlanetNPCSettlement settlement in Master.worldValues.NPCSettlements.ToArray())
            {
                if (settlement.Tile == tile) return true;
            }

            return false;
        }

        private static PlanetNPCSettlement GetSettlementFromTile(int tile)
        {
            return Master.worldValues.NPCSettlements.FirstOrDefault(fetch => fetch.Tile == tile); ;
        }
    }
}
