using GameClient.Dialogs;
using GameClient.Misc;
using TCPNetwork.Packets;
using RimWorld;
using Shared;
using static Shared.CommonEnumerators;
using GameClient.Managers;

namespace GameClient.PacketManagers
{
    //Class that handles how the client will answer to incoming server commands
    public static class PM_Console
    {
        //Parses the received packet into a command to execute

        [HandlesPacket(PacketHeader.ConsoleManager)]
        private static void ParsePacket(byte[] bytes)
        {
            CommandData data = Serializer.ConvertBytesToObject<CommandData>(bytes);

            switch (data._commandMode)
            {
                case CommandMode.Op:
                    OnOpCommand();
                    break;

                case CommandMode.Deop:
                    OnDeopCommand();
                    break;

                case CommandMode.Broadcast:
                    OnBroadcastCommand(data);
                    break;

                case CommandMode.ForceSave:
                    OnForceSaveCommand();
                    break;
            }
        }

        //Executes the command depending on the type

        private static void OnOpCommand()
        {
            SessionHandler.IsAdmin = true;
            DLG_Base.PushNewDialog(new DLG_Message("MESSAGE", new string[] { "You are now an admin!" }));
        }

        private static void OnDeopCommand()
        {
            SessionHandler.IsAdmin = false;
            DLG_Base.PushNewDialog(new DLG_Message("MESSAGE", new string[] { "You are no longer an admin!" }));
        }

        private static void OnBroadcastCommand(CommandData commandData)
        {
            RimworldManager.GenerateLetter("Server Broadcast", ChatManagerH.ParseMessage(commandData._details, true), LetterDefOf.PositiveEvent);
        }

        private static void OnForceSaveCommand() { PM_Saves.ForceSave(); }
    }
}
