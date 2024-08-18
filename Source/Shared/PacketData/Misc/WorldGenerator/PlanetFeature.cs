using System;

namespace Shared
{
    [Serializable]
    public class PlanetFeature
    {
        // Definition name of the planet feature
        public string DefName { get; set; } = string.Empty; // Default to an empty string, represents the feature's definition name

        // Name of the feature
        public string FeatureName { get; set; } = string.Empty; // Default to an empty string, represents the feature's name

        // Center point for drawing the feature on the planet
        public float[] DrawCenter { get; set; } = new float[2]; // Default to an array of two floats (e.g., x, y coordinates)

        // Maximum size of the feature in tiles when drawn
        public float MaxDrawSizeInTiles { get; set; } = 0f; // Default to 0, represents the maximum size in tiles
    }
}