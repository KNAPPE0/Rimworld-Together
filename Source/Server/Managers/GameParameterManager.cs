using GameServer.Core;
using GameServer.Misc;
using Shared;
using static Shared.CommonEnumerators;
using Shared.Misc;
using TCPNetwork.Packets;
using TCPNetwork.Files.Client;
using Shared.Files.Configs;

namespace GameServer.Managers
{

    public static class GameParameterManager
    {
        [HandlesPacket(PacketHeader.GameParameterManager)]
        private static void ParsePacket(ServerClient client, byte[] bytes, PacketHeader header)
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
                Master.ScenarioValues.Save();
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
                Master.StorytellerValues.Save();
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
                Master.DifficultyValues.Save();

                InformationDisplayer.DisplaySetDifficulty(client.UserFile.Username);
            }
        }
    }
}
