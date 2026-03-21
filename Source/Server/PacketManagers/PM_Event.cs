using GameServer.Core;
using GameServer.Hooks.TCPNetwork;
using GameServer.Managers;
using GameServer.Misc;
using Shared;
using Shared.Files;
using Shared.Misc;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static Shared.CommonEnumerators;

namespace GameServer.PacketManager
{
    public class PM_Event : PM_Base
    {
        [HandlesPacket(PacketHeader.EventManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            if (client == null || bytes == null || bytes.Length == 0)
                return;

            if (!Master.ActionConfigs.EventAction.IsEnabled)
            {
                ResponseShortcutManager.SendIllegalPacket(client, "Tried to use disabled feature!");
                return;
            }

            PKT_Event data = null;

            try
            {
                data = Serializer.ConvertBytesToObject<PKT_Event>(bytes);
            }
            catch
            {
                Printer.Warning("[Event] Failed to deserialize event packet.");
                return;
            }

            if (data == null)
                return;

            switch (data._stepMode)
            {
                case EventStepMode.Send:
                    SendEvent(client, data);
                    break;

                case EventStepMode.Set:
                    SetEvents(client, data);
                    break;

                case EventStepMode.Customize:
                    ModifyEvents(client, data);
                    break;
            }
        }

        public static void SendEvent(ServerClient client, PKT_Event eventData)
        {
            if (client == null || client.UserFile == null || eventData == null)
                return;

            if (!PM_Settlements.CheckIfTileIsInUse(eventData._toTile))
            {
                ResponseShortcutManager.SendIllegalPacket(
                    client,
                    $"Player {client.UserFile.Username} attempted to send an event to settlement at tile {eventData._toTile}, but it has no settlement");
                return;
            }

            SettlementFile settlement = PM_Settlements.GetSettlementFileFromTile(eventData._toTile);
            if (settlement == null || string.IsNullOrWhiteSpace(settlement.Username))
            {
                ResponseShortcutManager.SendIllegalPacket(client, "Target settlement was invalid.");
                return;
            }

            if (!UserManagerH.CheckIfUserIsConnected(settlement.Username))
            {
                eventData._stepMode = EventStepMode.Recover;
                client.Listener.EnqueuePacket(PacketHeader.EventManager, eventData);
                return;
            }

            ServerClient target = ServerNetwork.GetConnectedClientFromUsername(settlement.Username);
            if (target == null || target.UserFile == null)
            {
                eventData._stepMode = EventStepMode.Recover;
                client.Listener.EnqueuePacket(PacketHeader.EventManager, eventData);
                return;
            }

            if (!PlayerCooldown.CheckIfCanEvent(
                    target.UserFile,
                    Master.ActionConfigs.EventAction.IsEnabled,
                    Master.ActionConfigs.EventAction.Cooldown))
            {
                eventData._stepMode = EventStepMode.Recover;
                client.Listener.EnqueuePacket(PacketHeader.EventManager, eventData);
                return;
            }

            client.Listener.EnqueuePacket(PacketHeader.EventManager, eventData);

            eventData._stepMode = EventStepMode.Receive;
            target.UserFile.Cooldowns.SetEventTimer(TimeConverter.GetCurrentTimeToEpoch(), target.UserFile);
            target.Listener.EnqueuePacket(PacketHeader.EventManager, eventData);
        }

        public static void SetEvents(ServerClient client, PKT_Event eventData)
        {
            if (client == null || client.UserFile == null || eventData == null)
                return;

            if (!client.UserFile.IsAdmin)
            {
                ResponseShortcutManager.SendIllegalPacket(client, "Only admins can set events.");
                Printer.Warning($"[Event] User {client.UserFile.Username} tried to set initial events without admin permissions.");
                return;
            }

            if (eventData._eventFiles == null || eventData._eventFiles.Length == 0)
            {
                ResponseShortcutManager.SendIllegalPacket(client, "No event files were provided.");
                return;
            }

            if (EventManagerH.LoadedEvents != null && EventManagerH.LoadedEvents.Length > 0)
            {
                ResponseShortcutManager.SendIllegalPacket(client, "Illegal setting of events!");
                return;
            }

            Directory.CreateDirectory(Master.EventsPath);

            foreach (EventFile file in eventData._eventFiles)
            {
                if (file == null || string.IsNullOrWhiteSpace(file.DefName))
                    continue;

                Serializer.SerializeToFile(Path.Combine(Master.EventsPath, file.DefName + EventManagerH.FileExtension), file);
            }

            EventManagerH.LoadAllEvents();
            InformationDisplayer.DisplaySetEvents(client);
        }

        private static void ModifyEvents(ServerClient client, PKT_Event data)
        {
            if (client == null || client.UserFile == null || data == null)
                return;

            if (!client.UserFile.IsAdmin)
            {
                ResponseShortcutManager.SendIllegalPacket(client, "Only admins can modify events.");
                Printer.Warning($"[Event] User {client.UserFile.Username} tried to modify events without admin permissions.");
                return;
            }

            if (data._eventFiles == null || data._eventFiles.Length == 0)
            {
                ResponseShortcutManager.SendIllegalPacket(client, "No event files were provided.");
                return;
            }

            Directory.CreateDirectory(Master.EventsPath);

            foreach (EventFile file in data._eventFiles)
            {
                if (file == null || string.IsNullOrWhiteSpace(file.DefName))
                    continue;

                Serializer.SerializeToFile(Path.Combine(Master.EventsPath, file.DefName + EventManagerH.FileExtension), file);
            }

            EventManagerH.LoadAllEvents();
            InformationDisplayer.DisplaySetEvents(client);
        }
    }

    public class EventManagerH
    {
        public static string FileExtension { get; private set; } = ".mpevent";

        public static EventFile[] LoadedEvents { get; private set; } = new EventFile[0];

        public static void LoadAllEvents()
        {
            List<EventFile> toLoad = new List<EventFile>();

            Directory.CreateDirectory(Master.EventsPath);

            foreach (string str in Directory.GetFiles(Master.EventsPath))
            {
                try
                {
                    EventFile file = Serializer.SerializeFromFile<EventFile>(str);
                    if (file != null)
                        toLoad.Add(file);
                }
                catch
                {
                    Printer.Warning($"[Event] Failed to load event file: {str}");
                }
            }

            LoadedEvents = toLoad
                .Where(fetch => fetch != null)
                .OrderBy(fetch => fetch.Name)
                .ToArray();
        }
    }
}