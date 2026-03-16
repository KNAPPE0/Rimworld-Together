using static Shared.CommonEnumerators;

namespace TCPNetwork.Packets
{
    public class PKT_Chat : PKT_Base
    {
        public ChatColor _usernameColor { get; set; } = ChatColor.Normal;

        public ChatColor _messageColor { get; set; } = ChatColor.Normal;

        public string _username { get; set; } = string.Empty;

        public string _message { get; set; } = string.Empty;
    }
}
