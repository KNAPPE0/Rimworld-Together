using GameServer.Core;
using Shared;
using Shared.Misc;
using System.Collections.Generic;
using System.IO;

namespace GameServer.Commands
{
    public class CMD_PardonIP : CMD_Base
    {
        public CMD_PardonIP()
        {
            Prefix = "pardonip";
            Description = "Unbans an IP address";
            ParameterCount = 1;
        }

        public override void Action()
        {
            string targetIP = CMD_Base.CommandParameters[0];

            List<string> bannedIPs = CMD_BanIP.LoadBannedIPs();

            if (!bannedIPs.Contains(targetIP))
            {
                Printer.Warning($"IP '{targetIP}' is not banned");
                return;
            }

            bannedIPs.Remove(targetIP);
            CMD_BanIP.SaveBannedIPs(bannedIPs);
            Printer.Title($"Unbanned IP {targetIP}");
        }
    }
}
