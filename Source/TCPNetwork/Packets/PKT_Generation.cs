using Shared.Files.Configs;
using static Shared.CommonEnumerators;

namespace TCPNetwork.Packets
{
    public class GameParameterData
    {
        public GenStepMode _stepMode { get; set; } = GenStepMode.Scenario;

        public byte[] _bytes { get; set; } = null;
    }
}