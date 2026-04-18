using GameServer.Hooks.TCPNetwork;
using Shared;
using Shared.Misc;
using TCPNetwork.Files.Client;
using System.Linq;

namespace GameServer.Commands
{
    public class CMD_KickIP : CMD_Base
    {
        public CMD_KickIP()
        {
            Prefix = "kickip";
            Description = "Kicks all players connected from the specified IP";
            ParameterCount = 1;
        }

        public override void Action()
        {
            string targetIP = CMD_Base.CommandParameters[0];
            ServerClient[] matches = ServerNetwork.GetConnectedClients()
                .Where(c => c.CurrentIP == targetIP).ToArray();

            if (matches.Length == 0)
            {
                Printer.Warning($"No connected players found with IP '{targetIP}'");
                return;
            }

            foreach (ServerClient client in matches)
            {
                string name = client.UserFile?.Username ?? "(unknown)";
                client.Listener.MarkForDisconnect();
                Printer.Warning($"Kicked '{name}' ({client.CurrentIP})");
            }

            Printer.Title($"Kicked {matches.Length} connection(s) from IP {targetIP}");
        }
    }
}
