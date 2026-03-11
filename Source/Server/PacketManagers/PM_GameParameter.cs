using GameServer.Core;
using GameServer.Managers;
using GameServer.Misc;
using Shared;
using Shared.Files.Configs;
using Shared.Misc;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static Shared.CommonEnumerators;

namespace GameServer.PacketManager
{
    public class PM_GameParameter : PM_Base
    {
        [HandlesPacket(PacketHeader.GameParameterManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            GameParameterData data = Serializer.ConvertBytesToObject<GameParameterData>(bytes);

            switch (data._stepMode)
            {
                case GenStepMode.Scenario:
                    SetScenario(client, data._bytes);
                    break;

                case GenStepMode.Storyteller:
                    SetStoryteller(client, data._bytes);
                    break;

                case GenStepMode.Difficulty:
                    SetDifficulty(client, data._bytes);
                    break;
            }
        }

        private static void SetScenario(ServerClient client, byte[] bytes)
        {
            if (!client.UserFile.IsAdmin && Master.WorldValues != null)
            {
                UserManager.BanPlayerFromName(client.UserFile.Username);
                Printer.Warning($"Player {client.UserFile.Username} attempted to set the scenario while not being an admin");
            }

            else
            {
                ScenarioConfigFile file = Serializer.ConvertBytesToObject<ScenarioConfigFile>(bytes);

                Master.ScenarioValues = file;
                ScenarioConfigFile.Save(ScenarioConfigFile.SavePath, file);
                InformationDisplayer.DisplaySetScenario(client.UserFile.Username);
            }
        }

        private static void SetStoryteller(ServerClient client, byte[] bytes)
        {
            if (!client.UserFile.IsAdmin && Master.WorldValues != null)
            {
                UserManager.BanPlayerFromName(client.UserFile.Username);
                Printer.Warning($"Player {client.UserFile.Username} attempted to set the storyteller while not being an admin");
            }

            else
            {
                StorytellerConfigFile file = Serializer.ConvertBytesToObject<StorytellerConfigFile>(bytes);

                Master.StorytellerValues = file;
                StorytellerConfigFile.Save(StorytellerConfigFile.SavePath, file);
                InformationDisplayer.DisplaySetStoryteller(client.UserFile.Username);
            }
        }

        private static void SetDifficulty(ServerClient client, byte[] bytes)
        {
            if (!client.UserFile.IsAdmin && Master.WorldValues != null)
            {
                UserManager.BanPlayerFromName(client.UserFile.Username);
                Printer.Warning($"Player {client.UserFile.Username} attempted to set the difficulty while not being an admin");
            }

            else
            {
                DifficultyConfigFile file = Serializer.ConvertBytesToObject<DifficultyConfigFile>(bytes);

                Master.DifficultyValues = file;
                DifficultyConfigFile.Save(DifficultyConfigFile.SavePath, file);
                InformationDisplayer.DisplaySetDifficulty(client.UserFile.Username);
            }
        }
    }
}
