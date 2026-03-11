using GameClient.Files;
using GameClient.Managers;
using GameClient.Misc;
using Shared;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;

namespace GameClient.PacketManagers
{
    public class PM_GlobalData : PM_Base
    {
        [HandlesPacket(PacketHeader.GlobalDataManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            ServerGlobalData serverGlobalData = Serializer.ConvertBytesToObject<ServerGlobalData>(bytes);

            SessionHandler.SetValues(serverGlobalData);
            EventManagerH.SetValues(serverGlobalData);
            GameParameterManager.SetValues(serverGlobalData);
            PlayerSettlementManagerHelper.SetValues(serverGlobalData);
            NPCManagerH.SetValues(serverGlobalData);
            SiteManagerH.SetValues(serverGlobalData);
            RoadManagerHelper.SetValues(serverGlobalData);
            PollutionManagerHelper.SetValues(serverGlobalData);
            PM_Mods.ReceiveModConfigs(serverGlobalData);
        }
    }
}