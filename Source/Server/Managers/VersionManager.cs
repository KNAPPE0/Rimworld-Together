using GameServer.Misc;
using Shared;
using static Shared.CommonEnumerators;
using TCPNetwork.Packets;
using TCPNetwork.Files.Client;

namespace GameServer.Managers
{

    public static class VersionManager
    {
        [HandlesPacket(PacketHeader.VersionManager)]
        private static void ParsePacket(ServerClient client, byte[] bytes)
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
