using GameServer.Managers;
using Shared;
using Shared.Misc;
using TCPNetwork.Files.Client;

namespace GameServer.Commands
{
    public class CMD_DeepList : CMD_Base
    {
        public CMD_DeepList()
        {
            Prefix = "deeplist";
            Description = "Shows a list of all server players";
        }

        public override void Action() 
        {
            UserFile[] userFiles = UserManagerH.GetAllUserFiles();

            Printer.Title($"Server players: [{userFiles.Length}]");
            Printer.Title("----------------------------------------");
            foreach (UserFile user in userFiles) Printer.Warning($"{user.Username}");
            Printer.Title("----------------------------------------");
        }
    }
}
