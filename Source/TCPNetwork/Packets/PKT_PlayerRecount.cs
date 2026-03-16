using System.Collections.Generic;

namespace TCPNetwork.Packets
{
    public class PKT_PlayerRecount : PKT_Base
    {
        public int _currentPlayerCount { get; set; } = -1;

        public List<string> _currentPlayerNames { get; set; } = new List<string>();
    }
}
