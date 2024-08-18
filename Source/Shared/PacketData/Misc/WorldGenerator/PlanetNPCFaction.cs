using System;

namespace Shared
{
    [Serializable]
    public class PlanetNPCFaction
    {
        // Definition name of the faction
        public string FactionDefName { get; set; } = string.Empty; // Default to an empty string, represents the faction's definition name

        // Name of the faction
        public string FactionName { get; set; } = string.Empty; // Default to an empty string, represents the faction's name

        // Color of the faction, represented as an array of floats (e.g., RGB values)
        public float[] FactionColor { get; set; } = new float[3]; // Default to an array of three floats (e.g., R, G, B), initialized to zeros
    }
}