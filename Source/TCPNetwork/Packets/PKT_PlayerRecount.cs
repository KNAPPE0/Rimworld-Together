using System.Collections.Generic;

namespace TCPNetwork.Packets
{

    public class PKT_PlayerRecount
    {
        public int _currentPlayerCount { get; set; } = -1;

        public List<string> _currentPlayerNames { get; set; } = new List<string>();

        public override string ToString()
        {
            return $"PlayerRecountData:|{_currentPlayerCount}|{_currentPlayerNames?.Count ?? 0}";
        }
    }
}
