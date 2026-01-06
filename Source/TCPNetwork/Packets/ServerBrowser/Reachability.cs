namespace TCPNetwork.Packets.ServerBrowser
{
    /// <summary>
    /// Used by the client to determine reachability of a server
    /// </summary>
    public enum Reachability
    {
        Unknown = 0,
        Unreachable = 1,
        Reachable = 2,
    }
}