using System;

namespace Shared
{
    [Serializable]
    public class TimeSpeedOrder
    {
        // Target time speed and map ticks
        public int TargetTimeSpeed { get; set; } = 0;
        public int TargetMapTicks { get; set; } = 0;
    }
}