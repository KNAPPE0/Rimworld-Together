using RimWorld.Planet;
using RimWorld;
using System;
using System.Linq;
using Verse;
using Shared;
using static Shared.CommonEnumerators;

namespace GameClient
{
    public static class NPCSettlementManager
    {
        public static void ParsePacket(Packet packet)
        {
            if (packet == null)
            {
                Logger.Error("Received a null packet in NPCSettlementManager.");
                return;
            }

            NPCSettlementData data = Serializer.ConvertBytesToObject<NPCSettlementData>(packet.Contents);
            if (data == null)
            {
                Logger.Error("Failed to deserialize NPCSettlementData from packet.");
                return;
            }

            switch (data.StepMode)
            {
                case SettlementStepMode.Add:
                    // Logic to add settlements could be added here if needed
                    break;

                case SettlementStepMode.Remove:
                    if (data.Details != null)
                    {
                        RemoveNPCSettlementFromPacket(data.Details);
                    }
                    else
                    {
                        Logger.Warning("Details are null in NPCSettlementData for Remove operation.");
                    }
                    break;

                default:
                    Logger.Warning($"Unknown SettlementStepMode received: {data.StepMode}");
                    break;
            }
        }

        public static void AddSettlements(PlanetNPCSettlement[] settlements)
        {
            if (settlements == null)
            {
                Logger.Warning("Attempted to add settlements, but the settlements array is null.");
                return;
            }

            foreach (PlanetNPCSettlement settlement in settlements)
            {
                SpawnSettlement(settlement);
            }
        }

        public static void SpawnSettlement(PlanetNPCSettlement toAdd)
        {
            if (toAdd == null)
            {
                Logger.Error("Cannot spawn a null settlement.");
                return;
            }

            try
            {
                Settlement settlement = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
                settlement.Tile = toAdd.Tile;
                settlement.Name = toAdd.Name ?? "Unnamed Settlement";

                // Handle faction assignment properly
                var faction = PlanetManagerHelper.GetNPCFactionFromDefName(toAdd.FactionDefName);
                if (faction != null)
                {
                    settlement.SetFaction(faction);
                }
                else
                {
                    Logger.Warning($"Faction not found for settlement: {toAdd.Name ?? "Unnamed"} at tile {toAdd.Tile}");
                }

                Find.WorldObjects.Add(settlement);
            }
            catch (Exception e)
            {
                Logger.Error($"Failed to build NPC settlement at {toAdd.Tile}. Reason: {e}");
            }
        }

        public static void ClearAllSettlements()
        {
            var settlements = Find.WorldObjects.Settlements
                .Where(fetch => fetch.Faction != null && !FactionValues.playerFactions.Contains(fetch.Faction) &&
                                fetch.Faction != Faction.OfPlayer)
                .ToArray();

            foreach (var settlement in settlements)
            {
                RemoveSettlement(settlement, null);
            }

            var destroyedSettlements = Find.WorldObjects.DestroyedSettlements
                .Where(fetch => fetch.Faction != null && !FactionValues.playerFactions.Contains(fetch.Faction) &&
                                fetch.Faction != Faction.OfPlayer)
                .ToArray();

            foreach (var destroyedSettlement in destroyedSettlements)
            {
                RemoveSettlement(null, destroyedSettlement);
            }
        }

        public static void RemoveNPCSettlementFromPacket(PlanetNPCSettlement data)
        {
            if (data == null)
            {
                Logger.Error("Cannot remove a null NPC settlement.");
                return;
            }

            var toRemove = Find.World.worldObjects.Settlements
                .FirstOrDefault(fetch => fetch.Tile == data.Tile && fetch.Faction != Faction.OfPlayer);

            if (toRemove != null)
            {
                RemoveSettlement(toRemove, null);
            }
            else
            {
                Logger.Warning($"No settlement found at tile {data.Tile} to remove.");
            }
        }

        public static void RemoveSettlement(Settlement settlement, DestroyedSettlement destroyedSettlement)
        {
            if (settlement != null)
            {
                NPCSettlementManagerHelper.LatestRemovedSettlement = settlement;
                Find.WorldObjects.Remove(settlement);
            }
            else if (destroyedSettlement != null)
            {
                Find.WorldObjects.Remove(destroyedSettlement);
            }
            else
            {
                Logger.Warning("Both settlement and destroyedSettlement are null in RemoveSettlement.");
            }
        }

        public static void RequestSettlementRemoval(Settlement settlement)
        {
            if (settlement == null)
            {
                Logger.Error("Cannot request removal of a null settlement.");
                return;
            }

            if (NPCSettlementManagerHelper.LatestRemovedSettlement == settlement) return;

            var data = new NPCSettlementData
            {
                StepMode = SettlementStepMode.Remove,
                Details = new PlanetNPCSettlement
                {
                    Tile = settlement.Tile
                }
            };

            var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.NPCSettlementPacket), data);
            Network.Listener.EnqueuePacket(packet);
        }
    }

    public static class NPCSettlementManagerHelper
    {
        public static PlanetNPCSettlement[] TempNPCSettlements { get; set; } = Array.Empty<PlanetNPCSettlement>();
        public static Settlement LatestRemovedSettlement { get; set; }

        public static void SetValues(ServerGlobalData serverGlobalData)
        {
            TempNPCSettlements = serverGlobalData?.NpcSettlements ?? Array.Empty<PlanetNPCSettlement>();
        }
    }
}