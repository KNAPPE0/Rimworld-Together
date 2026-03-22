using GameClient.Defs;
using GameClient.Misc;
using GameClient.PacketManagers;
using RimWorld;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using static Shared.CommonEnumerators;

namespace GameClient.Managers
{
    public static class PlanetManager
    {
        public static void BuildPlanet()
        {
            PlanetManagerHelper.GetPlayerFactionsInWorld();

            if (SessionHandler.IsGeneratingFreshWorld)
                return;

            PM_Settlements.ClearAllSettlements();
            PM_Settlements.AddSettlements(PlayerSettlementManagerHelper.tempSettlements);

            PM_Sites.ClearAllSites();
            PM_Sites.AddSites(SiteManagerH.tempSites);

            PM_Npcs.ClearAllSettlements();
            NPCManagerH.SaveAllQuests();

            PM_Npcs.AddSettlements(NPCManagerH.tempNPCSettlements);
            NPCManagerH.CleanupQuests();

            PM_Roads.ClearAllRoads();
            PM_Roads.AddRoads(RoadManagerHelper.tempRoadDetails, false);

            if (ModLister.BiotechInstalled)
            {
                PM_Pollution.ClearAllPollution();
                PM_Pollution.AddPollutedTiles(PollutionManagerHelper.tempPollutionDetails, false);
            }

            PM_Caravans.ClearAllCaravans();
            CaravanManagerH.SetAllPlayerCaravans();
        }
    }

    public static class PlanetManagerHelper
    {
        public static Faction GetPlayerFactionFromGoodwill(Goodwill goodwill)
        {
            switch (goodwill)
            {
                case Goodwill.Enemy: return SessionHandler.EnemyFaction;
                case Goodwill.Neutral: return SessionHandler.NeutralFaction;
                case Goodwill.Ally: return SessionHandler.AllyFaction;
                case Goodwill.Guild: return SessionHandler.GuildFaction;
                case Goodwill.Personal: return Faction.OfPlayer;
                default: return null;
            }
        }

        public static List<Faction> GetNPCFactionFromDefName(string defName)
        {
            List<Faction> factions = new List<Faction>();

            foreach (Faction faction in Find.World.factionManager.AllFactions)
            {
                if (faction.def.defName == defName)
                    factions.Add(faction);
            }

            return factions;
        }

        public static void GetPlayerFactionsInWorld()
        {
            Faction[] factions = Find.FactionManager.AllFactions.ToArray();

            SessionHandler.EnemyFaction = factions.FirstOrDefault(fetch => fetch.def.defName == RTFactionDefOf.RTEnemy.defName);
            SessionHandler.AllyFaction = factions.FirstOrDefault(fetch => fetch.def.defName == RTFactionDefOf.RTAlly.defName);
            SessionHandler.NeutralFaction = factions.FirstOrDefault(fetch => fetch.def.defName == RTFactionDefOf.RTNeutral.defName);
            SessionHandler.GuildFaction = factions.FirstOrDefault(fetch => fetch.def.defName == RTFactionDefOf.RTFaction.defName);

            SessionHandler.PlayerFactions.Clear();
            if (SessionHandler.EnemyFaction != null) SessionHandler.PlayerFactions.Add(SessionHandler.EnemyFaction);
            if (SessionHandler.AllyFaction != null) SessionHandler.PlayerFactions.Add(SessionHandler.AllyFaction);
            if (SessionHandler.NeutralFaction != null) SessionHandler.PlayerFactions.Add(SessionHandler.NeutralFaction);
            if (SessionHandler.GuildFaction != null) SessionHandler.PlayerFactions.Add(SessionHandler.GuildFaction);

            SessionHandler.PlayerFactionDefs.Clear();
            if (SessionHandler.EnemyFaction != null) SessionHandler.PlayerFactionDefs.Add(SessionHandler.EnemyFaction.def);
            if (SessionHandler.AllyFaction != null) SessionHandler.PlayerFactionDefs.Add(SessionHandler.AllyFaction.def);
            if (SessionHandler.NeutralFaction != null) SessionHandler.PlayerFactionDefs.Add(SessionHandler.NeutralFaction.def);
            if (SessionHandler.GuildFaction != null) SessionHandler.PlayerFactionDefs.Add(SessionHandler.GuildFaction.def);
        }
    }
}