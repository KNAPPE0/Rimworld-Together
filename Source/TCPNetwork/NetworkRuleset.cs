using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TCPNetwork.Files.Client;

namespace TCPNetwork
{
    public class NetworkRuleset
    {
        public Action<ServerClient> OnConnect { get; set; } = null;

        public Action<ServerClient> OnDisconnect { get; set; } = null;

        public Action<PacketHeader, byte[], ServerClient> OnRead { get; set; } = null;

        public Action<ServerClient> OnWrite { get; set; } = null;

        public NetworkRuleset(Action<ServerClient> onConnect, Action<ServerClient> onDisconnect, 
            Action<PacketHeader, byte[], ServerClient> onRead, Action<ServerClient> onWrite)
        {
            this.OnConnect = onConnect;
            this.OnDisconnect = onDisconnect;
            this.OnRead = onRead;
            this.OnWrite = onWrite;
        }
    }
}
