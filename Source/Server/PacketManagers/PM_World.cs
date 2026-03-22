using GameServer.Core;
using GameServer.Misc;
using Shared;
using Shared.Files.Configs;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static Shared.CommonEnumerators;
using System;
using System.IO;
using Shared.Misc;

namespace GameServer.PacketManager
{
    public class PM_World : PM_Base
    {
        [HandlesPacket(PacketHeader.WorldManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            if (client == null || bytes == null || bytes.Length == 0)
                return;

            PKT_World data = null;

            try
            {
                data = Serializer.ConvertBytesToObject<PKT_World>(bytes);
            }
            catch (Exception e)
            {
                Printer.Warning($"[World] Failed to deserialize world packet: {e}");
                return;
            }

            if (data == null)
                return;

            switch (data._stepMode)
            {
                case WorldStepMode.Sent:
                    ReceiveWorld(client, data);
                    break;
            }
        }

        public static bool CheckIfWorldExists()
        {
            return File.Exists(PlanetConfigFile.SavePath);
        }

        public static void RequireWorldFile(ServerClient client)
        {
            if (client == null)
                return;

            PKT_World worldData = new PKT_World
            {
                _stepMode = WorldStepMode.AskFor
            };

            client.Listener.EnqueuePacket(PacketHeader.WorldManager, worldData);
        }

        public static void SendWorld(ServerClient client)
        {
            if (client == null)
                return;

            if (!File.Exists(PlanetConfigFile.SavePath))
            {
                Printer.Warning("[World] Tried to send world, but no world file exists.");
                return;
            }

            PlanetConfigFile file = null;

            try
            {
                file = Serializer.FileBytesToObject<PlanetConfigFile>(PlanetConfigFile.SavePath);
            }
            catch
            {
                try
                {
                    file = Serializer.SerializeFromFile<PlanetConfigFile>(PlanetConfigFile.SavePath);
                }
                catch (Exception e)
                {
                    Printer.Warning($"[World] Failed to load world file for sending: {e}");
                    return;
                }
            }

            if (file == null)
            {
                Printer.Warning("[World] World file loaded as null.");
                return;
            }

            PKT_World data = new PKT_World
            {
                _fileBytes = Serializer.ConvertObjectToBytes(file),
                _stepMode = WorldStepMode.Sent
            };

            client.Listener.EnqueuePacket(PacketHeader.WorldManager, data);
        }

        public static void ReceiveWorld(ServerClient client, PKT_World data)
        {
            if (data == null || data._fileBytes == null || data._fileBytes.Length == 0)
            {
                Printer.Warning("[World] Received invalid world payload.");
                return;
            }

            PlanetConfigFile file = null;

            try
            {
                file = Serializer.ConvertBytesToObject<PlanetConfigFile>(data._fileBytes);
            }
            catch (Exception e)
            {
                Printer.Warning($"[World] Failed to deserialize incoming world bytes: {e}");
                return;
            }

            if (file == null)
            {
                Printer.Warning("[World] Deserialized world file was null.");
                return;
            }

            try
            {
                Serializer.ObjectBytesToFile(PlanetConfigFile.SavePath, file);
            }
            catch
            {
                try
                {
                    Serializer.SerializeToFile(PlanetConfigFile.SavePath, file);
                }
                catch (Exception e)
                {
                    Printer.Warning($"[World] Failed to persist incoming world file: {e}");
                    return;
                }
            }

            Master.WorldValues = file;
            InformationDisplayer.DisplaySetWorld(client);
        }
    }
}