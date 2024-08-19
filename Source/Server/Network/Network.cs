using Shared;
using System.Net;
using System.Net.Sockets;
using static Shared.CommonEnumerators;

namespace GameServer
{
    //Main class that is used to handle the connection with the clients
    public static class Network
    {
        //IP and Port that the connection will be bound to
        private static IPAddress localAddress = IPAddress.Parse(Master.serverConfig.IP);
        public static int port = int.Parse(Master.serverConfig.Port);

        //TCP listener that will handle the connection with the clients, and list of currently connected clients
        private static TcpListener connection;
        public static List<ServerClient> connectedClients = new List<ServerClient>();

        //Entry point function of the network class
        public static void ReadyServer()
        {
            if (Master.serverConfig.UseUPnP) { _ = new UPnP(); }

            connection = new TcpListener(localAddress, port);
            connection.Start();

            Threader.GenerateServerThread(Threader.ServerMode.RunSiteManager);
            Threader.GenerateServerThread(Threader.ServerMode.RunCaravanManager);

            Logger.Warning("Type 'help' to get a list of available commands");
            Logger.Warning($"Listening for users at {localAddress}:{port}");
            Logger.Warning("Server launched");
            Master.ChangeTitle();

            while (true) ListenForIncomingUsers();
        }

        //Listens for any user that might connect and executes all required tasks with it
        private static void ListenForIncomingUsers()
        {
            TcpClient newTCP = connection.AcceptTcpClient();
            ServerClient newServerClient = new ServerClient(newTCP);
            Listener newListener = new Listener(newServerClient, newTCP);
            newServerClient.Listener = newListener;

            Threader.GenerateClientThread(newServerClient.Listener, Threader.ClientMode.ListenToClient);
            Threader.GenerateClientThread(newServerClient.Listener, Threader.ClientMode.SendDataToClient);
            Threader.GenerateClientThread(newServerClient.Listener, Threader.ClientMode.MonitorClientHealth);
            Threader.GenerateClientThread(newServerClient.Listener, Threader.ClientMode.MonitorKAFlag);

            if (Master.isClosing) newServerClient.Listener.disconnectFlag = true;
            else if (Master.worldValues == null && connectedClients.Count > 0) UserManager.SendLoginResponse(newServerClient, LoginResponse.NoWorld);
            else
            {
                if (connectedClients.Count >= int.Parse(Master.serverConfig.MaxPlayers))
                {
                    UserManager.SendLoginResponse(newServerClient, LoginResponse.ServerFull);
                    Logger.Warning($"Server Full");
                }
                else
                {
                    connectedClients.Add(newServerClient);
                    Master.ChangeTitle();
                    Logger.Message($"[Connect] > {newServerClient.UserFile.Username} | {newServerClient.UserFile.SavedIP}");
                }
            }
        }

        //Kicks specified client from the server
        public static void KickClient(ServerClient client)
        {
            try
            {
                connectedClients.Remove(client);
                client.Listener.DestroyConnection();

                Master.ChangeTitle();
                UserManager.SendPlayerRecount();
                Logger.Message($"[Disconnect] > {client.UserFile.Username} | {client.UserFile.SavedIP}");
            }
            catch { Logger.Warning($"Error disconnecting user {client.UserFile.Username}, this will cause memory overhead"); }
        }
    }

    public static class NetworkHelper
    {
        public static ServerClient[] GetConnectedClientsSafe()
        {
            return Network.connectedClients.ToArray();
        }

        public static void SendPacketToAllClients(Packet packet, ServerClient toExclude = null)
        {
            foreach (ServerClient client in GetConnectedClientsSafe())
            {
                if (toExclude != null && client == toExclude) continue;
                else client.Listener.EnqueuePacket(packet);
            }
        }
    }
}