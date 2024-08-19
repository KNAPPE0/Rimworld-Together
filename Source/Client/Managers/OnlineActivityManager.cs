using RimWorld.Planet;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Shared;
using static Shared.CommonEnumerators;
using UnityEngine;

namespace GameClient
{
    public static class OnlineActivityManager
    {
        public static List<Pawn> factionPawns = new List<Pawn>();
        public static List<Pawn> nonFactionPawns = new List<Pawn>();
        public static List<Thing> mapThings = new List<Thing>();
        public static Map onlineMap;

        public static Thing queuedThing;
        public static int queuedTimeSpeed;
        public static WeatherDef queuedWeather;
        public static GameCondition queuedGameCondition;
        public static TimeSpeed maximumAllowedTimeSpeed = TimeSpeed.Fast;

        public static void ParseOnlineActivityPacket(Packet packet)
        {
            OnlineActivityData data = Serializer.ConvertBytesToObject<OnlineActivityData>(packet.Contents);

            switch (data.ActivityStepMode)
            {
                case OnlineActivityStepMode.Request:
                    OnActivityRequest(data);
                    break;

                case OnlineActivityStepMode.Accept:
                    OnActivityAccept(data);
                    break;

                case OnlineActivityStepMode.Reject:
                    OnActivityReject();
                    break;

                case OnlineActivityStepMode.Unavailable:
                    OnActivityUnavailable();
                    break;

                case OnlineActivityStepMode.Action:
                    OnlineManagerHelper.ReceivePawnOrder(data);
                    break;

                case OnlineActivityStepMode.Create:
                    OnlineManagerHelper.ReceiveCreationOrder(data);
                    break;

                case OnlineActivityStepMode.Destroy:
                    OnlineManagerHelper.ReceiveDestructionOrder(data);
                    break;

                case OnlineActivityStepMode.Damage:
                    OnlineManagerHelper.ReceiveDamageOrder(data);
                    break;

                case OnlineActivityStepMode.Hediff:
                    OnlineManagerHelper.ReceiveHediffOrder(data);
                    break;

                case OnlineActivityStepMode.TimeSpeed:
                    OnlineManagerHelper.ReceiveTimeSpeedOrder(data);
                    break;

                case OnlineActivityStepMode.GameCondition:
                    OnlineManagerHelper.ReceiveGameConditionOrder(data);
                    break;

                case OnlineActivityStepMode.Weather:
                    OnlineManagerHelper.ReceiveWeatherOrder(data);
                    break;

                case OnlineActivityStepMode.Kill:
                    OnlineManagerHelper.ReceiveKillOrder(data);
                    break;

                case OnlineActivityStepMode.Stop:
                    OnActivityStop();
                    break;
            }
        }

