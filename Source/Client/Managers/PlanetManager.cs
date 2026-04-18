using GameClient.Defs;
using GameClient.Misc;
using GameClient.PacketManagers;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using static Shared.CommonEnumerators;

namespace GameClient.Managers
{
    //Class that handles all the planet functions for the mod
    public static class PlanetManager
    {
        //Regenerates the planet of player objects

        public static void BuildPlanet()
        {
            PlanetManagerHelper.GetPlayerFactionsInWorld();

            //This step gets skiped if it's the first time building the planet
            if (SessionHandler.IsGeneratingFreshWorld) return;
            else
            {
                PM_Settlements.ClearAllSettlements();
                PM_Settlements.AddSettlements(SessionHandler.GlobalData._playerSettlements);
                
                PM_Sites.ClearAllSites();
                PM_Sites.AddSites(SessionHandler.GlobalData._playerSites);
                
                PM_Npcs.ClearAllSettlements();
                NPCManagerH.SaveAllQuests();
                
                PM_Npcs.AddSettlements(SessionHandler.GlobalData._npcSettlements);
                NPCManagerH.CleanupQuests();
                
                PM_Roads.ClearAllRoads();
                PM_Roads.AddRoads(SessionHandler.GlobalData._roads, false);
                
                PM_Caravans.ClearAllCaravans();
                CaravanManagerH.SetAllPlayerCaravans();

                if (ModLister.BiotechInstalled)
                {
                    PM_Pollution.ClearAllPollution();
                    PM_Pollution.AddPollutedTiles(SessionHandler.GlobalData._pollutedTiles, false);
                }
            }
        }
    }

    //Helper class for the PlanetManager class

    public static class PlanetManagerHelper
    {
        //Returns an online faction depending on the value

        public static Faction GetPlayerFactionFromGoodwill(Goodwill goodwill)
        {
            Faction factionToUse = null;
            switch (goodwill)
            {
                case Goodwill.Enemy:
                    factionToUse = SessionHandler.EnemyFaction;
                    break;

                case Goodwill.Neutral:
                    factionToUse = SessionHandler.NeutralFaction;
                    break;

                case Goodwill.Ally:
                    factionToUse = SessionHandler.AllyFaction;
                    break;

                case Goodwill.Guild:
                    factionToUse = SessionHandler.GuildFaction;
                    break;

                case Goodwill.Personal:
                    factionToUse = Faction.OfPlayer;
                    break;
            }

            return factionToUse;
        }

        //Returns an npc faction depending on the value

        public static List<Faction> GetNPCFactionFromDefName(string defName)
        {
            List<Faction> factions = new List<Faction>();
            foreach (Faction faction in Find.World.factionManager.AllFactions)
            {
                if (faction.def.defName == defName)
                {
                    factions.Add(faction);
                }
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
            SessionHandler.PlayerFactions.Add(SessionHandler.EnemyFaction);
            SessionHandler.PlayerFactions.Add(SessionHandler.AllyFaction);
            SessionHandler.PlayerFactions.Add(SessionHandler.NeutralFaction);
            SessionHandler.PlayerFactions.Add(SessionHandler.GuildFaction);

            SessionHandler.PlayerFactionDefs.Clear();
            SessionHandler.PlayerFactionDefs.Add(SessionHandler.EnemyFaction.def);
            SessionHandler.PlayerFactionDefs.Add(SessionHandler.AllyFaction.def);
            SessionHandler.PlayerFactionDefs.Add(SessionHandler.NeutralFaction.def);
            SessionHandler.PlayerFactionDefs.Add(SessionHandler.GuildFaction.def);
        }
    }
}
