using Shared.Files;
using static Shared.CommonEnumerators;

namespace TCPNetwork.Packets
{

    public class PKT_PlayerSettlement
    {
        public SettlementStepMode _stepMode { get; set; } = SettlementStepMode.Add;

        public SettlementFile _settlementFile { get; set; } = new SettlementFile();

        public override string ToString()
        {
            return $"PlayerSettlementData:|{_stepMode}|{_settlementFile}";
        }
    }
}
