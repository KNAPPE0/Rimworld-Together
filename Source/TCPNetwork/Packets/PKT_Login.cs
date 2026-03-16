using Shared.Files.Configs.Mods;
using System.Collections.Generic;
using static Shared.CommonEnumerators;

namespace TCPNetwork.Packets
{
    public class PKT_Login : PKT_Base
    {
        public string _username { get; set; } = string.Empty;

        public string _password { get; set; } = string.Empty;

        public ModsConfigFile _runningMods { get; set; } = null;

        public LoginResponse _tryResponse { get; set; } = LoginResponse.Invalid;

        public List<string> _extraDetails { get; set; } = new List<string>();
    }
}
