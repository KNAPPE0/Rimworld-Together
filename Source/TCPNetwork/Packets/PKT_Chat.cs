using System.Collections.Generic;

namespace TCPNetwork.Packets
{
    public class PKT_Chat : PKT_Base
    {
        public static Dictionary<ChatColor, string> MessageColorDictionary { get; private set; } = new Dictionary<ChatColor, string>()
        {
            { ChatColor.Normal, "<color=white>" },
            { ChatColor.Admin, "<color=red>" },
            { ChatColor.Console, "<color=yellow>" },
            { ChatColor.Private, "<color=#3ae0dd>" },
            { ChatColor.Server, " <color=white>" },
            { ChatColor.Discord, "<color=#7289DA>" }
        };

        public enum ChatColor { Normal, Admin, Console, Private, Server, Discord }

        public ChatColor UsernameColor { get; set; } = ChatColor.Normal;

        public ChatColor MessageColor { get; set; } = ChatColor.Normal;

        public string Username { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public bool IsCommand { get; set; } = false;
    }
}
