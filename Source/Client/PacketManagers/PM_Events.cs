using System;
using System.Collections.Generic;
using System.Linq;
using GameClient.Dialogs;
using GameClient.Misc;
using RimWorld;
using Shared;
using Verse;
using static Shared.CommonEnumerators;
using Shared.Files;
using TCPNetwork.Packets;
using GameClient.Hooks.TCPNetwork;
using TCPNetwork;
using GameClient.Managers;
using TCPNetwork.Files.Client;

namespace GameClient.PacketManagers
{
    public class PM_Events : PM_Base
    {
        [HandlesPacket(PacketHeader.EventManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_Event data = Serializer.ConvertBytesToObject<PKT_Event>(bytes);

            switch (data._stepMode)
            {
                case EventStepMode.Send:
                    OnEventSent();
                    break;

                case EventStepMode.Receive:
                    OnEventReceived(data);
                    break;

                case EventStepMode.Recover:
                    OnRecoverEventSilver();
                    break;
            }
        }

        public static void SendExistingEventsToServer()
        {          
            List<EventFile> existingEvents = new List<EventFile>();
            foreach (IncidentDef incident in DefDatabase<IncidentDef>.AllDefs)
            {
                EventFile file = new EventFile();
                file.Name = incident.LabelCap;
                file.DefName = incident.defName;
                file.Cost = 500;
                file.IsEnabled = true;

                existingEvents.Add(file);
            }

            PKT_Event eventData = new PKT_Event();
            eventData._stepMode = EventStepMode.Set;
            eventData._eventFiles = existingEvents.ToArray();

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.EventManager, eventData);
        }

        public static void ShowEventMenu()
        {
            List<string> eventNames = new List<string>();
            foreach (EventFile eventFile in EventManagerH.EnabledEvents) eventNames.Add(eventFile.Name);

            Action a1 = delegate
            {
                DLG_YesNo d2 = new DLG_YesNo($"This event will cost you {EventManagerH.EnabledEvents[DLG_ScrollButtons.SelectedScrollButton].Cost} " +
                    $"silver, continue?", SendEvent, null);

                DLG_Base.PushNewDialog(d2);
            };

            DLG_ScrollButtons d1 = new DLG_ScrollButtons("Event Selector", "Choose the event you want to send",
                eventNames.ToArray(), a1.Invoke, null);

            DLG_Base.PushNewDialog(d1);
        }

        public static void OpenEventManagerMenu()
        {
            string title = "Event Manager";
            string description = "Configure the availability of each event";
            string[] values = { "Disabled", "Enabled" };

            List<string> eventNames = new List<string>();
            foreach (EventFile ev in EventManagerH.AvailableEvents) eventNames.Add(ev.Name);

            List<int> defaultValues = new List<int>();
            foreach (EventFile ev in EventManagerH.AvailableEvents) defaultValues.Add(ev.IsEnabled == true ? 1 : 0);

            Action toDo = delegate
            {
                for (int i = 0; i < EventManagerH.AvailableEvents.Length; i++)
                {
                    EventFile file = EventManagerH.AvailableEvents[i];
                    file.IsEnabled = DLG_ListingWithTuple.DialogTupleListingResultInt[i] == 1 ? true : false;
                }

                PKT_Event data = new PKT_Event();
                data._stepMode = CommonEnumerators.EventStepMode.Customize;
                data._eventFiles = EventManagerH.AvailableEvents;
                Network.ServerEndpoint.EnqueuePacket(PacketHeader.EventManager, data);

                DLG_Base.PushNewDialog(new DLG_Message("SUCCESS",
                    new string[] { "Changes will apply to new connecting players" }));
            };

            DLG_Base.PushNewDialog(new DLG_ListingWithTuple(title, description, eventNames.ToArray(), values, defaultValues.ToArray(), toDo));
        }

        public static void SendEvent()
        {
            DLG_ScrollButtons.Instance.Close();

            //TODO
            //MAKE IT SO ALL MAPS ARE ACCOUNTED FOR
            Map toGetSilverFrom = Find.AnyPlayerHomeMap;

            if (!RimworldManager.CheckIfHasEnoughSilverInMap(toGetSilverFrom, EventManagerH.EnabledEvents[DLG_ScrollButtons.SelectedScrollButton].Cost))
            {
                DLG_Base.PushNewDialog(new DLG_Message("ERROR", new string[] { "You do not have enough silver for this action!" }));
            }

            else
            {
                RimworldManager.RemoveThingFromSettlement(toGetSilverFrom, ThingDefOf.Silver, EventManagerH.EnabledEvents[DLG_ScrollButtons.SelectedScrollButton].Cost);

                PKT_Event eventData = new PKT_Event();
                eventData._stepMode = EventStepMode.Send;
                eventData._fromTile = toGetSilverFrom.Tile;
                eventData._toTile = SessionHandler.ChosenSettlement.Tile;
                eventData._eventFile = EventManagerH.EnabledEvents[DLG_ScrollButtons.SelectedScrollButton];

                Network.ServerEndpoint.EnqueuePacket(PacketHeader.EventManager, eventData);

                DLG_Base.PushNewDialog(new DLG_Wait("Waiting for event"));
            }
        }

        public static void TriggerEvent(IncidentDef eventToTrigger, Map targetMap)
        {
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(eventToTrigger.category, targetMap);
            parms.customLetterLabel = $"Event - {eventToTrigger.LabelCap}";
            parms.faction = SessionHandler.NeutralFaction;
            parms.target = targetMap;

            eventToTrigger.Worker.TryExecute(parms);

            PM_Saves.ForceSave();
        }

        public static void OnEventReceived(PKT_Event eventData)
        {
            Map targetMap;
            if (eventData._toTile != -1) targetMap = Find.WorldObjects.Settlements.FirstOrDefault(fetch => fetch.Tile == eventData._toTile).Map;
            else targetMap = Find.AnyPlayerHomeMap;

            IncidentDef eventToTrigger = DefDatabase<IncidentDef>.AllDefs.FirstOrDefault(fetch => fetch.defName == eventData._eventFile.DefName);
            if (eventToTrigger != null) TriggerEvent(eventToTrigger, targetMap);
        }

        public static void OnEventSent()
        {
            DLG_Wait.Instance.Close();

            RimworldManager.GenerateLetter("Event sent!", "Your event has been sent!",
                LetterDefOf.PositiveEvent);

            PM_Saves.ForceSave();
        }

        private static void OnRecoverEventSilver()
        {
            DLG_Wait.Instance.Close();

            //TODO
            //MAKE IT SO ALL MAPS ARE ACCOUNTED FOR
            Map toReturnTo = Find.AnyPlayerHomeMap;

            Thing silverToReturn = ThingMaker.MakeThing(ThingDefOf.Silver);
            silverToReturn.stackCount = EventManagerH.EnabledEvents[DLG_ScrollButtons.SelectedScrollButton].Cost;

            RimworldManager.PlaceThingIntoMap(silverToReturn, toReturnTo, toReturnTo.Center, false);

            DLG_Base.PushNewDialog(new DLG_Message("ERROR", new string[] { "Player is not currently available!" }));
        }
    }

    public class EventManagerH
    {
        public static EventFile[] AvailableEvents { get; private set; } = null;

        public static EventFile[] EnabledEvents { get; private set; } = null;

        public static void SetValues(PKT_ServerGlobalData serverGlobalData) 
        { 
            AvailableEvents = serverGlobalData._eventValues;
            EnabledEvents = AvailableEvents.Where(fetch => fetch.IsEnabled).ToArray();
        }
    }
}
