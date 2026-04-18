using GameServer.Core;
using GameServer.Misc;
using Shared;
using Shared.Files.Configs;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;
using static TCPNetwork.Packets.PKT_World;

namespace GameServer.PacketManager
{
    public class PM_World : PM_Base
    {
        [HandlesPacket(PacketHeader.WorldManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_World data = Serializer.ConvertBytesToObject<PKT_World>(bytes);
            if (data == null) return;

            switch (data._stepMode)
            {
                case WorldStepMode.Sent:
                    ReceiveWorld(client, data);
                    break;
            }
        }

        public static bool CheckIfWorldExists() { return File.Exists(PlanetConfigFile.SavePath); }

        public static void RequireWorldFile(ServerClient client)
        {
            PKT_World worldData = new PKT_World();
            worldData._stepMode = WorldStepMode.AskFor;
            client.Listener.EnqueuePacket(PacketHeader.WorldManager, worldData);
        }

        public static void SendWorld(ServerClient client)
        {
            PKT_World data = new PKT_World();
            data._stepMode = WorldStepMode.Sent;
            data.File = Serializer.SerializeFromFile<PlanetConfigFile>(PlanetConfigFile.SavePath);

            client.Listener.EnqueuePacket(PacketHeader.WorldManager, data);
        }

        public static void ReceiveWorld(ServerClient client, PKT_World data)
        {
            if (!client.UserFile.IsAdmin) client.Listener.MarkForDisconnect();
            else
            {
                Master.WorldValues = data.File;
                PlanetConfigFile.Save(PlanetConfigFile.SavePath, Master.WorldValues);
                InformationDisplayer.DisplaySetWorld(client);
            }
        }
    }
}
