using RimWorld;
using RimWorld.Planet;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using static Shared.CommonEnumerators;

namespace GameClient
{
    public static class RoadManager
    {
        public static void ParsePacket(Packet packet)
        {
            RoadData data = Serializer.ConvertBytesToObject<RoadData>(packet.Contents);

            switch (data.StepMode)
            {
                case RoadStepMode.Add:
                    AddRoadSimple(data.Details.TileA, data.Details.TileB, RoadManagerHelper.GetRoadDefFromDefName(data.Details.RoadDefName), true);
                    break;

                case RoadStepMode.Remove:
                    RemoveRoadSimple(data.Details.TileA, data.Details.TileB, true);
                    break;
            }
        }

        public static void SendRoadAddRequest(int TileAID, int TileBID, RoadDef roadDef)
        {
            RoadData data = new RoadData();
            data.StepMode = RoadStepMode.Add;

            data.Details = new RoadDetails();
            data.Details.TileA = TileAID;
            data.Details.TileB = TileBID;
            data.Details.RoadDefName = roadDef.defName;

            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.RoadPacket), data);
            Network.Listener.EnqueuePacket(packet);
        }

        public static void SendRoadRemoveRequest(int TileAID, int TileBID)
        {
            RoadData data = new RoadData();
            data.StepMode = RoadStepMode.Remove;

            data.Details = new RoadDetails();
            data.Details.TileA = TileAID;
            data.Details.TileB = TileBID;

            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.RoadPacket), data);
            Network.Listener.EnqueuePacket(packet);
        }

        public static void AddRoads(RoadDetails[] Details, bool forceRefresh)
        {
            if (Details == null) return;

            foreach (RoadDetails detail in Details)
            {
                AddRoadSimple(detail.TileA, detail.TileB, RoadManagerHelper.GetRoadDefFromDefName(detail.RoadDefName), forceRefresh);
            }

            //If we don't want to force refresh we wait for all and then refresh the layer
            if (!forceRefresh) RoadManagerHelper.ForceRoadLayerRefresh();
        }

        public static void AddRoadSimple(int TileAID, int TileBID, RoadDef roadDef, bool forceRefresh)
        {
            if (!RoadManagerHelper.CheckIfCanBuildRoadOnTile(TileBID))
            {
                Logger.Warning($"Tried building a road at '{TileBID}' when it's not possible");
                return;
            }

            Tile TileA = Find.WorldGrid[TileAID];
            Tile TileB = Find.WorldGrid[TileBID];

            AddRoadLink(TileA, TileBID, roadDef);
            AddRoadLink(TileB, TileAID, roadDef);

            if (forceRefresh) RoadManagerHelper.ForceRoadLayerRefresh();
        }

        private static void AddRoadLink(Tile toAddTo, int neighborTileID, RoadDef roadDef)
        {
            if (toAddTo.Roads != null)
            {
                foreach (Tile.RoadLink roadLink in toAddTo.Roads)
                {
                    if (roadLink.neighbor == neighborTileID) return;
                }
            }

            Tile.RoadLink linkToAdd = new Tile.RoadLink
            {
                neighbor = neighborTileID,
                road = roadDef
            };

            toAddTo.potentialRoads ??= new List<Tile.RoadLink>();
            toAddTo.potentialRoads.Add(linkToAdd);
        }

        public static void ClearAllRoads()
        {
            foreach (Tile tile in Find.WorldGrid.tiles)
            {
                tile.Roads?.Clear();
                tile.potentialRoads = null;
            }

            RoadManagerHelper.ForceRoadLayerRefresh();
        }

        private static void RemoveRoadSimple(int TileAID, int TileBID, bool forceRefresh)
        {
            Tile TileA = Find.WorldGrid[TileAID];
            Tile TileB = Find.WorldGrid[TileBID];

            foreach (Tile.RoadLink roadLink in TileA.Roads.ToList())
            {
                if (roadLink.neighbor == TileBID)
                {
                    TileA.Roads.Remove(roadLink);
                    TileA.potentialRoads.Remove(roadLink);

                    //We need this to let the game know it shouldn't try to draw anything in here if there's no roads
                    if (TileA.potentialRoads.Count() == 0) TileA.potentialRoads = null;
                }
            }

            foreach (Tile.RoadLink roadLink in TileB.Roads.ToList())
            {
                if (roadLink.neighbor == TileAID)
                {
                    TileB.Roads.Remove(roadLink);
                    TileB.potentialRoads.Remove(roadLink);

                    //We need this to let the game know it shouldn't try to draw anything in here if there's no roads
                    if (TileB.potentialRoads.Count() == 0) TileB.potentialRoads = null;
                }
            }

            if (forceRefresh) RoadManagerHelper.ForceRoadLayerRefresh();
        }
    }

    public static class RoadManagerHelper
    {
        public static RoadDetails[] tempRoadDetails;
        public static RoadDef[] allowedRoadDefs;
        public static int[] allowedRoadCosts;

        public static RoadDef DirtPathDef => DefDatabase<RoadDef>.AllDefs.First(fetch => fetch.defName == "DirtPath");
        public static RoadDef DirtRoadDef => DefDatabase<RoadDef>.AllDefs.First(fetch => fetch.defName == "DirtRoad");
        public static RoadDef StoneRoadDef => DefDatabase<RoadDef>.AllDefs.First(fetch => fetch.defName == "StoneRoad");
        public static RoadDef AncientAsphaltRoadDef => DefDatabase<RoadDef>.AllDefs.First(fetch => fetch.defName == "AncientAsphaltRoad");
        public static RoadDef AncientAsphaltHighwayDef => DefDatabase<RoadDef>.AllDefs.First(fetch => fetch.defName == "AncientAsphaltHighway");

        public static void SetValues(ServerGlobalData serverGlobalData) 
        {
            tempRoadDetails = serverGlobalData.Roads;

            List<RoadDef> allowedRoads = new List<RoadDef>();
            if (serverGlobalData.RoadValues.AllowDirtPath) allowedRoads.Add(DirtPathDef);
            if (serverGlobalData.RoadValues.AllowDirtRoad) allowedRoads.Add(DirtRoadDef);
            if (serverGlobalData.RoadValues.AllowStoneRoad) allowedRoads.Add(StoneRoadDef);
            if (serverGlobalData.RoadValues.AllowAsphaltPath) allowedRoads.Add(AncientAsphaltRoadDef);
            if (serverGlobalData.RoadValues.AllowAsphaltHighway) allowedRoads.Add(AncientAsphaltHighwayDef);
            allowedRoadDefs = allowedRoads.ToArray();

            List<int> allowedCosts = new List<int>();
            if (serverGlobalData.RoadValues.AllowDirtPath) allowedCosts.Add(serverGlobalData.RoadValues.DirtPathCost);
            if (serverGlobalData.RoadValues.AllowDirtRoad) allowedCosts.Add(serverGlobalData.RoadValues.DirtRoadCost);
            if (serverGlobalData.RoadValues.AllowStoneRoad) allowedCosts.Add(serverGlobalData.RoadValues.StoneRoadCost);
            if (serverGlobalData.RoadValues.AllowAsphaltPath) allowedCosts.Add(serverGlobalData.RoadValues.AsphaltPathCost);
            if (serverGlobalData.RoadValues.AllowAsphaltHighway) allowedCosts.Add(serverGlobalData.RoadValues.AsphaltHighwayCost);
            allowedRoadCosts = allowedCosts.ToArray();
        }

        public static bool CheckIfTwoTilesAreConnected(int TileAID, int TileBID)
        {
            Tile TileA = Find.WorldGrid[TileAID];

            if (TileA.Roads != null)
            {
                foreach (Tile.RoadLink roadLink in TileA.Roads)
                {
                    if (roadLink.neighbor == TileBID) return true;
                }
            }

            return false;
        }

        public static bool CheckIfCanBuildRoadOnTile(int tileID)
        {
            Tile tile = Find.WorldGrid[tileID];

            if (tile.WaterCovered) return false;
            else if (!Find.WorldPathGrid.Passable(tileID)) return false;
            else return true;
        }

        public static string[] GetAvailableRoadLabels(bool includePrices)
        {
            List<string> roadLabels = new List<string>();
            for(int i = 0; i < allowedRoadDefs.Length; i++)
            {
                RoadDef def = allowedRoadDefs[i];

                if (includePrices) roadLabels.Add($"{def.LabelCap} > {allowedRoadCosts[i]}$/u");
                else roadLabels.Add(def.LabelCap);
            }

            return roadLabels.ToArray();
        }

        public static RoadDef GetRoadDefFromDefName(string defName)
        {
            return DefDatabase<RoadDef>.AllDefs.First(fetch => fetch.defName == defName);
        }

        public static void ChooseRoadDialogs(int[] neighborTiles, bool hasRoadOnTile)
        {
            if (hasRoadOnTile)
            {
                RT_Dialog_2Button d1 = new RT_Dialog_2Button("Road manager", "Select the action you want to do",
                    "Build", "Destroy", delegate { ShowRoadBuildDialog(neighborTiles); }, delegate { ShowRoadDestroyDialog(neighborTiles); }, null);

                DialogManager.PushNewDialog(d1);
            }
            else ShowRoadBuildDialog(neighborTiles);
        }

        public static void ShowRoadBuildDialog(int[] neighborTiles)
        {
            List<string> selectableTileLabels = new List<string>();
            List<int> selectableTiles = new List<int>();

            foreach (int tileID in neighborTiles)
            {
                if (!CheckIfCanBuildRoadOnTile(tileID)) continue;
                else if (CheckIfTwoTilesAreConnected(ClientValues.chosenCaravan.Tile, tileID)) continue;
                else
                {
                    Vector2 vector = Find.WorldGrid.LongLatOf(tileID);
                    string toDisplay = $"Tile at {vector.y.ToStringLatitude()} - {vector.x.ToStringLongitude()}";
                    selectableTileLabels.Add(toDisplay);
                    selectableTiles.Add(tileID);
                }
            }

            Action r1 = delegate
            {
                int selectedTile = selectableTiles[DialogManager.dialogButtonListingResultInt];

                RT_Dialog_ListingWithButton d1 = new RT_Dialog_ListingWithButton("Road builder", "Select road type to use",
                    GetAvailableRoadLabels(true),
                    delegate
                    {
                        int selectedIndex = DialogManager.dialogButtonListingResultInt;

                        if (RimworldManager.CheckIfHasEnoughSilverInCaravan(ClientValues.chosenCaravan, allowedRoadCosts[selectedIndex]))
                        {
                            RimworldManager.RemoveThingFromCaravan(ThingDefOf.Silver, allowedRoadCosts[selectedIndex], ClientValues.chosenCaravan);
                            RoadManager.SendRoadAddRequest(ClientValues.chosenCaravan.Tile, selectedTile, allowedRoadDefs[selectedIndex]);
                            SaveManager.ForceSave();
                        }
                        else DialogManager.PushNewDialog(new RT_Dialog_Error("You do not have enough silver for this action!"));
                    });

                DialogManager.PushNewDialog(d1);
            };

            DialogManager.PushNewDialog(new RT_Dialog_ListingWithButton("Road builder", "Select a tile to connect with",
                selectableTileLabels.ToArray(), r1));
        }

        public static void ShowRoadDestroyDialog(int[] neighborTiles)
        {
            List<string> selectableTilesLabels = new List<string>();
            List<int> selectableTiles = new List<int>();

            foreach (int tileID in neighborTiles)
            {
                if (CheckIfTwoTilesAreConnected(ClientValues.chosenCaravan.Tile, tileID))
                {
                    Vector2 vector = Find.WorldGrid.LongLatOf(tileID);
                    string toDisplay = $"Tile at {vector.y.ToStringLatitude()} - {vector.x.ToStringLongitude()}";
                    selectableTilesLabels.Add(toDisplay);
                    selectableTiles.Add(tileID);
                }
            }

            Action r1 = delegate
            {
                int selectedTile = selectableTiles[DialogManager.dialogButtonListingResultInt];

                RoadManager.SendRoadRemoveRequest(ClientValues.chosenCaravan.Tile, selectedTile);
            };

            DialogManager.PushNewDialog(new RT_Dialog_ListingWithButton("Road destroyer", "Select a tile to disconnect from",
                selectableTilesLabels.ToArray(), r1));
        }

        public static RoadDetails[] GetPlanetRoads()
        {
            List<RoadDetails> toGet = new List<RoadDetails>();
            foreach (Tile tile in Find.WorldGrid.tiles)
            {
                if (tile.Roads != null)
                {
                    foreach (Tile.RoadLink link in tile.Roads)
                    {
                        RoadDetails Details = new RoadDetails();
                        Details.TileA = Find.WorldGrid.tiles.IndexOf(tile);
                        Details.TileB = link.neighbor;
                        Details.RoadDefName = link.road.defName;

                        if (!CheckIfExists(Details.TileA, Details.TileB)) toGet.Add(Details);
                    }
                }
            }
            return toGet.ToArray();

            bool CheckIfExists(int TileA, int TileB)
            {
                foreach (RoadDetails Details in toGet)
                {
                    if (Details.TileA == TileA && Details.TileB == TileB) return true;
                    else if (Details.TileA == TileB && Details.TileB == TileA) return true;
                }

                return false;
            }
        }

        public static void ForceRoadLayerRefresh()
        {
            Find.World.renderer.SetDirty<WorldLayer_Roads>();
            Find.World.renderer.RegenerateLayersIfDirtyInLongEvent();
        }
    }
}
