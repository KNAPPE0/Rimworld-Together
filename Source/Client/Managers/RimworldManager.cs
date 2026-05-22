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
            return Find.AnyPlayerHomeMap != null;
        }

        public static Pawn GetIfSocialPawnInCaravan(Caravan caravan)
        {
            return caravan.PawnsListForReading.FirstOrDefault(p => p.IsColonist && !p.skills.skills[10].PermanentlyDisabled);
        }

        public static bool CheckIfSocialPawnInMap(Map map)
        {
            return map.mapPawns.AllPawns.Find(p => p.IsColonist && !p.skills.skills[10].PermanentlyDisabled) != null;
        }

        public static bool CheckIfHasEnoughSilverInMap(Map map, int requiredQuantity)
        {
            if (requiredQuantity == 0) return true;
            return GetSpecificThingCountInMap(ThingDefOf.Silver, map) >= requiredQuantity;
        }

        public static bool CheckIfHasEnoughSilverInCaravan(Caravan caravan, int requiredQuantity)
        {
            if (requiredQuantity == 0) return true;
            return GetSilverInCaravan(caravan) >= requiredQuantity;
        }

        public static bool CheckIfHasEnoughItemInCaravan(Caravan caravan, string defName, int quantity)
        {
            if (quantity == 0) return true;

            // Single-pass sum; previous impl built an interim List via FindAll then re-iterated.
            int total = 0;
            foreach (Thing stack in CaravanInventoryUtility.AllInventoryItems(caravan))
            {
                if (stack?.def == null) continue;
                if (stack.def.defName == defName) total += stack.stackCount;
            }
            return total >= quantity;
        }

        public static Pawn GetNegotiatorAtMap(Map map)
        {
            // Was scanning Find.AnyPlayerHomeMap regardless of the `map` arg — ignored the parameter.
            if (map?.mapPawns == null) return null;
            return map.mapPawns.AllPawns.Find(p => p.IsColonist && !p.skills.skills[10].PermanentlyDisabled);
        }

        public static Thing[] GetAllThingsInMap(Map map)
        {
            // Removed duplicated `category == Item` predicate (upstream typo).
            return map.listerThings.AllThings
                .Where(t => t.def.category == ThingCategory.Item && t.IsInAnyStorage() && !t.Position.Fogged(map))
                .ToArray();
        }

        public static int GetSpecificThingCountInMap(ThingDef thingDef, Map map)
        {
            // FindAll().ToList() in the original was a double allocation. Single pass now.
            int total = 0;
            foreach (Thing t in map.listerThings.ThingsOfDef(thingDef))
            {
                if (t.IsInAnyStorage()) total += t.stackCount;
            }
            return total;
        }

        public static int GetSilverInCaravan(Caravan caravan)
        {
            int total = 0;
            foreach (Thing t in CaravanInventoryUtility.AllInventoryItems(caravan))
            {
                if (t.def == ThingDefOf.Silver) total += t.stackCount;
            }
            return total;
        }

        // Generalised GetSilverInCaravan for treasury/marketplace dialogs.
        public static int GetItemCountInCaravan(Caravan caravan, string defName)
        {
            if (caravan == null || string.IsNullOrEmpty(defName)) return 0;
            int total = 0;
            foreach (Thing t in CaravanInventoryUtility.AllInventoryItems(caravan))
            {
                if (t?.def != null && t.def.defName == defName) total += t.stackCount;
            }
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
            IntVec3 positionToPlaceAt = position != IntVec3.Invalid ? position : map.Center;

            if (byDropPod) TradeUtility.SpawnDropPod(FindVectorNear(positionToPlaceAt, map), map, thing);
            else if (thing is Pawn) GenSpawn.Spawn(thing, positionToPlaceAt, map, thing.Rotation);
            else GenPlace.TryPlaceThing(thing, positionToPlaceAt, map, ThingPlaceMode.Direct, rot: thing.Rotation);
        }

        public static void PlaceThingIntoCaravan(Thing thing, Caravan caravan)
        {
            if (thing is Pawn pawn)
            {
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

            int taken = 0;
            // Walk inventory directly — was double-iterating via FindAll → foreach.
            foreach (Thing thing in CaravanInventoryUtility.AllInventoryItems(caravan))
            {
                if (thing.def != thingDef) continue;
                if (taken + thing.stackCount >= requiredQuantity)
                {
                    thing.holdingOwner.Take(thing, requiredQuantity - taken);
                    return;
                }
                thing.holdingOwner.Take(thing, thing.stackCount);
                taken += thing.stackCount;
            }
        }

        public static void RemoveThingFromSettlement(Map map, ThingDef thingDef, int requiredQuantity)
        {
            // Was: FirstOrDefault() → NRE on .stackCount if list empties before quantity reached.
            List<Thing> things = map.listerThings.ThingsOfDef(thingDef).Where(t => t.IsInAnyStorage()).ToList();

            int i = 0;
            while (requiredQuantity > 0 && i < things.Count)
            {
                Thing thing = things[i];
                int take = Mathf.Min(requiredQuantity, thing.stackCount);
                thing.SplitOff(take);
                requiredQuantity -= take;
                i++;
            }
        }

        public static void RemovePawnFromGame(Pawn pawn)
        {
            if (pawn.Spawned) pawn.DeSpawn();
            pawn.Destroy();
        }

        public static Pawn[] GetAllSettlementsPawns(Faction faction, bool includeAnimals)
        {
            List<Pawn> allPawns = new List<Pawn>();
            foreach (Settlement settlement in Find.World.worldObjects.Settlements)
            {
                if (settlement.Faction != faction) continue;
                allPawns.AddRange(GetPawnsFromMap(settlement.Map, faction, includeAnimals));
            }
            return allPawns.ToArray();
        }

        public static Pawn[] GetPawnsFromMap(Map map, Faction faction, bool includeAnimals)
        {
            if (map == null || map.mapPawns == null) return new Pawn[0];

            if (includeAnimals)
                return map.mapPawns.AllPawns.Where(p => p.Faction == faction).ToArray();
            return map.mapPawns.AllPawns.Where(p => p.Faction == faction && !CheckIfThingIsAnimal(p)).ToArray();
        }

        public static bool CheckIfMapHasPlayerPawns(Map map)
        {
            if (map?.mapPawns == null) return false;
            return map.mapPawns.AllPawns.Find(p => p.Faction == Faction.OfPlayer) != null;
        }

        public static void SetMapFactions(Map map, Faction targetFaction)
        {
            foreach (Pawn pawn in map.mapPawns.AllPawns.Where(p => p.Faction == Faction.OfPlayer))
            {
                pawn.SetFaction(targetFaction);
            }

            foreach (Thing thing in map.listerThings.AllThings.Where(t => t.Faction == Faction.OfPlayer))
            {
                if (thing.def.CanHaveFaction) thing.SetFaction(targetFaction);
            }
        }

        public static void SetMapLord(Map map, Faction targetFaction)
        {
            IntVec3 deployPlace = map.Center;
            Thing toFocusOn = map.listerThings.AllThings.Find(x => x.def.defName == "RTDefenseSpot" || x.def.defName == "RTChillSpot");
            if (toFocusOn != null) deployPlace = toFocusOn.Position;

            // Was: AllPawns.ToList().FindAll(...).ToArray() — triple allocation.
            Pawn[] lordPawns = map.mapPawns.AllPawns.Where(p => p.Faction == targetFaction).ToArray();
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
                bool isPlayer = pawn.Faction == Faction.OfPlayer;
                if (!isPlayer && factionSpecific) continue;

                pawns.Add(ScribeManager.SerializeToString(pawn, ScribeManager.SerializableType.Pawn, -1, useCustomID ? pawn.ThingID : null));
            }
            return pawns;
        }

        public static bool CheckIfThingIsPawn(Thing thing)
        {
            try
            {
                if (thing?.def?.defName == "Human") return true;
                PawnKindDef pawn = DefDatabase<PawnKindDef>.AllDefs.FirstOrDefault(d => d.defName == thing.def.defName);
                return pawn != null;
            }
            catch { return false; }
        }

        public static bool CheckIfThingIsHuman(Pawn pawn) { return !pawn.IsAnimal; }

        public static bool CheckIfThingIsAnimal(Pawn pawn) { return pawn.IsAnimal; }

        public static bool CheckIfThingIsCorpse(Thing thing) { return thing is Corpse; }

        public static IntVec3 GetTransferLocationInMap(Map map)
        {
            Thing tradingSpot = map.listerThings.AllThings.Find(x => x.def.defName == "RTTransferSpot");
            if (tradingSpot != null) return tradingSpot.Position;

            const string title = "Missing transfer spot";
            const string description = "Received things will appear in the center of the map";
            GenerateLetter(title, description, LetterDefOf.NeutralEvent);
            return new IntVec3(map.Center.x, map.Center.y, map.Center.z);
        }
    }
}
