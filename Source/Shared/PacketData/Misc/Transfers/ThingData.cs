using System;

namespace Shared
{
    [Serializable]
    public class ThingData
    {
        // Basic Information
        public string DefName { get; set; } = "";
        public string MaterialDefName { get; set; } = "";
        public int Quantity { get; set; } = 0;
        public string Quality { get; set; } = "";

        // State and Condition
        public bool IsMinified { get; set; } = false;
        public int Hitpoints { get; set; } = 0;

        // Transform (Position and Rotation)
        public string[] Position { get; set; } = Array.Empty<string>();
        public int Rotation { get; set; } = 0;

        // Growth
        public float GrowthTicks { get; set; } = 0f;
    }
}
