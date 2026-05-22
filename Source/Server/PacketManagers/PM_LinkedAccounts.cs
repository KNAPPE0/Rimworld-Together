using GameServer.Managers;
using Shared;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;

namespace GameServer.PacketManager
{
    /// <summary>
    /// KMH: Server-side handler for PKT_LinkedAccounts.
    ///
    /// Only direction we expect is server → client (snapshots), but this
    /// stub also lets a client politely re-request the snapshot if its
    /// cache is stale (e.g. after a brief disconnect).
    /// </summary>
    public class PM_LinkedAccounts : PM_Base
    {
        [HandlesPacket(PacketHeader.LinkedAccountsManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            // Treat any inbound packet on this header as "send me a snapshot."
            LinkedAccountsManager.SendSnapshot(client);
        }
    }
}
