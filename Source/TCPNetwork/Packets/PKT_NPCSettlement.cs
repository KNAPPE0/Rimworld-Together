using Shared.Details.Planet;
using static Shared.CommonEnumerators;

namespace TCPNetwork.Packets
{
    public class PKT_NPCSettlement : PKT_Base
    {
        public SettlementStepMode _stepMode { get; set; } = SettlementStepMode.Add;

        public NPCSettlementDetail _settlementData { get; set; } = new NPCSettlementDetail();
    }
}