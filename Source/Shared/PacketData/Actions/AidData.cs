using System;
using static Shared.CommonEnumerators;

namespace Shared
{
    [Serializable]
    public class AidData
    {
        // The current step mode of the aid process
        public AidStepMode StepMode { get; set; } = AidStepMode.Send; // Default to 'Send', adjust as necessary

        // Tiles representing the source and destination of the aid
        public int FromTile { get; set; } = 0; // Tile where the aid is coming from
        public int ToTile { get; set; } = 0;   // Tile where the aid is going

        // Data about the human involved in the aid
        public HumanData HumanData { get; set; } = new HumanData(); // Initialize with a new HumanData object
    }
}