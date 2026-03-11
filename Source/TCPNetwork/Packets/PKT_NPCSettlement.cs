using Shared.Details.Planet;
using static Shared.CommonEnumerators;

namespace TCPNetwork.Packets
{
    public class PKT_NPCSettlement
    {
        public SettlementStepMode _stepMode { get; set; } = SettlementStepMode.Add;

        public NPCSettlementDetail _settlementData { get; set; } = new NPCSettlementDetail();

        public override string ToString()
        {
            return $"NPCSettlementData:|{_stepMode}|{_settlementData}";
        }
    }
}