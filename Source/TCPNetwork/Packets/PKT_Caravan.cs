using Shared.Files;
using static Shared.CommonEnumerators;

namespace TCPNetwork.Packets
{
    public class PKT_Caravan : PKT_Base
    {
        public CaravanStepMode _stepMode { get; set; } = CaravanStepMode.Add;

        public CaravanFile _caravanFile { get; set; } = null;
    }
}