        public static void RequestOnlineActivity(OnlineActivityType toRequest)
        {
            if (ClientValues.currentRealTimeEvent != OnlineActivityType.None) DialogManager.PushNewDialog(new RT_Dialog_Error("You are already in a real time activity!"));
            else
            {
                OnlineManagerHelper.ClearAllQueues();
                ClientValues.ToggleRealTimeHost(false);

                DialogManager.PushNewDialog(new RT_Dialog_Wait("Waiting for server response"));

                OnlineActivityData data = new OnlineActivityData();
                data.ActivityStepMode = OnlineActivityStepMode.Request;
                data.ActivityType = toRequest;
                data.FromTile = Find.AnyPlayerHomeMap.Tile;
                data.TargetTile = ClientValues.chosenSettlement.Tile;
                data.CaravanHumans = OnlineManagerHelper.GetActivityHumans();
                data.CaravanAnimals = OnlineManagerHelper.GetActivityAnimals();

                Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.OnlineActivityPacket), data);
                Network.Listener.EnqueuePacket(packet);
            }
        }

        public static void RequestStopOnlineActivity()
        {
            OnlineActivityData data = new OnlineActivityData();
            data.ActivityStepMode = OnlineActivityStepMode.Stop;

            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.OnlineActivityPacket), data);
            Network.Listener.EnqueuePacket(packet);
        }

        private static void JoinMap(MapData MapData, OnlineActivityData activityData)
        {
            onlineMap = MapScribeManager.StringToMap(MapData, true, true, false, false, false, false);
            factionPawns = OnlineManagerHelper.GetCaravanPawns().ToList();
            mapThings = RimworldManager.GetThingsInMap(onlineMap).OrderBy(fetch => (fetch.PositionHeld.ToVector3() - Vector3.zero).sqrMagnitude).ToList();

            OnlineManagerHelper.SpawnMapPawns(activityData);

            OnlineManagerHelper.EnterMap(activityData);

            //ALWAYS BEFORE RECEIVING ANY ORDERS BECAUSE THEY WILL BE IGNORED OTHERWISE
            ClientValues.ToggleOnlineFunction(activityData.ActivityType);
            OnlineManagerHelper.ReceiveTimeSpeedOrder(activityData);
        }

        private static void SendRequestedMap(OnlineActivityData data)
        {
            data.ActivityStepMode = OnlineActivityStepMode.Accept;
            data.MapHumans = OnlineManagerHelper.GetActivityHumans();
            data.MapAnimals = OnlineManagerHelper.GetActivityAnimals();
            data.TimeSpeedOrder = OnlineManagerHelper.CreateTimeSpeedOrder();
            data.MapData = MapManager.ParseMap(onlineMap, true, false, false, true);

            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.OnlineActivityPacket), data);
            Network.Listener.EnqueuePacket(packet);
        }

        private static void OnActivityRequest(OnlineActivityData data)
        {
            Action r1 = delegate
            {
                OnlineManagerHelper.ClearAllQueues();
                ClientValues.ToggleRealTimeHost(true);

                onlineMap = Find.WorldObjects.Settlements.Find(fetch => fetch.Tile == data.TargetTile).Map;
                factionPawns = OnlineManagerHelper.GetMapPawns().ToList();
                mapThings = RimworldManager.GetThingsInMap(onlineMap).OrderBy(fetch => (fetch.PositionHeld.ToVector3() - Vector3.zero).sqrMagnitude).ToList();

                SendRequestedMap(data);

                OnlineManagerHelper.SpawnMapPawns(data);

                //ALWAYS LAST TO MAKE SURE WE DON'T SEND NON-NEEDED Details BEFORE EVERYTHING IS READY
                ClientValues.ToggleOnlineFunction(data.ActivityType);
            };

            Action r2 = delegate
            {
                data.ActivityStepMode = OnlineActivityStepMode.Reject;
                Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.OnlineActivityPacket), data);
                Network.Listener.EnqueuePacket(packet);
            };

            RT_Dialog_YesNo promptDialog = null;
            if (data.ActivityType == OnlineActivityType.Visit) promptDialog = new RT_Dialog_YesNo($"Visited by {data.OtherPlayerName}, accept?", r1, r2);
            else if (data.ActivityType == OnlineActivityType.Raid) promptDialog = new RT_Dialog_YesNo($"Raided by {data.OtherPlayerName}, accept?", r1, r2);

            DialogManager.PushNewDialog(promptDialog);
        }

        private static void OnActivityAccept(OnlineActivityData visitData)
        {
            DialogManager.PopWaitDialog();

            Action r1 = delegate { JoinMap(visitData.MapData, visitData); };
            Action r2 = delegate { RequestStopOnlineActivity(); };
            if (!ModManager.CheckIfMapHasConflictingMods(visitData.MapData)) r1.Invoke();
            else DialogManager.PushNewDialog(new RT_Dialog_YesNo("Map received but contains unknown mod data, continue?", r1, r2));
        }

        private static void OnActivityReject()
        {
            DialogManager.PopWaitDialog();
            DialogManager.PushNewDialog(new RT_Dialog_Error("Player rejected the activity!"));
        }

        private static void OnActivityUnavailable()
        {
            DialogManager.PopWaitDialog();
            DialogManager.PushNewDialog(new RT_Dialog_Error("Player must be online!"));
        }

        private static void OnActivityStop()
        {
            if (ClientValues.currentRealTimeEvent == OnlineActivityType.None) return;
            else
            {
                foreach (Pawn pawn in nonFactionPawns.ToArray())
                {
                    pawn.DeSpawn();

                    if (Find.WorldPawns.AllPawnsAliveOrDead.Contains(pawn)) Find.WorldPawns.RemovePawn(pawn);
                }

                if (!ClientValues.isRealTimeHost) CaravanExitMapUtility.ExitMapAndCreateCaravan(factionPawns, Faction.OfPlayer, onlineMap.Tile, Direction8Way.North, onlineMap.Tile);

                ClientValues.ToggleRealTimeHost(false);

                ClientValues.ToggleOnlineFunction(OnlineActivityType.None);

                DialogManager.PushNewDialog(new RT_Dialog_OK("Online activity ended"));
            }
        }
    }

    public static class OnlineManagerHelper
    {
        //Create orders

        public static PawnOrder CreatePawnOrder(Pawn pawn, Job newJob)
        {
            PawnOrder PawnOrder = new PawnOrder();
            PawnOrder.PawnIndex = OnlineActivityManager.factionPawns.IndexOf(pawn);

            PawnOrder.TargetCount = newJob.count;
            if (newJob.countQueue != null) PawnOrder.QueueTargetCounts = newJob.countQueue.ToArray();

            PawnOrder.DefName = newJob.def.defName;
            PawnOrder.Targets = GetActionTargets(newJob);
            PawnOrder.TargetIndexes = GetActionIndexes(newJob);
            PawnOrder.TargetTypes = GetActionTypes(newJob);
            PawnOrder.TargetFactions = GetActionTargetFactions(newJob);

            PawnOrder.QueueTargetsA = GetQueuedActionTargets(newJob, 0);
            PawnOrder.QueueTargetIndexesA = GetQueuedActionIndexes(newJob, 0);
            PawnOrder.QueueTargetTypesA = GetQueuedActionTypes(newJob, 0);
            PawnOrder.QueueTargetFactionsA = GetQueuedActionTargetFactions(newJob, 0);

            PawnOrder.QueueTargetsB = GetQueuedActionTargets(newJob, 1);
            PawnOrder.QueueTargetIndexesB = GetQueuedActionIndexes(newJob, 1);
            PawnOrder.QueueTargetTypesB = GetQueuedActionTypes(newJob, 1);
            PawnOrder.QueueTargetFactionsB = GetQueuedActionTargetFactions(newJob, 1);

            PawnOrder.IsDrafted = GetPawnDraftState(pawn);
            PawnOrder.UpdatedPosition = ValueParser.IntVec3ToArray(pawn.Position);
            PawnOrder.UpdatedRotation = ValueParser.Rot4ToInt(pawn.Rotation);

            return PawnOrder;
        }

        //This function doesn't take into account non-host thing creation right now, handle with care

        public static CreationOrder CreateCreationOrder(Thing thing)
        {
            CreationOrder CreationOrder = new CreationOrder();

            if (DeepScribeHelper.CheckIfThingIsHuman(thing)) CreationOrder.CreationType = CreationType.Human;
            else if (DeepScribeHelper.CheckIfThingIsAnimal(thing)) CreationOrder.CreationType = CreationType.Animal;
            else CreationOrder.CreationType = CreationType.Thing;

            if (CreationOrder.CreationType == CreationType.Human) CreationOrder.DataToCreate = Serializer.ConvertObjectToBytes(HumanScribeManager.HumanToString((Pawn)thing));
            else if (CreationOrder.CreationType == CreationType.Animal) CreationOrder.DataToCreate = Serializer.ConvertObjectToBytes(AnimalScribeManager.AnimalToString((Pawn)thing));
            else
            {
                //Modify position based on center cell because RimWorld doesn't store it by default
                thing.Position = thing.OccupiedRect().CenterCell;
                CreationOrder.DataToCreate = Serializer.ConvertObjectToBytes(ThingScribeManager.ItemToString(thing, thing.stackCount));
            }

            return CreationOrder;
        }

        public static DestructionOrder CreateDestructionOrder(Thing thing)
        {
            DestructionOrder DestructionOrder = new DestructionOrder();
            DestructionOrder.IndexToDestroy = OnlineActivityManager.mapThings.IndexOf(thing);

            return DestructionOrder;
        }

        public static DamageOrder CreateDamageOrder(DamageInfo damageInfo, Thing afectedThing)
        {
            DamageOrder DamageOrder = new DamageOrder();
            DamageOrder.DefName = damageInfo.Def.defName;
            DamageOrder.DamageAmount = damageInfo.Amount;
            DamageOrder.IgnoreArmor = damageInfo.IgnoreArmor;
            DamageOrder.ArmorPenetration = damageInfo.ArmorPenetrationInt;
            DamageOrder.TargetIndex = OnlineActivityManager.mapThings.IndexOf(afectedThing);
            if (damageInfo.Weapon != null) DamageOrder.WeaponDefName = damageInfo.Weapon.defName;
            if (damageInfo.HitPart != null) DamageOrder.HitPartDefName = damageInfo.HitPart.def.defName;

            return DamageOrder;
        }

        public static HediffOrder CreateHediffOrder(Hediff hediff, Pawn pawn, OnlineActivityApplyMode ApplyMode)
        {
            HediffOrder HediffOrder = new HediffOrder();
            HediffOrder.ApplyMode = ApplyMode;

            //Invert the enum because it needs to be mirrored for the non-host

            if (OnlineActivityManager.factionPawns.Contains(pawn))
            {
                HediffOrder.PawnFaction = OnlineActivityTargetFaction.NonFaction;
                HediffOrder.HediffTargetIndex = OnlineActivityManager.factionPawns.IndexOf(pawn);
            }

            else
            {
                HediffOrder.PawnFaction = OnlineActivityTargetFaction.Faction;
                HediffOrder.HediffTargetIndex = OnlineActivityManager.nonFactionPawns.IndexOf(pawn);
            }

            HediffOrder.HediffDefName = hediff.def.defName;
            if (hediff.Part != null) HediffOrder.HediffPartDefName = hediff.Part.def.defName;
            if (hediff.sourceDef != null) HediffOrder.HediffWeaponDefName = hediff.sourceDef.defName;
            HediffOrder.HediffSeverity = hediff.Severity;
            HediffOrder.HediffPermanent = hediff.IsPermanent();

            return HediffOrder;
        }

        public static TimeSpeedOrder CreateTimeSpeedOrder()
        {
            TimeSpeedOrder TimeSpeedOrder = new TimeSpeedOrder();
            TimeSpeedOrder.TargetTimeSpeed = OnlineActivityManager.queuedTimeSpeed;
            TimeSpeedOrder.TargetMapTicks = RimworldManager.GetGameTicks();

            return TimeSpeedOrder;
        }

        public static GameConditionOrder CreateGameConditionOrder(GameCondition gameCondition, OnlineActivityApplyMode ApplyMode)
        {
            GameConditionOrder GameConditionOrder = new GameConditionOrder();            
            GameConditionOrder.ConditionDefName = gameCondition.def.defName;
            GameConditionOrder.Duration = gameCondition.Duration;
            GameConditionOrder.ApplyMode = ApplyMode;

            return GameConditionOrder;
        }

        public static WeatherOrder CreateWeatherOrder(WeatherDef weatherDef)
        {
            WeatherOrder weatherOrder = new WeatherOrder();
            weatherOrder.WeatherDefName = weatherDef.defName;

            return weatherOrder;
        }

        public static KillOrder CreateKillOrder(Thing instance)
        {
            KillOrder killOrder = new KillOrder();

            //Invert the enum because it needs to be mirrored for the non-host

            if (OnlineActivityManager.factionPawns.Contains(instance))
            {
                killOrder.PawnFaction = OnlineActivityTargetFaction.NonFaction;
                killOrder.KillTargetIndex = OnlineActivityManager.factionPawns.IndexOf((Pawn)instance);
            }

            else
            {
                killOrder.PawnFaction = OnlineActivityTargetFaction.Faction;
                killOrder.KillTargetIndex = OnlineActivityManager.nonFactionPawns.IndexOf((Pawn)instance);
            }

            return killOrder;
        }

        //Receive orders

        public static void ReceivePawnOrder(OnlineActivityData data)
        {
            if (ClientValues.currentRealTimeEvent == OnlineActivityType.None) return;

            try
            {
                Pawn pawn = OnlineActivityManager.nonFactionPawns[data.PawnOrder.PawnIndex];
                IntVec3 jobPositionStart = ValueParser.ArrayToIntVec3(data.PawnOrder.UpdatedPosition);
                Rot4 jobRotationStart = ValueParser.IntToRot4(data.PawnOrder.UpdatedRotation);
                ChangePawnTransform(pawn, jobPositionStart, jobRotationStart);
                SetPawnDraftState(pawn, data.PawnOrder.IsDrafted);

                JobDef jobDef = RimworldManager.GetJobFromDef(data.PawnOrder.DefName);
                LocalTargetInfo targetA = SetActionTargetsFromString(data.PawnOrder, 0);
                LocalTargetInfo targetB = SetActionTargetsFromString(data.PawnOrder, 1);
                LocalTargetInfo targetC = SetActionTargetsFromString(data.PawnOrder, 2);
                LocalTargetInfo[] targetQueueA = SetQueuedActionTargetsFromString(data.PawnOrder, 0);
                LocalTargetInfo[] targetQueueB = SetQueuedActionTargetsFromString(data.PawnOrder, 1);

                Job newJob = RimworldManager.SetJobFromDef(jobDef, targetA, targetB, targetC);
                newJob.count = data.PawnOrder.TargetCount;
                if (data.PawnOrder.QueueTargetCounts != null) newJob.countQueue = data.PawnOrder.QueueTargetCounts.ToList();

                foreach (LocalTargetInfo target in targetQueueA) newJob.AddQueuedTarget(TargetIndex.A, target);
                foreach (LocalTargetInfo target in targetQueueB) newJob.AddQueuedTarget(TargetIndex.B, target);

                EnqueueThing(pawn);
                ChangeCurrentJob(pawn, newJob);
                ChangeJobSpeedIfNeeded(newJob);
            }
            catch { Logger.Warning($"Couldn't set order for pawn with index '{data.PawnOrder.PawnIndex}'"); }
        }

        //This function doesn't take into account non-host thing creation right now, handle with care

        public static void ReceiveCreationOrder(OnlineActivityData data)
        {
            if (ClientValues.currentRealTimeEvent == OnlineActivityType.None) return;

            Thing toSpawn;
            if (data.CreationOrder.CreationType == CreationType.Human)
            {
                HumanData humanData = Serializer.ConvertBytesToObject<HumanData>(data.CreationOrder.DataToCreate);
                toSpawn = HumanScribeManager.StringToHuman(humanData);
                toSpawn.SetFaction(FactionValues.allyPlayer);
            }

            else if (data.CreationOrder.CreationType == CreationType.Animal)
            {
                AnimalData animalData = Serializer.ConvertBytesToObject<AnimalData>(data.CreationOrder.DataToCreate);
                toSpawn = AnimalScribeManager.StringToAnimal(animalData);
                toSpawn.SetFaction(FactionValues.allyPlayer);
            }

            else
            {
                ThingData thingData = Serializer.ConvertBytesToObject<ThingData>(data.CreationOrder.DataToCreate);
                toSpawn = ThingScribeManager.StringToItem(thingData);
            }

            EnqueueThing(toSpawn);

            //Request
            RimworldManager.PlaceThingIntoMap(toSpawn, OnlineActivityManager.onlineMap);
        }

        public static void ReceiveDestructionOrder(OnlineActivityData data)
        {
            if (ClientValues.currentRealTimeEvent == OnlineActivityType.None) return;

            Thing toDestroy = OnlineActivityManager.mapThings[data.DestructionOrder.IndexToDestroy];

            //Request
            if (ClientValues.isRealTimeHost) toDestroy.Destroy(DestroyMode.Deconstruct);
            else
            {
                EnqueueThing(toDestroy);
                toDestroy.Destroy(DestroyMode.Vanish);
            }
        }

        public static void ReceiveDamageOrder(OnlineActivityData data)
        {
            if (ClientValues.currentRealTimeEvent == OnlineActivityType.None) return;

            try
            {
                BodyPartRecord bodyPartRecord = new BodyPartRecord();
                bodyPartRecord.def = DefDatabase<BodyPartDef>.AllDefs.FirstOrDefault(fetch => fetch.defName == data.DamageOrder.HitPartDefName);

                DamageDef damageDef = DefDatabase<DamageDef>.AllDefs.First(fetch => fetch.defName == data.DamageOrder.DefName);
                ThingDef thingDef = DefDatabase<ThingDef>.AllDefs.FirstOrDefault(fetch => fetch.defName == data.DamageOrder.WeaponDefName);

                DamageInfo damageInfo = new DamageInfo(damageDef, data.DamageOrder.DamageAmount, data.DamageOrder.ArmorPenetration, -1, null, bodyPartRecord, thingDef);
                damageInfo.SetIgnoreArmor(data.DamageOrder.IgnoreArmor);

                Thing toApplyTo = OnlineActivityManager.mapThings[data.DamageOrder.TargetIndex];

                EnqueueThing(toApplyTo);

                //Request
                toApplyTo.TakeDamage(damageInfo);
            }
            catch (Exception e) { Logger.Warning($"Couldn't apply damage order. Reason: {e}"); }
        }

        public static void ReceiveHediffOrder(OnlineActivityData data)
        {
            if (ClientValues.currentRealTimeEvent == OnlineActivityType.None) return;

            try
            {
                Pawn toTarget = null;
                if (data.HediffOrder.PawnFaction == OnlineActivityTargetFaction.Faction) toTarget = OnlineActivityManager.factionPawns[data.HediffOrder.HediffTargetIndex];
                else toTarget = OnlineActivityManager.nonFactionPawns[data.HediffOrder.HediffTargetIndex];

                EnqueueThing(toTarget);

                BodyPartRecord bodyPartRecord = toTarget.RaceProps.body.AllParts.FirstOrDefault(fetch => fetch.def.defName == data.HediffOrder.HediffPartDefName);

                if (data.HediffOrder.ApplyMode == OnlineActivityApplyMode.Add)
                {
                    HediffDef hediffDef = DefDatabase<HediffDef>.AllDefs.First(fetch => fetch.defName == data.HediffOrder.HediffDefName);
                    Hediff toMake = HediffMaker.MakeHediff(hediffDef, toTarget, bodyPartRecord);
                    
                    if (data.HediffOrder.HediffWeaponDefName != null)
                    {
                        ThingDef source = DefDatabase<ThingDef>.AllDefs.First(fetch => fetch.defName == data.HediffOrder.HediffWeaponDefName);
                        toMake.sourceDef = source;
                        toMake.sourceLabel = source.label;
                    }

                    toMake.Severity = data.HediffOrder.HediffSeverity;

                    if (data.HediffOrder.HediffPermanent)
                    {
                        HediffComp_GetsPermanent hediffComp = toMake.TryGetComp<HediffComp_GetsPermanent>();
                        hediffComp.IsPermanent = true;
                    }

                    //Request
                    toTarget.health.AddHediff(toMake, bodyPartRecord);
                }

                else
                {
                    Hediff hediff = toTarget.health.hediffSet.hediffs.First(fetch => fetch.def.defName == data.HediffOrder.HediffDefName &&
                        fetch.Part.def.defName == bodyPartRecord.def.defName);

                    //Request
                    toTarget.health.RemoveHediff(hediff);
                }
            }
            catch (Exception e) { Logger.Warning($"Couldn't apply hediff order. Reason: {e}"); }
        }

        public static void ReceiveTimeSpeedOrder(OnlineActivityData data)
        {
            if (ClientValues.currentRealTimeEvent == OnlineActivityType.None) return;

            try
            {
                EnqueueTimeSpeed(data.TimeSpeedOrder.TargetTimeSpeed);
                RimworldManager.SetGameTicks(data.TimeSpeedOrder.TargetMapTicks);
            }
            catch (Exception e) { Logger.Warning($"Couldn't apply time speed order. Reason: {e}"); }
        }

        public static void ReceiveGameConditionOrder(OnlineActivityData data)
        {
            if (ClientValues.currentRealTimeEvent == OnlineActivityType.None) return;

            try
            {
                GameCondition gameCondition = null;

                if (data.GameConditionOrder.ApplyMode == OnlineActivityApplyMode.Add)
                {
                    GameConditionDef conditionDef = DefDatabase<GameConditionDef>.AllDefs.First(fetch => fetch.defName == data.GameConditionOrder.ConditionDefName);
                    gameCondition = GameConditionMaker.MakeCondition(conditionDef);
                    gameCondition.Duration = data.GameConditionOrder.Duration;
                    EnqueueGameCondition(gameCondition);

                    //Request
                    Find.World.gameConditionManager.RegisterCondition(gameCondition);
                }

                else
                {
                    gameCondition = Find.World.gameConditionManager.ActiveConditions.First(fetch => fetch.def.defName == data.GameConditionOrder.ConditionDefName);
                    EnqueueGameCondition(gameCondition);

                    //Request
                    gameCondition.End();
                }
            }
            catch (Exception e) { Logger.Warning($"Couldn't apply game condition order. Reason: {e}"); }
        }

        public static void ReceiveWeatherOrder(OnlineActivityData data)
        {
            if (ClientValues.currentRealTimeEvent == OnlineActivityType.None) return;

            try
            {
                WeatherDef weatherDef = DefDatabase<WeatherDef>.AllDefs.First(fetch => fetch.defName == data.WeatherOrder.WeatherDefName);

                EnqueueWeather(weatherDef);

                //Request
                OnlineActivityManager.onlineMap.weatherManager.TransitionTo(weatherDef);
            }
            catch (Exception e) { Logger.Warning($"Couldn't apply weather order. Reason: {e}"); }
        }

        public static void ReceiveKillOrder(OnlineActivityData data)
        {
            if (ClientValues.currentRealTimeEvent == OnlineActivityType.None) return;

            try
            {
                Pawn toTarget = null;
                if (data.KillOrder.PawnFaction == OnlineActivityTargetFaction.Faction) toTarget = OnlineActivityManager.factionPawns[data.KillOrder.KillTargetIndex];
                else toTarget = OnlineActivityManager.nonFactionPawns[data.KillOrder.KillTargetIndex];

                EnqueueThing(toTarget);

                //Request
                toTarget.Kill(null);
            }
            catch (Exception e) { Logger.Warning($"Couldn't apply kill order. Reason: {e}"); }
        }

        //Misc

        //This function doesn't take into account non-host thing creation right now, handle with care

        public static void AddThingToMap(Thing thing)
        {
            if (DeepScribeHelper.CheckIfThingIsHuman(thing) || DeepScribeHelper.CheckIfThingIsAnimal(thing))
            {
                if (ClientValues.isRealTimeHost) OnlineActivityManager.factionPawns.Add((Pawn)thing);
                else OnlineActivityManager.nonFactionPawns.Add((Pawn)thing);
            }
            else OnlineActivityManager.mapThings.Add(thing);
        }

        public static void RemoveThingFromMap(Thing thing)
        {
            if (OnlineActivityManager.factionPawns.Contains(thing)) OnlineActivityManager.factionPawns.Remove((Pawn)thing);
            else if (OnlineActivityManager.nonFactionPawns.Contains(thing)) OnlineActivityManager.nonFactionPawns.Remove((Pawn)thing);
            else OnlineActivityManager.mapThings.Remove(thing);
        }

        public static void ClearAllQueues()
        {
            ClearThingQueue();
            ClearTimeSpeedQueue();
            ClearWeatherQueue();
            ClearGameConditionQueue();
        }

        public static void EnqueueThing(Thing thing) { OnlineActivityManager.queuedThing = thing; }

        public static void EnqueueTimeSpeed(int timeSpeed) { OnlineActivityManager.queuedTimeSpeed = timeSpeed; }

        public static void EnqueueWeather(WeatherDef weatherDef) { OnlineActivityManager.queuedWeather = weatherDef; }

        public static void EnqueueGameCondition(GameCondition gameCondition) { OnlineActivityManager.queuedGameCondition = gameCondition; }

        public static void ClearThingQueue() { OnlineActivityManager.queuedThing = null; }

        public static void ClearTimeSpeedQueue() { OnlineActivityManager.queuedTimeSpeed = 0; }

        public static void ClearWeatherQueue() { OnlineActivityManager.queuedWeather = null; }

        public static void ClearGameConditionQueue() { OnlineActivityManager.queuedGameCondition = null; }

        public static string[] GetActionTargets(Job job)
        {
            List<string> targetInfoList = new List<string>();

            for (int i = 0; i < 3; i++)
            {
                LocalTargetInfo target = null;
                if (i == 0) target = job.targetA;
                else if (i == 1) target = job.targetB;
                else if (i == 2) target = job.targetC;

                try
                {
                    if (target.Thing == null) targetInfoList.Add(ValueParser.Vector3ToString(target.Cell));
                    else
                    {
                        if (DeepScribeHelper.CheckIfThingIsHuman(target.Thing)) targetInfoList.Add(Serializer.SerializeToString(HumanScribeManager.HumanToString(target.Pawn)));
                        else if (DeepScribeHelper.CheckIfThingIsAnimal(target.Thing)) targetInfoList.Add(Serializer.SerializeToString(AnimalScribeManager.AnimalToString(target.Pawn)));
                        else targetInfoList.Add(Serializer.SerializeToString(ThingScribeManager.ItemToString(target.Thing, target.Thing.stackCount)));
                    }
                }
                catch { Logger.Error($"failed to parse {target}"); }
            }

            return targetInfoList.ToArray();
        }

        public static string[] GetQueuedActionTargets(Job job, int index)
        {
            List<string> targetInfoList = new List<string>();

            List<LocalTargetInfo> selectedQueue = new List<LocalTargetInfo>();
            if (index == 0) selectedQueue = job.targetQueueA;
            else if (index == 1) selectedQueue = job.targetQueueB;

            if (selectedQueue == null) return targetInfoList.ToArray();
            for (int i = 0; i < selectedQueue.Count; i++)
            {
                try
                {
                    if (selectedQueue[i].Thing == null) targetInfoList.Add(ValueParser.Vector3ToString(selectedQueue[i].Cell));
                    else
                    {
                        if (DeepScribeHelper.CheckIfThingIsHuman(selectedQueue[i].Thing)) targetInfoList.Add(Serializer.SerializeToString(HumanScribeManager.HumanToString(selectedQueue[i].Pawn)));
                        else if (DeepScribeHelper.CheckIfThingIsAnimal(selectedQueue[i].Thing)) targetInfoList.Add(Serializer.SerializeToString(AnimalScribeManager.AnimalToString(selectedQueue[i].Pawn)));
                        else targetInfoList.Add(Serializer.SerializeToString(ThingScribeManager.ItemToString(selectedQueue[i].Thing, 1)));
                    }
                }
                catch { Logger.Error($"failed to parse {selectedQueue[i]}"); }
            }

            return targetInfoList.ToArray();
        }

        public static LocalTargetInfo SetActionTargetsFromString(PawnOrder PawnOrder, int index)
        {
            LocalTargetInfo toGet = LocalTargetInfo.Invalid;

            try
            {
                switch (PawnOrder.TargetTypes[index])
                {
                    case ActionTargetType.Thing:
                        toGet = new LocalTargetInfo(OnlineActivityManager.mapThings[PawnOrder.TargetIndexes[index]]);
                        break;

                    case ActionTargetType.Human:
                        if (PawnOrder.TargetFactions[index] == OnlineActivityTargetFaction.Faction)
                        {
                            toGet = new LocalTargetInfo(OnlineActivityManager.factionPawns[PawnOrder.TargetIndexes[index]]);
                        }
                        else if (PawnOrder.TargetFactions[index] == OnlineActivityTargetFaction.NonFaction)
                        {
                            toGet = new LocalTargetInfo(OnlineActivityManager.nonFactionPawns[PawnOrder.TargetIndexes[index]]);
                        }
                        break;

                    case ActionTargetType.Animal:
                        if (PawnOrder.TargetFactions[index] == OnlineActivityTargetFaction.Faction)
                        {
                            toGet = new LocalTargetInfo(OnlineActivityManager.factionPawns[PawnOrder.TargetIndexes[index]]);
                        }
                        else if (PawnOrder.TargetFactions[index] == OnlineActivityTargetFaction.NonFaction)
                        {
                            toGet = new LocalTargetInfo(OnlineActivityManager.nonFactionPawns[PawnOrder.TargetIndexes[index]]);
                        }
                        break;

                    case ActionTargetType.Cell:
                        toGet = new LocalTargetInfo(ValueParser.StringToVector3(PawnOrder.Targets[index]));
                        break;
                }
            }
            catch (Exception e) { Logger.Error(e.ToString()); }

            return toGet;
        }

        public static LocalTargetInfo[] SetQueuedActionTargetsFromString(PawnOrder PawnOrder, int index)
        {
            List<LocalTargetInfo> toGet = new List<LocalTargetInfo>();

            int[] actionTargetIndexes = null;
            string[] actionTargets = null;
            ActionTargetType[] actionTargetTypes = null;

            if (index == 0)
            {
                actionTargetIndexes = PawnOrder.QueueTargetIndexesA.ToArray();
                actionTargets = PawnOrder.QueueTargetsA.ToArray();
                actionTargetTypes = PawnOrder.QueueTargetTypesA.ToArray();
            }

            else if (index == 1)
            {
                actionTargetIndexes = PawnOrder.QueueTargetIndexesB.ToArray();
                actionTargets = PawnOrder.QueueTargetsB.ToArray();
                actionTargetTypes = PawnOrder.QueueTargetTypesB.ToArray();
            }

            for (int i = 0; i < actionTargets.Length; i++)
            {
                try
                {
                    switch (actionTargetTypes[index])
                    {
                        case ActionTargetType.Thing:
                            toGet.Add(new LocalTargetInfo(OnlineActivityManager.mapThings[actionTargetIndexes[i]]));
                            break;

                        case ActionTargetType.Human:
                            if (PawnOrder.TargetFactions[index] == OnlineActivityTargetFaction.Faction)
                            {
                                toGet.Add(new LocalTargetInfo(OnlineActivityManager.factionPawns[PawnOrder.TargetIndexes[index]]));
                            }
                            else if (PawnOrder.TargetFactions[index] == OnlineActivityTargetFaction.NonFaction)
                            {
                                toGet.Add(new LocalTargetInfo(OnlineActivityManager.nonFactionPawns[PawnOrder.TargetIndexes[index]]));
                            }
                            break;

                        case ActionTargetType.Animal:
                            if (PawnOrder.TargetFactions[index] == OnlineActivityTargetFaction.Faction)
                            {
                                toGet.Add(new LocalTargetInfo(OnlineActivityManager.factionPawns[PawnOrder.TargetIndexes[index]]));
                            }
                            else if (PawnOrder.TargetFactions[index] == OnlineActivityTargetFaction.NonFaction)
                            {
                                toGet.Add(new LocalTargetInfo(OnlineActivityManager.nonFactionPawns[PawnOrder.TargetIndexes[index]]));
                            }
                            break;

                        case ActionTargetType.Cell:
                            toGet.Add(new LocalTargetInfo(ValueParser.StringToVector3(actionTargets[i])));
                            break;
                    }
                }
                catch (Exception e) { Logger.Error(e.ToString()); }
            }

            return toGet.ToArray();
        }

        public static OnlineActivityTargetFaction[] GetActionTargetFactions(Job job)
        {
            List<OnlineActivityTargetFaction> TargetFactions = new List<OnlineActivityTargetFaction>();

            for (int i = 0; i < 3; i++)
            {
                LocalTargetInfo target = null;
                if (i == 0) target = job.targetA;
                else if (i == 1) target = job.targetB;
                else if (i == 2) target = job.targetC;

                try
                {
                    if (target.Thing == null) TargetFactions.Add(OnlineActivityTargetFaction.None);
                    else
                    {
                        //Faction and non-faction pawns get inverted in here to send into the other side

                        if (OnlineActivityManager.factionPawns.Contains(target.Thing)) TargetFactions.Add(OnlineActivityTargetFaction.NonFaction);
                        else if (OnlineActivityManager.nonFactionPawns.Contains(target.Thing)) TargetFactions.Add(OnlineActivityTargetFaction.Faction);
                        else if (OnlineActivityManager.mapThings.Contains(target.Thing)) TargetFactions.Add(OnlineActivityTargetFaction.None);
                    }
                }
                catch { Logger.Error($"failed to parse {target}"); }
            }

            return TargetFactions.ToArray();
        }

        public static OnlineActivityTargetFaction[] GetQueuedActionTargetFactions(Job job, int index)
        {
            List<OnlineActivityTargetFaction> TargetFactions = new List<OnlineActivityTargetFaction>();

            List<LocalTargetInfo> selectedQueue = new List<LocalTargetInfo>();
            if (index == 0) selectedQueue = job.targetQueueA;
            else if (index == 1) selectedQueue = job.targetQueueB;

            if (selectedQueue == null) return TargetFactions.ToArray();
            for (int i = 0; i < selectedQueue.Count; i++)
            {
                try
                {
                    if (selectedQueue[i].Thing == null) TargetFactions.Add(OnlineActivityTargetFaction.None);
                    else
                    {
                        //Faction and non-faction pawns get inverted in here to send into the other side

                        if (OnlineActivityManager.factionPawns.Contains(selectedQueue[i].Thing)) TargetFactions.Add(OnlineActivityTargetFaction.NonFaction);
                        else if (OnlineActivityManager.nonFactionPawns.Contains(selectedQueue[i].Thing)) TargetFactions.Add(OnlineActivityTargetFaction.Faction);
                        else if (OnlineActivityManager.mapThings.Contains(selectedQueue[i].Thing)) TargetFactions.Add(OnlineActivityTargetFaction.None);
                    }
                }
                catch { Logger.Error($"failed to parse {selectedQueue[i]}"); }
            }

            return TargetFactions.ToArray();
        }

        public static int[] GetActionIndexes(Job job)
        {
            List<int> TargetIndexList = new List<int>();

            for (int i = 0; i < 3; i++)
            {
                LocalTargetInfo target = null;
                if (i == 0) target = job.targetA;
                else if (i == 1) target = job.targetB;
                else if (i == 2) target = job.targetC;

                try
                {
                    if (target.Thing == null) TargetIndexList.Add(0);
                    else
                    {
                        if (OnlineActivityManager.factionPawns.Contains(target.Thing)) TargetIndexList.Add(OnlineActivityManager.factionPawns.IndexOf((Pawn)target.Thing));
                        else if (OnlineActivityManager.nonFactionPawns.Contains(target.Thing)) TargetIndexList.Add(OnlineActivityManager.nonFactionPawns.IndexOf((Pawn)target.Thing));
                        else if (OnlineActivityManager.mapThings.Contains(target.Thing)) TargetIndexList.Add(OnlineActivityManager.mapThings.IndexOf(target.Thing));
                    }
                }
                catch { Logger.Error($"failed to parse {target}"); }
            }

            return TargetIndexList.ToArray();
        }

        public static int[] GetQueuedActionIndexes(Job job, int index)
        {
            List<int> TargetIndexList = new List<int>();

            List<LocalTargetInfo> selectedQueue = new List<LocalTargetInfo>();
            if (index == 0) selectedQueue = job.targetQueueA;
            else if (index == 1) selectedQueue = job.targetQueueB;

            if (selectedQueue == null) return TargetIndexList.ToArray();
            for (int i = 0; i < selectedQueue.Count; i++)
            {
                try
                {
                    if (selectedQueue[i].Thing == null) TargetIndexList.Add(0);
                    else
                    {
                        if (OnlineActivityManager.factionPawns.Contains(selectedQueue[i].Thing)) TargetIndexList.Add(OnlineActivityManager.factionPawns.IndexOf((Pawn)selectedQueue[i].Thing));
                        else if (OnlineActivityManager.nonFactionPawns.Contains(selectedQueue[i].Thing)) TargetIndexList.Add(OnlineActivityManager.nonFactionPawns.IndexOf((Pawn)selectedQueue[i].Thing));
                        else if (OnlineActivityManager.mapThings.Contains(selectedQueue[i].Thing)) TargetIndexList.Add(OnlineActivityManager.mapThings.IndexOf(selectedQueue[i].Thing));
                    }
                }
                catch { Logger.Error($"failed to parse {selectedQueue[i]}"); }
            }

            return TargetIndexList.ToArray();
        }

        public static ActionTargetType[] GetActionTypes(Job job)
        {
            List<ActionTargetType> targetTypeList = new List<ActionTargetType>();

            for (int i = 0; i < 3; i++)
            {
                LocalTargetInfo target = null;
                if (i == 0) target = job.targetA;
                else if (i == 1) target = job.targetB;
                else if (i == 2) target = job.targetC;

                try
                {
                    if (target.Thing == null) targetTypeList.Add(ActionTargetType.Cell);
                    else
                    {
                        if (DeepScribeHelper.CheckIfThingIsHuman(target.Thing)) targetTypeList.Add(ActionTargetType.Human);
                        else if (DeepScribeHelper.CheckIfThingIsAnimal(target.Thing)) targetTypeList.Add(ActionTargetType.Animal);
                        else targetTypeList.Add(ActionTargetType.Thing);
                    }
                }
                catch { Logger.Error($"failed to parse {target}"); }
            }

            return targetTypeList.ToArray();
        }

        public static ActionTargetType[] GetQueuedActionTypes(Job job, int index)
        {
            List<ActionTargetType> targetTypeList = new List<ActionTargetType>();

            List<LocalTargetInfo> selectedQueue = new List<LocalTargetInfo>();
            if (index == 0) selectedQueue = job.targetQueueA;
            else if (index == 1) selectedQueue = job.targetQueueB;

            if (selectedQueue == null) return targetTypeList.ToArray();
            for (int i = 0; i < selectedQueue.Count; i++)
            {
                try
                {
                    if (selectedQueue[i].Thing == null) targetTypeList.Add(ActionTargetType.Cell);
                    else
                    {
                        if (DeepScribeHelper.CheckIfThingIsHuman(selectedQueue[i].Thing)) targetTypeList.Add(ActionTargetType.Human);
                        else if (DeepScribeHelper.CheckIfThingIsAnimal(selectedQueue[i].Thing)) targetTypeList.Add(ActionTargetType.Animal);
                        else targetTypeList.Add(ActionTargetType.Thing);
                    }
                }
                catch { Logger.Error($"failed to parse {selectedQueue[i]}"); }
            }

            return targetTypeList.ToArray();
        }

        public static void SetPawnDraftState(Pawn pawn, bool shouldBeDrafted)
        {
            try
            {
                pawn.drafter ??= new Pawn_DraftController(pawn);

                if (shouldBeDrafted) pawn.drafter.Drafted = true;
                else { pawn.drafter.Drafted = false; }
            }
            catch (Exception e) { Logger.Warning($"Couldn't apply pawn draft state for {pawn.Label}. Reason: {e}"); }
        }

        public static bool GetPawnDraftState(Pawn pawn)
        {
            if (pawn.drafter == null) return false;
            else return pawn.drafter.Drafted;
        }

        public static void ChangePawnTransform(Pawn pawn, IntVec3 pawnPosition, Rot4 pawnRotation)
        {
            pawn.Position = pawnPosition;
            pawn.Rotation = pawnRotation;
            pawn.pather.Notify_Teleported_Int();
        }

        public static void ChangeCurrentJob(Pawn pawn, Job newJob)
        {
            pawn.jobs.ClearQueuedJobs();
            if (pawn.jobs.curJob != null) pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, false);

            //TODO
            //Investigate if this can be implemented
            //pawn.Reserve(newJob.targetA, newJob);

            newJob.TryMakePreToilReservations(pawn, false);
            pawn.jobs.StartJob(newJob);
        }

        public static void ChangeJobSpeedIfNeeded(Job job)
        {
            if (job.def == JobDefOf.GotoWander) job.locomotionUrgency = LocomotionUrgency.Walk;
            else if (job.def == JobDefOf.Wait_Wander) job.locomotionUrgency = LocomotionUrgency.Walk;
        }

        public static void SpawnMapPawns(OnlineActivityData activityData)
        {
            if (ClientValues.isRealTimeHost)
            {
                OnlineActivityManager.nonFactionPawns = GetCaravanPawns(activityData).ToList();
                foreach (Pawn pawn in OnlineActivityManager.nonFactionPawns)
                {
                    if (activityData.ActivityType == OnlineActivityType.Visit) pawn.SetFaction(FactionValues.allyPlayer);
                    else if (activityData.ActivityType == OnlineActivityType.Raid) pawn.SetFaction(FactionValues.enemyPlayer);

                    //Initial position and rotation left to default since caravan doesn't have it stored
                    GenSpawn.Spawn(pawn, OnlineActivityManager.onlineMap.Center, OnlineActivityManager.onlineMap, Rot4.Random);
                }
            }

            else
            {
                OnlineActivityManager.nonFactionPawns = GetMapPawns(activityData).ToList();
                foreach (Pawn pawn in OnlineActivityManager.nonFactionPawns)
                {
                    if (activityData.ActivityType == OnlineActivityType.Visit) pawn.SetFaction(FactionValues.allyPlayer);
                    else if (activityData.ActivityType == OnlineActivityType.Raid) pawn.SetFaction(FactionValues.enemyPlayer);

                    //Initial position and rotation grabbed from online Details
                    GenSpawn.Spawn(pawn, pawn.Position, OnlineActivityManager.onlineMap, pawn.Rotation);
                }
            }
        }

        public static Pawn[] GetMapPawns(OnlineActivityData activityData = null)
        {
            if (ClientValues.isRealTimeHost)
            {
                List<Pawn> MapHumans = OnlineActivityManager.onlineMap.mapPawns.AllPawns
                    .FindAll(fetch => DeepScribeHelper.CheckIfThingIsHuman(fetch))
                    .OrderBy(p => p.def.defName)
                    .ToList();

                List<Pawn> MapAnimals = OnlineActivityManager.onlineMap.mapPawns.AllPawns
                    .FindAll(fetch => DeepScribeHelper.CheckIfThingIsAnimal(fetch))
                    .OrderBy(p => p.def.defName)
                    .ToList();

                List<Pawn> allPawns = new List<Pawn>();
                foreach (Pawn pawn in MapHumans) allPawns.Add(pawn);
                foreach (Pawn pawn in MapAnimals) allPawns.Add(pawn);

                return allPawns.ToArray();
            }

            else
            {
                List<Pawn> pawnList = new List<Pawn>();

                foreach (HumanData humanData in activityData.MapHumans)
                {
                    Pawn human = HumanScribeManager.StringToHuman(humanData);
                    pawnList.Add(human);
                }

                foreach (AnimalData animalData in activityData.MapAnimals)
                {
                    Pawn animal = AnimalScribeManager.StringToAnimal(animalData);
                    pawnList.Add(animal);
                }

                return pawnList.ToArray();
            }
        }

        public static Pawn[] GetCaravanPawns(OnlineActivityData activityData = null)
        {
            if (ClientValues.isRealTimeHost)
            {
                List<Pawn> pawnList = new List<Pawn>();

                foreach (HumanData humanData in activityData.CaravanHumans)
                {
                    Pawn human = HumanScribeManager.StringToHuman(humanData);
                    pawnList.Add(human);
                }

                foreach (AnimalData animalData in activityData.CaravanAnimals)
                {
                    Pawn animal = AnimalScribeManager.StringToAnimal(animalData);
                    pawnList.Add(animal);
                }

                return pawnList.ToArray();
            }

            else
            {
                List<Pawn> CaravanHumans = ClientValues.chosenCaravan.PawnsListForReading
                    .FindAll(fetch => DeepScribeHelper.CheckIfThingIsHuman(fetch))
                    .OrderBy(p => p.def.defName)
                    .ToList();

                List<Pawn> CaravanAnimals = ClientValues.chosenCaravan.PawnsListForReading
                    .FindAll(fetch => DeepScribeHelper.CheckIfThingIsAnimal(fetch))
                    .OrderBy(p => p.def.defName)
                    .ToList();

                List<Pawn> allPawns = new List<Pawn>();
                foreach (Pawn pawn in CaravanHumans) allPawns.Add(pawn);
                foreach (Pawn pawn in CaravanAnimals) allPawns.Add(pawn);

                return allPawns.ToArray();
            }
        }

        public static List<HumanData> GetActivityHumans()
        {
            if (ClientValues.isRealTimeHost)
            {
                List<Pawn> MapHumans = OnlineActivityManager.onlineMap.mapPawns.AllPawns
                    .FindAll(fetch => DeepScribeHelper.CheckIfThingIsHuman(fetch))
                    .OrderBy(p => p.def.defName)
                    .ToList();

                List<HumanData> convertedList = new List<HumanData>();
                foreach (Pawn human in MapHumans)
                {
                    HumanData data = HumanScribeManager.HumanToString(human);
                    convertedList.Add(data);
                }

                return convertedList;
            }

            else
            {
                List<Pawn> CaravanHumans = ClientValues.chosenCaravan.PawnsListForReading
                    .FindAll(fetch => DeepScribeHelper.CheckIfThingIsHuman(fetch))
                    .OrderBy(p => p.def.defName)
                    .ToList();

                List<HumanData> convertedList = new List<HumanData>();
                foreach (Pawn human in CaravanHumans)
                {
                    HumanData data = HumanScribeManager.HumanToString(human);
                    convertedList.Add(data);
                }

                return convertedList;
            }
        }

        public static List<AnimalData> GetActivityAnimals()
        {
            if (ClientValues.isRealTimeHost)
            {
                List<Pawn> MapAnimals = OnlineActivityManager.onlineMap.mapPawns.AllPawns
                    .FindAll(fetch => DeepScribeHelper.CheckIfThingIsAnimal(fetch))
                    .OrderBy(p => p.def.defName)
                    .ToList();

                List<AnimalData> convertedList = new List<AnimalData>();
                foreach (Pawn animal in MapAnimals)
                {
                    AnimalData data = AnimalScribeManager.AnimalToString(animal);
                    convertedList.Add(data);
                }

                return convertedList;
            }

            else
            {
                List<Pawn> CaravanAnimals = ClientValues.chosenCaravan.PawnsListForReading
                    .FindAll(fetch => DeepScribeHelper.CheckIfThingIsAnimal(fetch))
                    .OrderBy(p => p.def.defName)
                    .ToList();

                List<AnimalData> convertedList = new List<AnimalData>();
                foreach (Pawn animal in CaravanAnimals)
                {
                    AnimalData data = AnimalScribeManager.AnimalToString(animal);
                    convertedList.Add(data);
                }

                return convertedList;
            }
        }

        public static bool CheckIfIgnoreThingSync(Thing toCheck)
        {
            if (toCheck is Projectile) return true;
            else if (toCheck is Mote) return true;
            else return false;
        }

        public static void EnterMap(OnlineActivityData activityData)
        {
            if (activityData.ActivityType == OnlineActivityType.Visit)
            {
                CaravanEnterMapUtility.Enter(ClientValues.chosenCaravan, OnlineActivityManager.onlineMap, CaravanEnterMode.Edge,
                    CaravanDropInventoryMode.DoNotDrop, draftColonists: false);
            }

            else if (activityData.ActivityType == OnlineActivityType.Raid)
            {
                SettlementUtility.Attack(ClientValues.chosenCaravan, ClientValues.chosenSettlement);
            }

            CameraJumper.TryJump(OnlineActivityManager.factionPawns[0].Position, OnlineActivityManager.onlineMap);
        }
    }
}
