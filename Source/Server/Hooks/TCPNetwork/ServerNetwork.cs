using GameServer.Core;
using GameServer.Managers;
using GameServer.Misc;
using GameServer.PacketManager;
using Shared;
using Shared.Misc;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using static TCPNetwork.Packets.PKT_Login;

namespace GameServer.Hooks.TCPNetwork
{
    public class ServerNetwork
    {
        // O(1) username→client index. Replaces linear scans that hit on every
        // marketplace buy / treasury access / chat broadcast on busy servers.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ServerClient> ConnectedByUsername =
            new System.Collections.Concurrent.ConcurrentDictionary<string, ServerClient>(StringComparer.OrdinalIgnoreCase);

        // Last-writer-wins matches "kick older session" semantics.
        public static void RegisterAuthenticatedClient(ServerClient client)
        {
            string u = client?.UserFile?.Username;
            if (string.IsNullOrEmpty(u)) return;
            ConnectedByUsername[u] = client;
        }

        private static Action<PacketHeader, byte[], ServerClient> OnReadPacket { get; set; } = delegate (PacketHeader header, byte[] buffer, ServerClient client)
        {
            MethodInfo method = (MethodInfo)PM_Base.PacketDictionary[header][1];
            method.Invoke(PM_Base.PacketDictionary[header][0], new object[] { client, buffer, header });
        };

        private static Action<ServerClient> OnDisconnect { get; set; } = delegate (ServerClient client)
        {
            try
            {
                Network.ServerClients.Remove(client, out _);

                // Only evict if we still own the slot — a fast reconnect may have replaced us.
                string u = client?.UserFile?.Username;
                if (!string.IsNullOrEmpty(u))
                {
                    if (ConnectedByUsername.TryGetValue(u, out ServerClient cur) && ReferenceEquals(cur, client))
                        ConnectedByUsername.TryRemove(u, out _);
                }

                InformationDisplayer.DisplayDisconnect(client);
                GameServer.Integrations.Discord.DiscordPlayerAnnouncer.AnnounceLeft(client.UserFile?.Username);
                if (Master.ChatConfig.DisconnectNotifications) PM_Chat.BroadcastServerNotification($"{client.UserFile.Username} has left the server!");

                UserManager.SendPlayerRecount();
            }
            catch (Exception ex) { Printer.Error(ex); }
        };

        public static void StartFeature()
        {
            try
            {
                Network.Ip = Master.ServerConfig.IP;
                Network.Port = Master.ServerConfig.Port;

                if (Master.ServerConfig.UseUPnP) { _ = new UPnP(); }

                Network.ServerListener = new TcpListener(IPAddress.Parse(Network.Ip), Network.Port);
                Network.ServerListener.Start();

                Printer.Warning("Server launched");
                Printer.Warning($"Listening for users at {Network.Ip}:{Network.Port}");
                Printer.Warning("Type 'help' to get a list of available commands");

                Task.Run(delegate { while (true) ListenForNewClients(); });
            }
            catch (Exception e) { Printer.Error(e); }
        }

        private static void ListenForNewClients()
        {
            ServerClient client = new ServerClient(Network.ServerListener.AcceptTcpClient(), new NetworkRuleset(null, OnDisconnect, OnReadPacket, null));

            if (GameServer.Commands.CMD_BanIP.IsIPBanned(client.CurrentIP))
            {
                Printer.Warning($"[IP Ban] Rejected connection from banned IP: {client.CurrentIP}");
                try { client.Listener.MarkForDisconnect(); } catch { }
                return;
            }

            // Use .Count directly — building the full array just to read .Length is wasteful.
            if (Network.ServerClients.Count >= Master.ServerConfig.MaxPlayers) PM_Logins.DenyConnectionWithReason(client, LoginResponse.Full);
            else if (Master.WorldValues == null && Network.ServerClients.Count > 0) PM_Logins.DenyConnectionWithReason(client, LoginResponse.NoWorld);
            else
            {
                Network.ServerClients.TryAdd(client, -1);
                InformationDisplayer.DisplayConnect(client);
                PM_Version.AskForClientVersion(client);
            }
        }

        public static ServerClient[] GetConnectedClients(ServerClient toExclude = null)
        {
            if (toExclude == null) return Network.ServerClients.Keys.ToArray();

            // Null-safe + case-insensitive — earlier impl NPE'd on half-initialized clients.
            string excludeName = toExclude.UserFile?.Username;
            if (string.IsNullOrEmpty(excludeName)) return Network.ServerClients.Keys.ToArray();
            return Network.ServerClients.Keys
                .Where(fetch => !string.Equals(fetch?.UserFile?.Username, excludeName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        public static ServerClient GetConnectedClientFromUsername(string username)
        {
            if (string.IsNullOrEmpty(username)) return null;
            if (ConnectedByUsername.TryGetValue(username, out ServerClient hit) && hit != null)
            {
                // Verify still-connected — half-closed sockets can linger past OnDisconnect.
                if (Network.ServerClients.ContainsKey(hit)) return hit;
                ConnectedByUsername.TryRemove(username, out _);
            }
            // Fallback scan (cache miss) — repopulates the index.
            foreach (ServerClient sc in Network.ServerClients.Keys)
            {
                if (string.Equals(sc?.UserFile?.Username, username, StringComparison.OrdinalIgnoreCase))
                {
                    ConnectedByUsername[username] = sc;
                    return sc;
                }
            }
            return null;
        }

        public static void SendPacketToAllClients(PacketHeader header, object obj, ServerClient toExclude = null)
        {
            foreach (ServerClient client in GetConnectedClients(toExclude))
            {
                client.Listener.EnqueuePacket(header, obj);
            }
        }
    }
}
