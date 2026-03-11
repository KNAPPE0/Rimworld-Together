using GameServer.Misc;
using Shared;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static Shared.CommonEnumerators;

namespace GameServer.PacketManager
{
    public class PM_Version : PM_Base
    {
        [HandlesPacket(PacketHeader.VersionManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            VersionData data = Serializer.ConvertBytesToObject<VersionData>(bytes);

            if (data._version == CommonValues.ExecutableVersion)
            {
                data._step = VersionData.VersionStep.Pass;
                client.Listener.EnqueuePacket(PacketHeader.VersionManager, data);
            }

            else
            {
                LoginManagerH.DenyConnectionWithReason(client, LoginResponse.Version);
                InformationDisplayer.DisplayVersionMismatch(client);
            }
        }

        public static void AskForClientVersion(ServerClient client)
        {
            VersionData data = new VersionData();
            data._step = VersionData.VersionStep.Ask;

            client.Listener.EnqueuePacket(PacketHeader.VersionManager, data);
        }
    }
}
