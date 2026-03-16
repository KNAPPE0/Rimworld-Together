using TCPNetwork.ServerBrowser;

namespace TCPNetwork.Packets.ServerBrowser;

public class PKT_AllServers : PKT_Base
{
    public ServerInfo[] _serverInfos;
}