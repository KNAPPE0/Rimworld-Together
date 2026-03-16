using MessagePack;
using Shared.Files.Configs.Mods;

namespace TCPNetwork.ServerBrowser;
/// <summary>
/// Do not make sealed, master server inherits this
/// </summary>
public class ServerInfo
{
    public string _ip;
    public string _name;
    public string _description;
    public ModsConfigFile _config;
    public int _maximumPlayerCount;
    public int _currentPlayerCount;
    public int _port;
    public string _version;
    [IgnoreMember] public volatile Reachability Reachability = Reachability.Unknown;
    [IgnoreMember] public ulong _secret;

    public override bool Equals(object obj)
    {
        if (obj is ServerInfo info)
        {
            return info._ip == _ip && info._port == _port;
        }
        else
            return base.Equals(obj);
    }
    public override int GetHashCode()
    {
        return _ip.GetHashCode() + _port.GetHashCode();
    }
    public static bool operator ==(ServerInfo a, ServerInfo b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (ReferenceEquals(a, null) || ReferenceEquals(b, null)) return false;

        return (a._ip == b._ip) && (a._port == b._port);
    }
    public static bool operator !=(ServerInfo a, ServerInfo b)
    {
        return !(a == b);
    }
}