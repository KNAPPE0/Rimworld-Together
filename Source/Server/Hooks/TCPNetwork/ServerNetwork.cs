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
        // KMH 2.7: O(1) username→client index. Previously every lookup
        // (GetConnectedClientFromUsername) did a linear scan via
        // GetConnectedClients().FirstOrDefault(...), which was called from
        // marketplace buys, treasury access, Discord links, chat broadcasts,
        // and the player-stats path — adding up to dozens of O(n) scans per
        // user action on busy servers.
        //
        // Kept as a separate index (not derived from Network.ServerClients
        // each call) so it survives the same lifecycle as the connected-client
        // dictionary: populated on login (RegisterAuthenticatedClient) and
        // pruned on disconnect.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ServerClient> ConnectedByUsername =
            new System.Collections.Concurrent.ConcurrentDictionary<string, ServerClient>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// KMH 2.7: Call once a client has finished login + has a non-null
        /// <c>UserFile.Username</c>. Safe to call multiple times — last writer
        /// wins per username, which matches "kick the older session" semantics.
        /// </summary>
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

                // KMH 2.7: Prune the username index. Only remove if we still
                // own the slot — a fast reconnect could already have replaced
                // it with a fresh session for the same username, and we don't
                // want to evict the new session by mistake.
                string u = client?.UserFile?.Username;
                if (!string.IsNullOrEmpty(u))
                {
                    if (ConnectedByUsername.TryGetValue(u, out ServerClient cur) && ReferenceEquals(cur, client))
                        ConnectedByUsername.TryRemove(u, out _);
                }

                InformationDisplayer.DisplayDisconnect(client);
                // KMH: Announce leave to Discord
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

            // KMH: Check IP ban before anything else
            if (GameServer.Commands.CMD_BanIP.IsIPBanned(client.CurrentIP))
            {
                Printer.Warning($"[IP Ban] Rejected connection from banned IP: {client.CurrentIP}");
                try { client.Listener.MarkForDisconnect(); } catch { }
                return;
            }

            if (GetConnectedClients().Length >= Master.ServerConfig.MaxPlayers) PM_Logins.DenyConnectionWithReason(client, LoginResponse.Full);
            else if (Master.WorldValues == null && GetConnectedClients().Length > 0) PM_Logins.DenyConnectionWithReason(client, LoginResponse.NoWorld);
            else
            {
                Network.ServerClients.TryAdd(client, -1);
                InformationDisplayer.DisplayConnect(client);
                PM_Version.AskForClientVersion(client);
            }
        }

        public static ServerClient[] GetConnectedClients(ServerClient toExclude = null)
        {
            if (toExclude != null) return Network.ServerClients.Keys.Where(fetch => fetch.UserFile.Username != toExclude.UserFile.Username).ToArray();
            else return Network.ServerClients.Keys.ToArray();
        }

        public static ServerClient GetConnectedClientFromUsername(string username)
        {
            // KMH 2.7: O(1) lookup via the username index. Falls back to a
            // linear scan only if the cache hasn't been populated for this
            // username yet (defensive — should never happen post-login).
            if (string.IsNullOrEmpty(username)) return null;
            if (ConnectedByUsername.TryGetValue(username, out ServerClient hit) && hit != null)
            {
                // Verify it's still connected (the OnDisconnect path scrubs
                // most cases, but a half-closed socket could linger).
                if (Network.ServerClients.ContainsKey(hit)) return hit;
                ConnectedByUsername.TryRemove(username, out _);
            }
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