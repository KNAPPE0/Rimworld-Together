using static Shared.CommonEnumerators;

namespace TCPNetwork.Packets
{
    public class PKT_Command : PKT_Base
    {
        public CommandMode _commandMode { get; set; } = CommandMode.Op;

        public string _details { get; set; } = string.Empty;
    }
}
