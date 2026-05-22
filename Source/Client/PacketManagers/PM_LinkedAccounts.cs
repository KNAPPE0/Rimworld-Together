using GameClient.Managers;
using Shared;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;

namespace GameClient.PacketManagers
{
    /// <summary>
    /// KMH: Receives the server's linked-accounts snapshot and updates
    /// <see cref="LinkedAccountsCache"/> so dialogs render Discord handles.
    /// </summary>
    public class PM_LinkedAccounts : PM_Base
    {
        [HandlesPacket(PacketHeader.LinkedAccountsManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_LinkedAccounts data = Serializer.ConvertBytesToObject<PKT_LinkedAccounts>(bytes);
            LinkedAccountsCache.Apply(data);
        }
    }
}
