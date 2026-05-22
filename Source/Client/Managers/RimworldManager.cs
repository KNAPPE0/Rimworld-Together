using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Shared.Misc;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using static Shared.Misc.Printer;

namespace GameClient.Managers
{
    public static class RimworldManager
    {
        public static bool CheckIfPlayerHasMap()
        {
            Map map = Find.AnyPlayerHomeMap;
            if (map != null) return true;
            else return false;
        }

        public static Pawn GetIfSocialPawnInCaravan(Caravan caravan)
        {
            return caravan.PawnsListForReading.FirstOrDefault(fetch => fetch.IsColonist && !fetch.skills.skills[10].PermanentlyDisabled);
        }

        public static bool CheckIfSocialPawnInMap(Map map)
        {
            Pawn playerNegotiator = map.mapPawns.AllPawns.Find(fetch => fetch.IsColonist && !fetch.skills.skills[10].PermanentlyDisabled);
            if (playerNegotiator != null) return true;
            else return false;
        }

        public static bool CheckIfHasEnoughSilverInMap(Map map, int requiredQuantity)
        {
            if (requiredQuantity == 0) return true;

            int silverInMap = GetSpecificThingCountInMap(ThingDefOf.Silver, map);
            if (silverInMap >= requiredQuantity) return true;
            else return false;
        }

        public static bool CheckIfHasEnoughSilverInCaravan(Caravan caravan, int requiredQuantity)
        {
            if (requiredQuantity == 0) return true;

            int silverInCaravan = GetSilverInCaravan(caravan);
            if (silverInCaravan >= requiredQuantity) return true;
            else return false;
        }

        public static bool CheckIfHasEnoughItemInCaravan(Caravan caravan, string defName, int quantity)
        {
            if (quantity == 0) return true;

            List<Thing> caravanItems = CaravanInventoryUtility.AllInventoryItems(caravan).FindAll(x => x.def.defName == defName);
            int totalItem = 0;

            foreach (Thing itemStack in caravanItems) totalItem += itemStack.stackCount;
            if (totalItem >= quantity) return true;

            return false;
        }

        public static Pawn GetNegotiatorAtMap(Map map)
        {
            return Find.AnyPlayerHomeMap.mapPawns.AllPawns.Find(fetch => fetch.IsColonist && !fetch.skills.skills[10].PermanentlyDisabled);
        }

        public static Thing[] GetAllThingsInMap(Map map)
        {
            return map.listerThings.AllThings.Where(fetch => fetch.def.category == ThingCategory.Item
                && fetch.IsInAnyStorage() && fetch.def.category == ThingCategory.Item && !fetch.Position.Fogged(map)).ToArray();
        }

        public static int GetSpecificThingCountInMap(ThingDef thingDef, Map map)
        {
            int totalCount = 0;
            List<Thing> things = map.listerThings.ThingsOfDef(thingDef).FindAll(fetch => fetch.IsInAnyStorage()).ToList();
            foreach (Thing thing in things)
            {
                totalCount += thing.stackCount;
            }

            return totalCount;
        }

        public static int GetSilverInCaravan(Caravan caravan)
        {
            List<Thing> caravanSilver = CaravanInventoryUtility.AllInventoryItems(caravan)
                .FindAll(x => x.def == ThingDefOf.Silver);

            int totalSilver = 0;
            foreach (Thing silverStack in caravanSilver) totalSilver += silverStack.stackCount;

            return totalSilver;
        }

        /// <summary>
        /// KMH: Total stack count of any defName in a caravan. Generalises
        /// <see cref="GetSilverInCaravan"/> for the treasury/marketplace dialogs.
        /// </summary>
        public static int GetItemCountInCaravan(Caravan caravan, string defName)
        {
            if (caravan == null || string.IsNullOrEmpty(defName)) return 0;
            List<Thing> matches = CaravanInventoryUtility.AllInventoryItems(caravan)
                .FindAll(x => x.def != null && x.def.defName == defName);

            int total = 0;
            foreach (Thing stack in matches) total += stack.stackCount;
            return total;
        }

        public static void GenerateLetter(string title, string description, LetterDef letterType)
        {
            Find.LetterStack.ReceiveLetter(title, description, letterType);
        }

