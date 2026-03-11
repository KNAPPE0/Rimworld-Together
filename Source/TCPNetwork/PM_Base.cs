using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TCPNetwork.Files.Client;

namespace TCPNetwork
{
    [ManagesPacket]
    public abstract class PM_Base
    {
        public abstract void Receive(ServerClient client, byte[] bytes, PacketHeader header);
    }
}
