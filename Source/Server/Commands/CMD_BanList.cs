using GameServer.Managers;
using Shared;
using Shared.Misc;
using TCPNetwork.Files.Client;

namespace GameServer.Commands
{
    public class CMD_BanList : CMD_Base
    {
        public CMD_BanList()
        {
            Prefix = "banlist";
            Description = "Shows a list of all banned server players";
            ParameterCount = 0;
        }

        public override void Action() 
        {
            UserFile[] userFiles = UserManagerH.GetAllUserFiles().Where(x => x.IsBanned).ToArray();

            Printer.Title($"Banned players: [{userFiles.Length}]");
            Printer.Title("----------------------------------------");
            foreach (UserFile user in userFiles) Printer.Warning($"{user.Username} - {user.LatestIP}");
            Printer.Title("----------------------------------------");
        }
    }
}
