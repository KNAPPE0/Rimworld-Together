using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TCPNetwork.Files.Client;
using static Shared.CommonEnumerators;

namespace TCPNetwork
{
    public class Network
    {
        public const int PacketLengthSizeInBytes = sizeof(int);

        public static string Ip { get; set; } = string.Empty;

        public static int Port { get; set; } = int.MaxValue;

        public static Listener ServerEndpoint { get; set; } = null;

        public static TcpListener ServerListener { get; set; } = null;

        public static List<ServerClient> ServerClients { get; private set; } = new List<ServerClient>();

        public static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(3);

        public static readonly TimeSpan KeepAliveMaxTime = TimeSpan.FromSeconds(60);

        public static readonly PacketHeader[] IgnoreLogPackets = { PacketHeader.KeepAliveManager };

        public static readonly PacketHeader[] BypassReadyPackets =
        {
            PacketHeader.LoginManager,
            PacketHeader.KeepAliveManager,
            PacketHeader.VersionManager,
            PacketHeader.SaveManager,
            PacketHeader.WorldManager,
            PacketHeader.GlobalDataManager,
            PacketHeader.RecountManager,
            PacketHeader.ChatManager,
            PacketHeader.ConsoleManager,
            PacketHeader.ServerBrowserReachability
        };
    }
}
