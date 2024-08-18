using System;

namespace Shared
{
    [Serializable]
    public class PlanetNPCSettlement
    {
        // Tile on the map where the settlement is located
        public int Tile { get; set; } = 0; // Default to 0, represents the map tile

        // Name of the settlement
        public string Name { get; set; } = string.Empty; // Default to an empty string

        // Definition name of the faction that owns the settlement
        public string FactionDefName { get; set; } = string.Empty; // Default to an empty string, represents the faction's definition name
    }
}