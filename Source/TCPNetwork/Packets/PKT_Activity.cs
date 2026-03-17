using static Shared.CommonEnumerators;

namespace TCPNetwork.Packets
{
    public class PKT_Activity : PKT_Base
    {
        public ActivityStepMode _stepMode { get; set; } = ActivityStepMode.Request;

        public int _targetTile { get; set; } = -1;

        public byte[] _mapRawData { get; set; } = null;
    }
}