        private static IntVec3 FindVectorNear(IntVec3 center, Map map)
        {
            if (!DropCellFinder.TryFindDropSpotNear(center, map, out IntVec3 vectorForUse, false, true))
            {
                Printer.Warning("Couldn't find any good drop spot near " + center + "Will use random valid location instead.", LogImportanceMode.Verbose);
                vectorForUse = CellFinderLoose.RandomCellWith(c => c.Standable(map) && !c.Fogged(map), map);
            }

            return vectorForUse;
        }

        public static void PlaceThingIntoMap(Thing thing, Map map, IntVec3 position, bool byDropPod = false)
        {
            IntVec3 positionToPlaceAt = IntVec3.Zero;
            if (position != IntVec3.Invalid) positionToPlaceAt = position;
            else positionToPlaceAt = map.Center;

            if (byDropPod) TradeUtility.SpawnDropPod(FindVectorNear(positionToPlaceAt, map), map, thing);
            else
            {
                if (thing is Pawn) GenSpawn.Spawn(thing, positionToPlaceAt, map, thing.Rotation);
                else GenPlace.TryPlaceThing(thing, positionToPlaceAt, map, ThingPlaceMode.Direct, rot: thing.Rotation);
            }
        }

        public static void PlaceThingIntoCaravan(Thing thing, Caravan caravan)
        {
            if (thing is Pawn)
            {
                Pawn pawn = thing as Pawn;
                if (!Find.WorldPawns.AllPawnsAliveOrDead.Contains(pawn)) Find.WorldPawns.PassToWorld(pawn);
                if (pawn.def.CanHaveFaction) pawn.SetFactionDirect(Faction.OfPlayer);

                caravan.AddPawn(pawn, false);
            }

            else
            {
                if (thing.stackCount == 0) return;
                caravan.AddPawnOrItem(thing, false);
            }
        }

        public static void RemoveThingFromCaravan(Caravan caravan, ThingDef thingDef, int requiredQuantity)
        {
            if (requiredQuantity == 0) return;

            List<Thing> caravanQuantity = CaravanInventoryUtility.AllInventoryItems(caravan)
                .FindAll(x => x.def == thingDef);

            int takenQuantity = 0;
            foreach (Thing thing in caravanQuantity)
            {
                if (takenQuantity + thing.stackCount >= requiredQuantity)
                {
                    thing.holdingOwner.Take(thing, requiredQuantity - takenQuantity);
                    break;
                }

                else if (takenQuantity + thing.stackCount < requiredQuantity)
                {
                    thing.holdingOwner.Take(thing, thing.stackCount);
                    takenQuantity += thing.stackCount;
                }
            }
        }

        public static void RemoveThingFromSettlement(Map map, ThingDef thingDef, int requiredQuantity)
        {
            List<Thing> things = map.listerThings.ThingsOfDef(thingDef).Where(fetch => fetch.IsInAnyStorage()).ToList();

            while (requiredQuantity > 0)
            {
                Thing thing = things.FirstOrDefault();
                int stackDeleting = Mathf.Min(requiredQuantity, thing.stackCount);
                thing.SplitOff(stackDeleting);
                requiredQuantity -= stackDeleting;
                things.Remove(thing);
            }
        }

        public static void RemovePawnFromGame(Pawn pawn)
        {
            if (pawn.Spawned) pawn.DeSpawn();
            pawn.Destroy();
        }

        public static Pawn[] GetAllSettlementsPawns(Faction faction, bool includeAnimals)
        {
            Settlement[] settlements = Find.World.worldObjects.Settlements.Where(fetch => fetch.Faction == faction).ToArray();

            List<Pawn> allPawns = new List<Pawn>();
            foreach (Settlement settlement in settlements)
            {
                allPawns.AddRange(GetPawnsFromMap(settlement.Map, faction, includeAnimals));
            }

            return allPawns.ToArray();
        }

