using Shared.Files;
using static Shared.CommonEnumerators;

namespace TCPNetwork.Packets
{
    public class PKT_PlayerSettlement : PKT_Base
    {
        public SettlementStepMode _stepMode { get; set; } = SettlementStepMode.Add;

        public SettlementFile _settlementFile { get; set; } = new SettlementFile();
    }
}
