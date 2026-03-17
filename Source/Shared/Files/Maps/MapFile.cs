using Shared.Files.Configs.Mods;
using System.Collections.Generic;

namespace Shared.Files.Maps
{
    public class MapFile
    {
        public int Tile { get; set; } = -1;

        public int[] Size { get; set; } = null;

        public string Username { get; set; } = string.Empty;

        public string SettlementName { get; set; } = string.Empty;

        public string FactionName { get; set; } = string.Empty;

        public double WealthExact { get; set; } = -1;

        public int GameTicks { get; set; } = -1;

        public double RealPlayTimeSeconds { get; set; } = -1;

        public double RealPlayTimeInteractingSeconds { get; set; } = -1;

        public long LastSavedUtcTicks { get; set; } = 0;

        public int Wealth { get; set; } = -1;

        public byte WeatherByte { get; set; } = byte.MaxValue;

        public string CurWeatherDefName { get; set; } = string.Empty;

        public ModsConfigFile Mods { get; set; } = new ModsConfigFile();

        public List<MapTile> Tiles { get; set; } = new List<MapTile>();

        public List<string> Things { get; set; } = new List<string>();

        public List<string> Pawns { get; set; } = new List<string>();

        public List<bool> Pollutions { get; set; } = new List<bool>();
    }
}