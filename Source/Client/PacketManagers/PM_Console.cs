using GameClient.Dialogs;
using GameClient.Managers;
using GameClient.Misc;
using RimWorld;
using Shared;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static Shared.CommonEnumerators;

namespace GameClient.PacketManagers
{
    //Class that handles how the client will answer to incoming server commands
    public class PM_Console : PM_Base
    {
        //Parses the received packet into a command to execute

        [HandlesPacket(PacketHeader.ConsoleManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
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
