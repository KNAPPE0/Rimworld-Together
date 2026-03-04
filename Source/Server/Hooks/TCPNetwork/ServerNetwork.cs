using GameServer.Core;
using GameServer.Integrations.Discord;
using GameServer.Managers;
using GameServer.Misc;
using Shared;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Misc;
using static Shared.CommonEnumerators;

namespace GameServer.Hooks.TCPNetwork
{
    public class ServerNetwork
    {
        private static readonly object ClientsLock = new object();
        private static readonly HashSet<string> BannedIps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private Action<PacketHeader, byte[], ServerClient> OnReadPacket { get; set; } = delegate (PacketHeader header, byte[] buffer, ServerClient client)
        {
            PacketCache.ServerMethodDictionary[header](client, buffer, header);
        };

        private Action<ServerClient> OnWritePacket { get; set; } = delegate (ServerClient client) { };

        private Action<ServerClient> OnConnect { get; set; } = delegate (ServerClient client) { };

        private Action<ServerClient> OnDisconnect { get; set; } = delegate (ServerClient client)
        {
            try
            {
                lock (ClientsLock)
                {
                    Network.ServerClients.RemoveAll(c => c == null);
                    Network.ServerClients.Remove(client);
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
                    ChatManager.BroadcastServerNotification($"{username} has left the server!");
            }
            catch { }

            try
            {
                if (!string.IsNullOrWhiteSpace(username))
                    DiscordPlayerAnnouncer.AnnounceLeft(username);
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

        public ServerNetwork()
        {
            Start();
        }

        private void Start()
        {
            Network.Ip = Master.ServerConfig.IP;
            Network.Port = Master.ServerConfig.Port;

            if (Master.ServerConfig.UseUPnP)
                _ = new UPnP();

            try
            {
                Network.ServerListener = new TcpListener(IPAddress.Parse(Network.Ip), Network.Port);
                Network.ServerListener.Start();
            }
            catch (SocketException e)
            {
                Printer.Error($"Failed to start server on {Network.Ip}:{Network.Port}, try setting the address to your local ip address or '0.0.0.0' on port 25555, {e}");
            }
            catch (Exception e)
            {
                Printer.Error(e);
            }

            Main_.ChangeTitle();
            Printer.Warning("Server launched");
            Printer.Warning($"Listening for users at {Network.Ip}:{Network.Port}");
            Printer.Warning("Type 'help' to get a list of available commands");

            Task.Run(delegate
            {
                while (true)
                    ListenForNewClients();
            });
        }

        private void ListenForNewClients()
        {
            TcpClient newTcp = Network.ServerListener.AcceptTcpClient();
            ServerClient client = new ServerClient(newTcp);
            NetworkRuleset ruleset = new NetworkRuleset(OnConnect, OnDisconnect, OnReadPacket, OnWritePacket);
            client.Listener = new Listener(client, newTcp, ruleset, Listener.ListenerMode.Server);

            try
            {
                string ip = client?.CurrentIP;
                if (!string.IsNullOrWhiteSpace(ip) && BannedIps.Contains(ip))
                {
                    try { Printer.Warning($"[Blocked] > {ip} (IP banned)"); } catch { }
                    try { client.Listener?.Disconnect(); } catch { }
                    return;
                }
            }
            catch { }

            int connectedCount = 0;
            try { connectedCount = GetConnectedClients().Length; } catch { connectedCount = 0; }

            try
            {
                if (connectedCount >= Master.ServerConfig.MaxPlayers)
                {
                    LoginManagerH.DenyConnectionWithReason(client, LoginResponse.Full);
                    return;
                }
            }
            catch { }

            if (Master.WorldValues == null && connectedCount > 0)
            {
                LoginManagerH.DenyConnectionWithReason(client, LoginResponse.NoWorld);
                return;
            }

            lock (ClientsLock)
            {
                Network.ServerClients.RemoveAll(c => c == null);
                Network.ServerClients.Add(client);
            }

            Main_.ChangeTitle();

            try { InformationDisplayer.DisplayConnect(client); } catch { }

            VersionManager.AskForClientVersion(client);
        }

        public static ServerClient[] GetConnectedClients(ServerClient toExclude = null)
        {
            ServerClient[] snapshot;

            lock (ClientsLock)
            {
                Network.ServerClients.RemoveAll(c => c == null);
                snapshot = Network.ServerClients.ToArray();
            }

            if (toExclude == null)
                return snapshot;

            return snapshot.Where(c => c != null && !ReferenceEquals(c, toExclude)).ToArray();
        }

        public static ServerClient GetConnectedClientFromUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return null;

            var snapshot = GetConnectedClients();
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

        public static ServerClient GetConnectedClientFromIP(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
                return null;

            ip = ip.Trim();

            var snapshot = GetConnectedClients();
            for (int i = 0; i < snapshot.Length; i++)
            {
                var c = snapshot[i];
                if (c == null) continue;

                if (string.Equals(c.CurrentIP, ip, StringComparison.OrdinalIgnoreCase))
                    return c;
            }

            return null;
        }

        public static bool KickByIP(string ip)
        {
            var c = GetConnectedClientFromIP(ip);
            if (c == null) return false;

            try { c.Listener?.Disconnect(); } catch { }
            return true;
        }

        public static bool BanByIP(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return false;
            ip = ip.Trim();

            try { BannedIps.Add(ip); } catch { }

            try { KickByIP(ip); } catch { }

            return true;
        }

        public static bool UnbanByIP(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return false;
            ip = ip.Trim();

            try { return BannedIps.Remove(ip); }
            catch { return false; }
        }

        public static bool IsIpBanned(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return false;
            ip = ip.Trim();

            try { return BannedIps.Contains(ip); }
            catch { return false; }
        }

        public static void SendPacketToAllClients(PacketHeader header, object obj, ServerClient toExclude = null)
        {
            foreach (ServerClient client in GetConnectedClients(toExclude))
            {
                try { client?.Listener?.EnqueuePacket(header, obj); }
                catch { }
            }
        }
    }
}