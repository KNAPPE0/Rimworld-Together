using GameServer.Hooks.TCPNetwork;
using GameServer.Managers;
using Shared;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;

namespace GameServer.PacketManager
{
    /// <summary>
    /// Receives a client's defName → label snapshot (sent on login)
    /// and merges it into <see cref="ItemLabelCache"/>. The cache feeds
    /// Discord output so listings, treasury dumps, and quest descriptions
    /// stop showing raw defNames.
    /// </summary>
    public class PM_ItemLabels : PM_Base
    {
        [HandlesPacket(PacketHeader.ItemLabelManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_ItemLabels data = Serializer.ConvertBytesToObject<PKT_ItemLabels>(bytes);
            if (data?.LabelsByDefName == null) return;
            ItemLabelCache.ApplySnapshot(data.LabelsByDefName);
        }
    }
}
