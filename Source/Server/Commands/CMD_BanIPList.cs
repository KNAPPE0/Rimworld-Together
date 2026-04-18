using Shared;
using Shared.Misc;
using System.Collections.Generic;

namespace GameServer.Commands
{
    public class CMD_BanIPList : CMD_Base
    {
        public CMD_BanIPList()
        {
            Prefix = "baniplist";
            Description = "Shows all banned IP addresses";
        }

        public override void Action()
        {
            List<string> bannedIPs = CMD_BanIP.LoadBannedIPs();

            Printer.Title($"Banned IPs: [{bannedIPs.Count}]");
            Printer.Title("----------------------------------------");
            foreach (string ip in bannedIPs) Printer.Warning(ip);
            Printer.Title("----------------------------------------");
        }
    }
}
