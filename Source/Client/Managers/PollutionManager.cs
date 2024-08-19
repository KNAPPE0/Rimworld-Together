﻿using RimWorld.Planet;
using Shared;
using System.Collections.Generic;
using Verse;

namespace GameClient
{
    public static class PollutionManager
    {
        public static void AddPollutedTiles(PollutionDetails[] details, bool forceRefresh)
        {
            if (details == null) return;

            foreach(PollutionDetails detail in details)
            {
                AddPollutedTileSimple(detail, forceRefresh);
            }

            //If we don't want to force refresh we wait for all and then refresh the layer
            if (!forceRefresh) PollutionManagerHelper.ForcePollutionLayerRefresh();
        }

        public static void AddPollutedTileSimple(PollutionDetails details, bool forceRefresh)
        {
            Tile toPollute = Find.WorldGrid.tiles[details.Tile];
            toPollute.pollution = details.Quantity;

            if (forceRefresh) PollutionManagerHelper.ForcePollutionLayerRefresh();
        }

        public static void ClearAllPollution()
        {
            foreach (Tile tile in Find.WorldGrid.tiles)
            {
                if (tile.pollution != 0) tile.pollution = 0;
            }

            PollutionManagerHelper.ForcePollutionLayerRefresh();
        }
    }

    public static class PollutionManagerHelper
    {
        public static PollutionDetails[] tempPollutionDetails;

        public static void SetValues(ServerGlobalData serverGlobalData)
        {
            tempPollutionDetails = serverGlobalData.PollutedTiles;
        }

        public static PollutionDetails[] GetPlanetPollutedTiles()
        {
            List<PollutionDetails> toGet = new List<PollutionDetails>();
            foreach (Tile tile in Find.WorldGrid.tiles)
            {
                if (tile.pollution != 0)
                {
                    PollutionDetails details = new PollutionDetails();
                    details.Tile = Find.WorldGrid.tiles.IndexOf(tile);
                    details.Quantity = tile.pollution;

                    toGet.Add(details);
                }
            }

            return toGet.ToArray();
        }

        public static void ForcePollutionLayerRefresh()
        {
            Find.World.renderer.SetDirty<WorldLayer_Pollution>();
            Find.World.renderer.RegenerateLayersIfDirtyInLongEvent();
        }
    }
}
