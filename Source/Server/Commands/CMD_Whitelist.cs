using GameServer.Core;
using Shared;
using Shared.Misc;

namespace GameServer.Commands
{
    public class CMD_Whitelist : CMD_Base
    {
        public CMD_Whitelist()
        {
            Prefix = "whitelist";
            Description = "Shows all whitelisted players";
            ParameterCount = 0;
        }

        public override void Action()
        {
            Printer.Title($"Whitelisted usernames: [{Master.Whitelist.WhitelistedUsers.Count}]");
            Printer.Title("----------------------------------------");
            foreach (string str in Master.Whitelist.WhitelistedUsers) Printer.Warning($"{str}");
            Printer.Title("----------------------------------------");
        }
    }
}
