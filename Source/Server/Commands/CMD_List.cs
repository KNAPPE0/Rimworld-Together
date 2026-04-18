using GameServer.Hooks.TCPNetwork;
using Shared;
using Shared.Misc;
using TCPNetwork.Files.Client;

namespace GameServer.Commands
{
    public class CMD_List : CMD_Base
    {
        public CMD_List()
        {
            Prefix = "list";
            Description = "Shows all connected players with username and IP";
        }

        public override void Action() 
        {
            ServerClient[] clients = ServerNetwork.GetConnectedClients();
            Printer.Title($"Connected players: [{clients.Length}]");
            Printer.Title("----------------------------------------");
            foreach (ServerClient client in clients) 
            {
                string name = client.UserFile?.Username ?? "(no login)";
                string admin = (client.UserFile?.IsAdmin ?? false) ? " [ADMIN]" : "";
                Printer.Warning($"{name}{admin} - {client.CurrentIP}");
            }
            Printer.Title("----------------------------------------");
        }
    }
}
