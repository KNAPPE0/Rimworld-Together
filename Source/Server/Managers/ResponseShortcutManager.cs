﻿using Shared;
using static Shared.CommonEnumerators;

namespace GameServer
{
    public static class ResponseShortcutManager
    {
        public static void SendIllegalPacket(ServerClient client, string message, bool shouldBroadcast = true)
        {
            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.IllegalActionPacket));
            client.Listener.EnqueuePacket(packet);
            client.Listener.disconnectFlag = true;

            if (shouldBroadcast) 
            { 
                Logger.Warning($"[Illegal action] > {client.userFile.Username} > {client.userFile.SavedIP}");
                Logger.Warning($"[Illegal reason] > {message}");
            }
        }

        public static void SendUnavailablePacket(ServerClient client)
        {
            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.UserUnavailablePacket));
            client.Listener.EnqueuePacket(packet);
        }

        public static void SendBreakPacket(ServerClient client)
        {
            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.BreakPacket));
            client.Listener.EnqueuePacket(packet);
        }

        public static void SendNoPowerPacket(ServerClient client, PlayerFactionData factionManifest)
        {
            factionManifest.manifestMode = FactionManifestMode.NoPower;

            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.FactionPacket), factionManifest);
            client.Listener.EnqueuePacket(packet);
        }

        public static void SendWorkerInsidePacket(ServerClient client)
        {
            SiteData siteData = new SiteData();
            siteData.SiteStepMode = SiteStepMode.WorkerError;

            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.SitePacket), siteData);
            client.Listener.EnqueuePacket(packet);
        }
    }
}
