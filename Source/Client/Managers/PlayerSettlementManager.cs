using RimWorld.Planet;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Shared;
using static Shared.CommonEnumerators;

namespace GameClient
{
    public static class PlayerSettlementManager
    {
        public static List<Settlement> PlayerSettlements { get; private set; } = new List<Settlement>();

        public static void ParsePacket(Packet packet)
        {
            if (packet?.Contents == null)
            {
                Logger.Error("Received null packet or packet contents in ParsePacket.");
                return;
            }

            SettlementData settlementData = Serializer.ConvertBytesToObject<SettlementData>(packet.Contents);

            switch (settlementData?.SettlementStepMode)
            {
                case SettlementStepMode.Add:
                    SpawnSingleSettlement(settlementData);
                    break;

                case SettlementStepMode.Remove:
                    RemoveSingleSettlement(settlementData);
                    break;

                default:
                    Logger.Warning($"Unknown SettlementStepMode received: {settlementData?.SettlementStepMode}");
                    break;
            }
        }

        public static void AddSettlements(OnlineSettlementFile[] settlements)
        {
            if (settlements == null) return;

            foreach (var settlementFile in settlements)
            {
                try
                {
                    Settlement settlement = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
                    settlement.Tile = settlementFile.Tile;
                    settlement.Name = $"{settlementFile.Owner}'s settlement";
                    settlement.SetFaction(PlanetManagerHelper.GetPlayerFactionFromGoodwill(settlementFile.Goodwill));

                    PlayerSettlements.Add(settlement);
                    Find.WorldObjects.Add(settlement);
                }
                catch (Exception e)
                {
                    Logger.Error($"Failed to build settlement at {settlementFile.Tile}. Reason: {e}");
                }
            }
        }

        public static void ClearAllSettlements()
        {
            PlayerSettlements.Clear();

            var settlements = Find.WorldObjects.Settlements
                .Where(fetch => FactionValues.playerFactions.Contains(fetch.Faction))
                .ToArray();

            foreach (var settlement in settlements)
            {
                Find.WorldObjects.Remove(settlement);
            }
        }

        public static void SpawnSingleSettlement(SettlementData newSettlementData)
        {
            if (ClientValues.isReadyToPlay)
            {
                try
                {
                    Settlement settlement = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
                    settlement.Tile = newSettlementData.Tile;
                    settlement.Name = $"{newSettlementData.Owner}'s settlement";
                    settlement.SetFaction(PlanetManagerHelper.GetPlayerFactionFromGoodwill(newSettlementData.Goodwill));

                    PlayerSettlements.Add(settlement);
                    Find.WorldObjects.Add(settlement);
                }
                catch (Exception e)
                {
                    Logger.Error($"Failed to spawn settlement at {newSettlementData.Tile}. Reason: {e}");
                }
            }
        }

        public static void RemoveSingleSettlement(SettlementData settlementData)
        {
            if (ClientValues.isReadyToPlay)
            {
                try
                {
                    var settlement = PlayerSettlements.FirstOrDefault(x => x.Tile == settlementData.Tile);

                    if (settlement != null)
                    {
                        PlayerSettlements.Remove(settlement);
                        Find.WorldObjects.Remove(settlement);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error($"Failed to remove settlement at {settlementData.Tile}. Reason: {e}");
                }
            }
        }
    }

    public static class PlayerSettlementManagerHelper
    {
        public static OnlineSettlementFile[] TempSettlements { get; private set; }

        public static void SetValues(ServerGlobalData serverGlobalData)
        {
            TempSettlements = serverGlobalData.PlayerSettlements;
        }
    }
}