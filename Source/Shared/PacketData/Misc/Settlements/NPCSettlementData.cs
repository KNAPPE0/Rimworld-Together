using System;
using static Shared.CommonEnumerators;

namespace Shared
{
    [Serializable]
    public class NPCSettlementData
    {
        // The current step mode for the NPC settlement
        public SettlementStepMode StepMode { get; set; } = SettlementStepMode.Add; // Default to 'Add', adjust as necessary

        // Details of the NPC settlement
        public PlanetNPCSettlement Details { get; set; } = new PlanetNPCSettlement(); // Initialize with a new PlanetNPCSettlement object
    }
}