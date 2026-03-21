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
            if (client == null || bytes == null || bytes.Length == 0)
                return;

            GameParameterData data = null;

            try
            {
                data = Serializer.ConvertBytesToObject<GameParameterData>(bytes);
            }
            catch
            {
                Printer.Warning("[GameParameter] Failed to deserialize incoming packet.");
                return;
            }

            if (data == null)
                return;

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

        private static bool BlockIfUnauthorized(ServerClient client, string actionName)
        {
            if (client == null)
                return true;

            if (client.UserFile == null)
            {
                Printer.Warning($"[GameParameter] Blocked {actionName} change because client user file was null.");
                return true;
            }

            if (!client.UserFile.IsAdmin && Master.WorldValues != null)
            {
                ResponseShortcutManager.SendIllegalPacket(client, $"Only admins can change {actionName}.");
                Printer.Warning($"[GameParameter] User {client.UserFile.Username} attempted to set {actionName} without admin permissions.");
                return true;
            }

            return false;
        }

        private static void SetScenario(ServerClient client, byte[] bytes)
        {
            if (BlockIfUnauthorized(client, "scenario"))
                return;

            if (bytes == null || bytes.Length == 0)
            {
                Printer.Warning("[GameParameter] Scenario bytes were empty.");
                return;
            }

            ScenarioConfigFile file = null;

            try
            {
                file = Serializer.ConvertBytesToObject<ScenarioConfigFile>(bytes);
            }
            catch
            {
                Printer.Warning("[GameParameter] Failed to deserialize scenario config.");
                return;
            }

            if (file == null)
            {
                Printer.Warning("[GameParameter] Scenario config was null after deserialize.");
                return;
            }

            Master.ScenarioValues = file;
            ScenarioConfigFile.Save(ScenarioConfigFile.SavePath, file);
            InformationDisplayer.DisplaySetScenario(client.UserFile.Username);
        }

        private static void SetStoryteller(ServerClient client, byte[] bytes)
        {
            if (BlockIfUnauthorized(client, "storyteller"))
                return;

            if (bytes == null || bytes.Length == 0)
            {
                Printer.Warning("[GameParameter] Storyteller bytes were empty.");
                return;
            }

            StorytellerConfigFile file = null;

            try
            {
                file = Serializer.ConvertBytesToObject<StorytellerConfigFile>(bytes);
            }
            catch
            {
                Printer.Warning("[GameParameter] Failed to deserialize storyteller config.");
                return;
            }

            if (file == null)
            {
                Printer.Warning("[GameParameter] Storyteller config was null after deserialize.");
                return;
            }

            Master.StorytellerValues = file;
            StorytellerConfigFile.Save(StorytellerConfigFile.SavePath, file);
            InformationDisplayer.DisplaySetStoryteller(client.UserFile.Username);
        }

        private static void SetDifficulty(ServerClient client, byte[] bytes)
        {
            if (BlockIfUnauthorized(client, "difficulty"))
                return;

            if (bytes == null || bytes.Length == 0)
            {
                Printer.Warning("[GameParameter] Difficulty bytes were empty.");
                return;
            }

            DifficultyConfigFile file = null;

            try
            {
                file = Serializer.ConvertBytesToObject<DifficultyConfigFile>(bytes);
            }
            catch
            {
                Printer.Warning("[GameParameter] Failed to deserialize difficulty config.");
                return;
            }

            if (file == null)
            {
                Printer.Warning("[GameParameter] Difficulty config was null after deserialize.");
                return;
            }

            Master.DifficultyValues = file;
            DifficultyConfigFile.Save(DifficultyConfigFile.SavePath, file);
            InformationDisplayer.DisplaySetDifficulty(client.UserFile.Username);
        }
    }
}