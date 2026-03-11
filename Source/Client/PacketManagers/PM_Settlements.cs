using GameClient.Hooks.TCPNetwork;
using GameClient.Managers;
using GameClient.Misc;
using GameClient.WorldObjects;
using RimWorld;
using RimWorld.Planet;
using Shared;
using Shared.Files;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using TCPNetwork;
using TCPNetwork.Packets;
using Verse;
using static Shared.CommonEnumerators;

namespace GameClient.PacketManagers
{
    public static class PM_Settlements
    {
        public static List<RTSettlement> PlayerSettlements { get; set; } = new List<RTSettlement>();

        [HandlesPacket(PacketHeader.SettlementManager)]
        private static void ParsePacket(byte[] bytes)
        {
            PlayerSettlementData data = Serializer.ConvertBytesToObject<PlayerSettlementData>(bytes);
            if (data == null) return;

            switch (data._stepMode)
            {
                case SettlementStepMode.Add:
                    SpawnSingleSettlement(data._settlementFile);
                    break;

                case SettlementStepMode.Remove:
                    RemoveSingleSettlement(data._settlementFile);
                    break;
            }
        }

        public static void AddSettlements(SettlementFile[] settlements)
        {
            if (settlements == null) return;

            foreach (SettlementFile toAdd in settlements)
            {
                SpawnSingleSettlement(toAdd);
            }
        }

        public static void ClearAllSettlements()
        {
            PlayerSettlements.Clear();

            WorldObject[] settlements = Finder.GetAllRTSettlements()?.ToArray() ?? Array.Empty<WorldObject>();

            foreach (WorldObject settlement in settlements)
            {
                if (settlement == null) continue;

                SettlementFile toRemove = new SettlementFile();
                toRemove.Tile = settlement.Tile;

                RemoveSingleSettlement(toRemove);
            }
        }

        public static void SpawnSingleSettlement(SettlementFile toAdd)
        {
            if (toAdd == null) return;

            try
            {
                WorldObjectDef def = DefDatabase<WorldObjectDef>.AllDefs.First(fetch => fetch.defName == "RTSettlement");
                RTSettlement settlement = (RTSettlement)WorldObjectMaker.MakeWorldObject(def);
                settlement.Tile = toAdd.Tile;

                if (!string.IsNullOrWhiteSpace(toAdd.Name))
                    settlement.Name = toAdd.Name;
                else
                    settlement.Name = $"{toAdd.Username}'s settlement";

                settlement.SetFaction(PlanetManagerHelper.GetPlayerFactionFromGoodwill(toAdd.Goodwill));

                PlayerSettlements.Add(settlement);
                Find.WorldObjects.Add(settlement);
            }
            catch (Exception e)
            {
                Printer.Error($"Failed to spawn settlement at {toAdd.Tile}. Reason: {e}");
            }
        }

        public static void RemoveSingleSettlement(SettlementFile toRemove)
        {
            if (toRemove == null) return;

            try
            {
                RTSettlement toGet = Finder.GetRTSettlementFromTile(toRemove.Tile);
                if (toGet == null) return;

                PlayerSettlements.Remove(toGet);

                Find.WorldObjects.Remove(toGet);
                toGet.Destroy();
            }
            catch (Exception e)
            {
                Printer.Error($"Failed to remove settlement at {toRemove.Tile}. Reason: {e}");
            }
        }

        public static void RegenSettlement(RTSettlement _)
        {
            if (_ == null) return;

            SettlementFile file = new SettlementFile();
            file.Tile = _.Tile;

            string label = _.Label ?? string.Empty;

            if (label.EndsWith("'s settlement"))
                file.Username = label.Replace("'s settlement", "").Trim();
            else
                file.Username = string.Empty;

            file.Name = label;

            if (_.Faction == SessionHandler.EnemyFaction) file.Goodwill = Goodwill.Enemy;
            else if (_.Faction == SessionHandler.AllyFaction) file.Goodwill = Goodwill.Ally;
            else if (_.Faction == SessionHandler.GuildFaction) file.Goodwill = Goodwill.Guild;
            else file.Goodwill = Goodwill.Neutral;

            RemoveSingleSettlement(file);
            SpawnSingleSettlement(file);
        }

        public static void SendNewPlayerSettlement(int settlementTile)
        {
            PlayerSettlementData settlementData = new PlayerSettlementData();
            settlementData._settlementFile.Tile = settlementTile;

            settlementData._settlementFile.Name = TryGetLocalColonyName(settlementTile);

            settlementData._stepMode = SettlementStepMode.Add;

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.SettlementManager, settlementData);
        }

        public static void AbandonSettlement(int settlementTile)
        {
            PlayerSettlementData settlementData = new PlayerSettlementData();
            settlementData._settlementFile.Tile = settlementTile;
            settlementData._stepMode = SettlementStepMode.Remove;

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.SettlementManager, settlementData);

            PM_Saves.ForceSave();
        }

        private static string TryGetLocalColonyName(int tile)
        {
            try
            {
                Map map = Find.Maps.FirstOrDefault(m => m != null && m.IsPlayerHome && m.Tile == tile);
                if (map != null && map.Parent != null && !string.IsNullOrWhiteSpace(map.Parent.Label))
                    return map.Parent.Label;

                WorldObject obj = Find.WorldObjects.AllWorldObjects.FirstOrDefault(o => o != null && o.Tile == tile);
                if (obj != null && !string.IsNullOrWhiteSpace(obj.Label))
                    return obj.Label;
            }
            catch { }

            return string.Empty;
        }
    }

    public static class PlayerSettlementManagerHelper
    {
        public static SettlementFile[] tempSettlements;

        public static void SetValues(ServerGlobalData serverGlobalData)
        {
            tempSettlements = serverGlobalData._playerSettlements;
        }
    }
}