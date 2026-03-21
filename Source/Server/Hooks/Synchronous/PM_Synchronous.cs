using GameServer.Hooks.TCPNetwork;
using GameServer.Managers;
using GameServer.PacketManager;
using Shared;
using Shared.Files;
using Shared.Misc;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;

namespace GameServer.Hooks.Synchronous
{
    public class PM_Synchronous : PM_Base
    {
        [HandlesPacket(PacketHeader.SynchronousManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_Synchronous data = Serializer.ConvertBytesToObject<PKT_Synchronous>(bytes);

            switch (data._stepMode)
            {
                case PKT_Synchronous.StepMode.Ask:
                    TryStartSynchronousSession(client, data);
                    break;

                case PKT_Synchronous.StepMode.Accept:
                    AcceptSynchronousSession(client, data);
                    break;

                case PKT_Synchronous.StepMode.Reject:
                    RejectSynchronousSession(client, data);
                    break;

                case PKT_Synchronous.StepMode.Start:
                    StartSynchronousSession(client, data);
                    break;
            }
        }

        private static void TryStartSynchronousSession(ServerClient client, PKT_Synchronous data)
        {
            SettlementFile settlement = PM_Settlements.GetSettlementFileFromTile(data._toTile);
            if (settlement == null)
            {
                ResponseShortcutManager.SendUnavailablePacket(client);
                return;
            }

            ServerClient toFind = ServerNetwork.GetConnectedClientFromUsername(settlement.Username);

            if (toFind == null)
            {
                ResponseShortcutManager.SendUnavailablePacket(client);
            }
            else
            {
                PKT_Synchronous request = new PKT_Synchronous();
                request._stepMode = PKT_Synchronous.StepMode.Ask;
                request._fromTile = PM_Settlements.GetSettlementFileFromUsername(client.UserFile.Username).Tile;
                request._username = client.UserFile.Username;
                request._toTile = data._toTile;
                request._party = data._party;
                request._type = data._type;

                toFind.Listener.EnqueuePacket(PacketHeader.SynchronousManager, request);
            }
        }

        private static void AcceptSynchronousSession(ServerClient client, PKT_Synchronous data)
        {
            SettlementFile settlement = PM_Settlements.GetSettlementFileFromTile(data._toTile);
            if (settlement == null) return;

            ServerClient toFind = ServerNetwork.GetConnectedClientFromUsername(settlement.Username);
            if (toFind == null) return;

            PKT_Synchronous accept = new PKT_Synchronous();
            accept._stepMode = PKT_Synchronous.StepMode.Accept;
            accept._fromTile = data._fromTile;
            accept._toTile = data._toTile;
            accept._contents = PM_Maps.GetMapBytesFromTile(data._fromTile);
            accept._party = data._party;
            accept._type = data._type;

            client.SynchronousClient = toFind;
            toFind.SynchronousClient = client;

            toFind.Listener.EnqueuePacket(PacketHeader.SynchronousManager, accept);
        }

        private static void RejectSynchronousSession(ServerClient client, PKT_Synchronous data)
        {
            SettlementFile settlement = PM_Settlements.GetSettlementFileFromTile(data._toTile);
            if (settlement == null) return;

            ServerClient toFind = ServerNetwork.GetConnectedClientFromUsername(settlement.Username);
            if (toFind == null) return;

            PKT_Synchronous reject = new PKT_Synchronous();
            reject._stepMode = PKT_Synchronous.StepMode.Reject;
            reject._fromTile = data._fromTile;
            reject._toTile = data._toTile;

            toFind.Listener.EnqueuePacket(PacketHeader.SynchronousManager, reject);
        }

        private static void StartSynchronousSession(ServerClient client, PKT_Synchronous data)
        {
            if (client?.SynchronousClient?.Listener == null) return;

            PKT_Synchronous start = new PKT_Synchronous();
            start._stepMode = PKT_Synchronous.StepMode.Start;

            client.SynchronousClient.Listener.EnqueuePacket(PacketHeader.SynchronousManager, start);
        }

        [HandlesPacket(PacketHeader.SPlayerDraft)]
        private static void SPlayerDraft(ServerClient client, byte[] bytes, PacketHeader header)
        {
            client.Listener.EnqueuePacket(header, bytes);
            client.SynchronousClient.Listener.EnqueuePacket(header, bytes);
        }

        [HandlesPacket(PacketHeader.SPlayerWeather)]
        private static void SPlayerWeather(ServerClient client, byte[] bytes, PacketHeader header)
        {
            client.Listener.EnqueuePacket(header, bytes);
            client.SynchronousClient.Listener.EnqueuePacket(header, bytes);
        }

        [HandlesPacket(PacketHeader.SPlayerMentalState)]
        private static void SPlayerMentalState(ServerClient client, byte[] bytes, PacketHeader header)
        {
            client.Listener.EnqueuePacket(header, bytes);
            client.SynchronousClient.Listener.EnqueuePacket(header, bytes);
        }

        [HandlesPacket(PacketHeader.SPlayerGameSpeed)]
        private static void SPlayerGameSpeed(ServerClient client, byte[] bytes, PacketHeader header)
        {
            client.Listener.EnqueuePacket(header, bytes);
            client.SynchronousClient.Listener.EnqueuePacket(header, bytes);
        }

        [HandlesPacket(PacketHeader.SPlayerJob)]
        private static void SPlayerJob(ServerClient client, byte[] bytes, PacketHeader header)
        {
            client.Listener.EnqueuePacket(header, bytes);
            client.SynchronousClient.Listener.EnqueuePacket(header, bytes);
        }

        [HandlesPacket(PacketHeader.SPlayerHediff)]
        private static void SPlayerHediff(ServerClient client, byte[] bytes, PacketHeader header)
        {
            client.Listener.EnqueuePacket(header, bytes);
            client.SynchronousClient.Listener.EnqueuePacket(header, bytes);
        }

        [HandlesPacket(PacketHeader.SPlayerDestroy)]
        private static void SPlayerDestroy(ServerClient client, byte[] bytes, PacketHeader header)
        {
            client.Listener.EnqueuePacket(header, bytes);
            client.SynchronousClient.Listener.EnqueuePacket(header, bytes);
        }

        [HandlesPacket(PacketHeader.SPlayerPosition)]
        private static void SPlayerPosition(ServerClient client, byte[] bytes, PacketHeader header)
        {
            client.SynchronousClient.Listener.EnqueuePacket(header, bytes);
        }
    }
}