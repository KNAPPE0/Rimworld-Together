using GameServer.Misc;
using Shared;
using static Shared.CommonEnumerators;
using TCPNetwork.Packets;
using TCPNetwork.Files.Client;
using Shared.Misc;

namespace GameServer.Managers
{

    public static class ResponseShortcutManager
    {
        public static void SendIllegalPacket(ServerClient client, string message, bool shouldBroadcast = true)
        {
            PKT_ResponseShortcut data = new PKT_ResponseShortcut();
            data._stepMode = ResponseStepMode.IllegalAction;

            client.Listener.EnqueuePacket(PacketHeader.ResponseShortcutManager, data);
            client.Listener.Disconnect();

            if (shouldBroadcast)
            {
                Printer.Warning($"[Illegal action] > {client.UserFile.Username} > {client.CurrentIP}");
                Printer.Warning($"[Illegal reason] > {message}");
            }
        }

        public static void SendUnavailablePacket(ServerClient client)
        {
            PKT_ResponseShortcut data = new PKT_ResponseShortcut();
            data._stepMode = ResponseStepMode.UserUnavailable;

            client.Listener.EnqueuePacket(PacketHeader.ResponseShortcutManager, data);
        }

        public static void SendBreakPacket(ServerClient client)
        {
            PKT_ResponseShortcut data = new PKT_ResponseShortcut();
            data._stepMode = ResponseStepMode.Pop;

            client.Listener.EnqueuePacket(PacketHeader.ResponseShortcutManager, data);
        }

        public static void SendNoPowerPacket(ServerClient client)
        {
            PKT_ResponseShortcut data = new PKT_ResponseShortcut();
            data._stepMode = ResponseStepMode.NoPower;

            client.Listener.EnqueuePacket(PacketHeader.ResponseShortcutManager, data);
        }
    }
}
