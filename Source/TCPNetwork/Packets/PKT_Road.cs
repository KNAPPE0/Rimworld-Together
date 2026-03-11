using Shared.Details.Planet;
using static Shared.CommonEnumerators;

namespace TCPNetwork.Packets
{

    public class PKT_Road
    {
        public RoadStepMode _stepMode { get; set; } = RoadStepMode.Add;

        public RoadDetail _details { get; set; } = null;

        public override string ToString()
        {
            return $"RoadData:|{_stepMode}|{_details}";
        }
    }
}