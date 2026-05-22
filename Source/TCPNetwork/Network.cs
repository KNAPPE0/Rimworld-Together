using Shared;
using Shared.Misc;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using TCPNetwork.Files.Client;
using static Shared.Misc.Printer;

namespace TCPNetwork
{
    public class Network
    {
        public static string Ip { get; set; } = string.Empty;

        public static int Port { get; set; } = int.MaxValue;

        public static Listener ServerEndpoint { get; set; } = null;

        public static Listener BrowserEndpoint { get; set; } = null;

        public static TcpListener ServerListener { get; set; } = null;

        public static string BrowserIp { get; set; } = "66.29.129.72";

        public static int BrowserServerPort { get; set; } = 7777;

        public static int BrowserClientPort { get; set; } = 7778;

        // KMH 26.5.22.1: Ported from upstream (May 2026). Cap doubled from
        // 8 MB → 16 MB so very large autosaves / map snapshots no longer
        // tripping the size check and force-disconnecting. KMH ships much
        // bigger map files than vanilla RWT (treasury inventories, quest
        // boards, marketplace listings, lifetime stats), and the 8 MB
        // ceiling had been forcing some long-running guilds to fall under
        // it. The cap is still enforced — it's just generous now.
        public static readonly int MaxPacketSize = 16777216;

        public static ConcurrentDictionary<ServerClient, int> ServerClients { get; private set; } = new ConcurrentDictionary<ServerClient, int>();

        public static readonly TimeSpan BrowserTelemetryInterval = TimeSpan.FromSeconds(300);

        public static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(10);

        public static readonly PacketHeader[] IgnoreLogPackets = { PacketHeader.KeepAliveManager };

        public static readonly PacketHeader[] PreVerifyHeaders = 
        { 
            PacketHeader.KeepAliveManager,
            PacketHeader.VersionManager,
            PacketHeader.LoginManager,
            PacketHeader.ServerBrowserListing,
            PacketHeader.ServerBrowserTelemetry
        };

        public static void ReadFullPacket(Stream stream, byte[] content)
        {
            int readBytes = 0;

            try
            {
                while (readBytes < content.Length)
                {
                    int read = stream.Read(content, readBytes, content.Length - readBytes);
                    if (read == 0) throw new ArgumentOutOfRangeException();
                    readBytes += read;
                }
            }
            catch (Exception e) { Printer.Warning(e, LogImportanceMode.Verbose); }
        }

        public static bool CheckForPacketSize(ServerClient client, byte[] buffer)
        {
            if (BitConverter.ToInt32(buffer, 0) < MaxPacketSize) return true;
            else
            {
                client.Listener.MarkForDisconnect();
                return false;
            }
        }

        public static bool CheckIfPacketIsValidated(ServerClient client, PacketHeader header)
        {
            if (client.IsVerified || Network.PreVerifyHeaders.Contains(header)) return true;
            else
            {
                client.Listener.MarkForDisconnect();
                return false;
            }
        }
    }
}
