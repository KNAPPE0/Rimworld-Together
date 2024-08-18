using System;

namespace Shared
{
    [Serializable]
    public class MapData
    {
        // Miscellaneous map details
        public int MapTile { get; set; } = 0; // Default to 0, representing the map tile index
        public int[] MapSize { get; set; } = new int[2]; // Default to a 2-element array for width and height
        public string MapOwner { get; set; } = string.Empty; // Default to an empty string
        public string[] MapMods { get; set; } = new string[0]; // Default to an empty array
        public string CurWeatherDefName { get; set; } = string.Empty; // Default to an empty string

        // Tile data
        public string[] TileDefNames { get; set; } = new string[0]; // Default to an empty array
        public string[] TileRoofDefNames { get; set; } = new string[0]; // Default to an empty array
        public bool[] TilePollutions { get; set; } = new bool[0]; // Default to an empty array

        // Thing data
        public ThingData[] FactionThings { get; set; } = new ThingData[0]; // Default to an empty array
        public ThingData[] NonFactionThings { get; set; } = new ThingData[0]; // Default to an empty array

        // Human data
        public HumanData[] FactionHumans { get; set; } = new HumanData[0]; // Default to an empty array
        public HumanData[] NonFactionHumans { get; set; } = new HumanData[0]; // Default to an empty array

        // Animal data
        public AnimalData[] FactionAnimals { get; set; } = new AnimalData[0]; // Default to an empty array
        public AnimalData[] NonFactionAnimals { get; set; } = new AnimalData[0]; // Default to an empty array
    }
}