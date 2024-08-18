using System;
using static Shared.CommonEnumerators;

namespace Shared
{
    [Serializable]
    public class WorldData
    {
        // The mode or step for the world operation
        public WorldStepMode WorldStepMode { get; set; } = WorldStepMode.Required; // Default to 'Required', adjust as necessary

        // The values associated with the world
        public WorldValuesFile WorldValuesFile { get; set; } = new WorldValuesFile(); // Initialize with a new WorldValuesFile object
    }
}