using Shared.Details.Planet;
using static Shared.CommonEnumerators;

namespace TCPNetwork.Packets
{
    public class PKT_Road : PKT_Base
    {
        public RoadStepMode _stepMode { get; set; } = RoadStepMode.Add;

        public RoadDetail _details { get; set; } = null;
    }
}