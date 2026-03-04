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

        public static string Port { get; set; } = string.Empty;

        public virtual Action<PacketHeader, byte[], ServerClient> OnReadPacket { get; set; }

        public virtual Action<ServerClient> OnWritePacket { get; set; }

        public virtual Action<ServerClient> OnConnect { get; set; }

        public virtual Action<ServerClient> OnDisconnect { get; set; }

        public Listener ClientListener { get; set; } = null;

        public TcpListener ServerListener { get; set; }

        public List<ServerClient> ServerClients { get; private set; } = new List<ServerClient>();

        public static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(3);

        public static readonly TimeSpan KeepAliveMaxTime = TimeSpan.FromSeconds(60);

        public static readonly string DefaultParserMethodName = "ParsePacket";

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
