using System;

namespace Shared
{
    [Serializable]
    public class MapFileData
    {
        // The owner of the map
        public string MapOwner { get; set; } = string.Empty; // Default to an empty string

        // The tile where the map is located
        public int MapTile { get; set; } = 0; // Default to 0, representing the map tile index

        // The data associated with the map
        public MapData MapData { get; set; } = new MapData(); // Initialize with a new MapData object
    }
}