using GameServer.Core;
using GameServer.Integrations.Discord;
using GameServer.Managers;
using GameServer.Misc;
using TCPNetwork;
using Shared;
using System.Net;
using System.Net.Sockets;
using static Shared.CommonEnumerators;
using TCPNetwork.Files.Client;
using Shared.Misc;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace GameServer
{
    public class ServerNetwork : Network
    {
        public static ServerNetwork Instance { get; private set; } = null;

        private readonly object _clientsLock = new object();

        private static readonly HashSet<string> _bannedIps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public override Action<PacketHeader, byte[], ServerClient> OnReadPacket { get; set; } = delegate (PacketHeader header, byte[] buffer, ServerClient client)
        {
            MethodGatherer.ServerMethodDictionary[header].Invoke(null, new object[] { client, buffer, header });
        };

        public override Action<bool> OnWritePacket { get; set; } = delegate (bool mode) { };

        public override Action<ServerClient> OnConnect { get; set; } = delegate (ServerClient client) { };

        public override Action<ServerClient> OnDisconnect { get; set; } = delegate (ServerClient client)
        {
            try
            {
                if (Instance != null)
                {
                    lock (Instance._clientsLock)
                    {
                        Instance.ServerClients.RemoveAll(c => c == null);
                        Instance.ServerClients.Remove(client);
                    }
                }
            }
            catch { }

            try { Main_.ChangeTitle(); } catch { }
            try { UserManager.SendPlayerRecount(); } catch { }
            try { InformationDisplayer.DisplayDisconnect(client); } catch { }

            string username = null;
            try { username = client?.UserFile?.Username; } catch { username = null; }

            try
            {
                if (Master.ChatConfig.DisconnectNotifications && !string.IsNullOrWhiteSpace(username))
                {
                    ChatManager.BroadcastServerNotification($"{username} has left the server!");
                }
            }
            catch { }

            try
            {
                if (!string.IsNullOrWhiteSpace(username))
                {
                    DiscordPlayerAnnouncer.AnnounceLeft(username);
                }
            }
            catch
            {
                try
                {
                    string safeName = username;
                    if (string.IsNullOrWhiteSpace(safeName)) safeName = client?.CurrentIP;
                    if (string.IsNullOrWhiteSpace(safeName)) safeName = "(unknown)";
                    Printer.Warning($"Error disconnecting user {safeName}, this will cause memory overhead");
                }
                catch { }
            }
        };

        public override Action<object, LogImportanceMode> OnMessage { get; set; } = delegate (object obj, LogImportanceMode mode)
        {
            Printer.Message(obj, mode);
        };

        public override Action<object, LogImportanceMode> OnWarning { get; set; } = delegate (object obj, LogImportanceMode mode)
        {
            Printer.Warning(obj, mode);
        };

        public override Action<object, LogImportanceMode> OnError { get; set; } = delegate (object obj, LogImportanceMode mode)
        {
            Printer.Error(obj, mode);
        };

        public ServerNetwork()
        {
            Instance = this;
            Ip = Master.ServerConfig.IP;
            Port = Master.ServerConfig.Port;

            Task.Run(Setup);
        }

        public void Setup()
        {
            if (Master.ServerConfig.UseUPnP) { _ = new UPnP(); }

            try
            {
                ServerListener = new TcpListener(IPAddress.Parse(Ip), int.Parse(Port));
                ServerListener.Start();
            }
            catch (SocketException e)
            {
                Printer.Error($"Failed to start server on {Ip}:{Port}, try setting the address to your local ip address or '0.0.0.0' on port 25555, {e}");
            }
            catch (Exception e)
            {
                Printer.Error(e);
            }

            Printer.Warning("Server launched");
            Printer.Warning($"Listening for users at {Ip}:{Port}");
            Printer.Warning("Type 'help' to get a list of available commands");

            Main_.ChangeTitle();

            while (true) ListenForNewClients();
        }

        private void ListenForNewClients()
        {
            TcpClient newTCP = ServerListener.AcceptTcpClient();

            ServerClient client = new ServerClient(newTCP);
            client.Listener = new Listener(client, newTCP, OnReadPacket, OnWritePacket, OnConnect, OnDisconnect,
                OnMessage, OnWarning, OnError, Listener.ListenerMode.Server);

            try
            {
                string ip = client?.CurrentIP;
                if (!string.IsNullOrWhiteSpace(ip) && _bannedIps.Contains(ip))
                {
                    try { Printer.Warning($"[Blocked] > {ip} (IP banned)"); } catch { }
                    try { client.Listener?.DisconnectNow(); } catch { }
                    return;
                }
            }
            catch { }

            int connectedCount = 0;
            try { connectedCount = GetConnectedClientsSafe().Length; } catch { connectedCount = 0; }

            try
            {
                if (connectedCount >= int.Parse(Master.ServerConfig.MaxPlayers))
                {
                    LoginManagerH.DenyConnectionWithReason(client, LoginResponse.Full);
                    return;
                }
            }
            catch
            {
            }

            if (Master.WorldValues == null && connectedCount > 0)
            {
                LoginManagerH.DenyConnectionWithReason(client, LoginResponse.NoWorld);
                return;
            }

            lock (_clientsLock)
            {
                ServerClients.RemoveAll(c => c == null);
                ServerClients.Add(client);
            }

            Main_.ChangeTitle();

            try { InformationDisplayer.DisplayConnect(client); } catch { }

            VersionManager.AskForClientVersion(client);
        }

        public ServerClient[] GetConnectedClientsSafe(ServerClient toExclude = null)
        {
            ServerClient[] snapshot;
            lock (_clientsLock)
            {
                ServerClients.RemoveAll(c => c == null);
                snapshot = ServerClients.ToArray();
            }

            if (toExclude == null)
                return snapshot;

            return snapshot.Where(c => c != null && !ReferenceEquals(c, toExclude)).ToArray();
        }

        public ServerClient GetConnectedClientFromUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return null;

            var snapshot = GetConnectedClientsSafe();
            for (int i = 0; i < snapshot.Length; i++)
            {
                var c = snapshot[i];
                if (c == null) continue;

                var u = c.UserFile != null ? c.UserFile.Username : null;
                if (string.IsNullOrWhiteSpace(u)) continue;

                if (string.Equals(u, username, StringComparison.OrdinalIgnoreCase))
                    return c;
            }

            return null;
        }

        public ServerClient GetConnectedClientFromIP(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
                return null;

            ip = ip.Trim();

            var snapshot = GetConnectedClientsSafe();
            for (int i = 0; i < snapshot.Length; i++)
            {
                var c = snapshot[i];
                if (c == null) continue;

                if (string.Equals(c.CurrentIP, ip, StringComparison.OrdinalIgnoreCase))
                    return c;
            }

            return null;
        }

        public bool KickByIP(string ip)
        {
            var c = GetConnectedClientFromIP(ip);
            if (c == null) return false;

            try { c.Listener?.DisconnectNow(); } catch { }
            return true;
        }

        public bool BanByIP(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return false;
            ip = ip.Trim();

            try { _bannedIps.Add(ip); } catch { }

            try { KickByIP(ip); } catch { }

            return true;
        }

        public bool UnbanByIP(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return false;
            ip = ip.Trim();

            try { return _bannedIps.Remove(ip); }
            catch { return false; }
        }

        public bool IsIpBanned(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return false;
            ip = ip.Trim();

            try { return _bannedIps.Contains(ip); }
            catch { return false; }
        }

        public void SendPacketToAllClients(PacketHeader header, object obj, ServerClient toExclude = null)
        {
            foreach (ServerClient client in GetConnectedClientsSafe(toExclude))
            {
                try { client?.Listener?.EnqueuePacket(header, obj); }
                catch { }
            }
        }
    }
}