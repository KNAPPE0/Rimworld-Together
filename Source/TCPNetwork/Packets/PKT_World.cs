using static Shared.CommonEnumerators;

namespace TCPNetwork.Packets
{
    public class PKT_World : PKT_Base
    {
        public WorldStepMode _stepMode { get; set; } = WorldStepMode.AskFor;

        public byte[] _fileBytes { get; set; } = null;
    }
}