        public static Pawn[] GetPawnsFromMap(Map map, Faction faction, bool includeAnimals)
        {
            if (map == null || map.mapPawns == null) return new Pawn[0];
            else
            {
                if (includeAnimals) return map.mapPawns.AllPawns.Where(fetch => fetch.Faction == faction).ToArray();
                else return map.mapPawns.AllPawns.Where(fetch => fetch.Faction == faction && !RimworldManager.CheckIfThingIsAnimal(fetch)).ToArray();
            }
        }

        public static bool CheckIfMapHasPlayerPawns(Map map)
        {
            if (map == null || map.mapPawns == null) return false;
            else if (map.mapPawns.AllPawns.FirstOrDefault(fetch => fetch.Faction == Faction.OfPlayer) != null) return true;
            else return false;
        }

        public static void SetMapFactions(Map map, Faction targetFaction)
        {
            foreach (Pawn pawn in map.mapPawns.AllPawns.Where(fetch => fetch.Faction == Faction.OfPlayer))
            {
                pawn.SetFaction(targetFaction);
            }

            foreach (Thing thing in map.listerThings.AllThings.Where(fetch => fetch.Faction == Faction.OfPlayer))
            {
                if (thing.def.CanHaveFaction) thing.SetFaction(targetFaction);
            }
        }

        public static void SetMapLord(Map map, Faction targetFaction)
        {
            Thing toFocusOn;

            IntVec3 deployPlace = map.Center;
            toFocusOn = map.listerThings.AllThings.Find(x => x.def.defName == "RTDefenseSpot" || x.def.defName == "RTChillSpot");
            if (toFocusOn != null) deployPlace = toFocusOn.Position;

            Pawn[] lordPawns = map.mapPawns.AllPawns.ToList().FindAll(fetch => fetch.Faction == targetFaction).ToArray();
            LordJob_DefendBase job = new LordJob_DefendBase(targetFaction, deployPlace, int.MaxValue);
            LordMaker.MakeNewLord(targetFaction, job, map, lordPawns);
        }

        public static List<string> GetCaravanPawnsIntoString(Caravan caravan, bool useCustomID = false)
        {
            List<string> pawns = new List<string>();

            foreach (Pawn pawn in caravan.PawnsListForReading)
            {
                pawns.Add(ScribeManager.SerializeToString(pawn, ScribeManager.SerializableType.Pawn, -1, useCustomID ? pawn.ThingID : null));
            }

            return pawns;
        }

        public static List<string> GetMapPawnsIntoString(Map map, bool useCustomID = false, bool factionSpecific = false)
        {
            List<string> pawns = new List<string>();

            foreach (Pawn pawn in map.mapPawns.AllPawns)
            {
                string data = ScribeManager.SerializeToString(pawn, ScribeManager.SerializableType.Pawn, -1, useCustomID ? pawn.ThingID : null);
                if (pawn.Faction == Faction.OfPlayer) pawns.Add(data);
                else 
                {
                    if (factionSpecific) continue;
                    else pawns.Add(data);
                }
            }

            return pawns;
        }

        public static bool CheckIfThingIsPawn(Thing thing)
        {
            try
            {
                PawnKindDef pawn = DefDatabase<PawnKindDef>.AllDefs.FirstOrDefault(fetch => fetch.defName == thing.def.defName);
                if (pawn != null || thing.def.defName == "Human") return true;
                else return false;
            }
            catch { return false; }
        }

        public static bool CheckIfThingIsHuman(Pawn pawn) { return !pawn.IsAnimal; }

        public static bool CheckIfThingIsAnimal(Pawn pawn) { return pawn.IsAnimal; }

        public static bool CheckIfThingIsCorpse(Thing thing)
        {
            if (thing is Corpse corpse) return true;
            else return false;
        }

        public static IntVec3 GetTransferLocationInMap(Map map)
        {
            Thing tradingSpot = map.listerThings.AllThings.Find(x => x.def.defName == "RTTransferSpot");
            if (tradingSpot != null) return tradingSpot.Position;
            else
            {
                string title = "Missing transfer spot";
                string description = "Received things will appear in the center of the map";
                RimworldManager.GenerateLetter(title, description, LetterDefOf.NeutralEvent);

                return new IntVec3(map.Center.x, map.Center.y, map.Center.z);
            }
        }
    }
}
