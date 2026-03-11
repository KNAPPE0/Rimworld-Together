using GameServer.Core;
using GameServer.Misc;
using Shared;
using static Shared.CommonEnumerators;
using TCPNetwork.Packets;
using TCPNetwork.Files.Client;
using Shared.Files.Configs;

namespace GameServer.PacketManager
{

    public static class PM_World
    {
        [HandlesPacket(PacketHeader.WorldManager)]
        private static void ParsePacket(ServerClient client, byte[] bytes, PacketHeader header)
        {
            WorldData data = Serializer.ConvertBytesToObject<WorldData>(bytes);

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
            WorldData worldData = new WorldData();
            worldData._stepMode = WorldStepMode.AskFor;

            client.Listener.EnqueuePacket(PacketHeader.WorldManager, worldData);
        }

        public static void SendWorld(ServerClient client)
        {
            WorldData data = new WorldData();
            PlanetConfigFile file = Serializer.FileBytesToObject<PlanetConfigFile>(PlanetConfigFile.SavePath);

            data._fileBytes = Serializer.ConvertObjectToBytes(file);
            data._stepMode = WorldStepMode.Sent;

            client.Listener.EnqueuePacket(PacketHeader.WorldManager, data);
        }

        public static void ReceiveWorld(ServerClient client, WorldData data)
        {
            PlanetConfigFile file = Serializer.ConvertBytesToObject<PlanetConfigFile>(data._fileBytes);
            Serializer.ObjectBytesToFile(PlanetConfigFile.SavePath, file);
            Master.WorldValues = file;

            InformationDisplayer.DisplaySetWorld(client);
        }
    }
}
