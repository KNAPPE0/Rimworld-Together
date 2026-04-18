using GameServer.Hooks.TCPNetwork;
using Shared;
using Shared.Misc;
using TCPNetwork.Files.Client;
using System.Linq;

namespace GameServer.Commands
{
    public class CMD_PlayerCount : CMD_Base
    {
        public CMD_PlayerCount()
        {
            Prefix = "players";
            Description = "Shows online player count and names";
        }

        public override void Action()
        {
            ServerClient[] clients = ServerNetwork.GetConnectedClients();
            string[] names = clients
                .Select(c => c.UserFile?.Username ?? "(unknown)")
                .Where(n => !string.IsNullOrEmpty(n))
                .ToArray();

            Printer.Title($"Online: {names.Length} player(s)");
            if (names.Length > 0)
                Printer.Warning(string.Join(", ", names));
        }
    }
}
