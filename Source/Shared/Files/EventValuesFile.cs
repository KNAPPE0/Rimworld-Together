using System;

namespace Shared
{
    [Serializable]
    public class EventValuesFile
    {
        public int RaidCost { get; set; } = 250;
        public int InfestationCost { get; set; } = 250;
        public int MechClusterCost { get; set; } = 250;
        public int ToxicFalloutCost { get; set; } = 250;
        public int ManhunterCost { get; set; } = 250;
        public int WandererCost { get; set; } = 250;
        public int FarmAnimalsCost { get; set; } = 250;
        public int ShipChunkCost { get; set; } = 250;
        public int TraderCaravanCost { get; set; } = 250;
    }
}