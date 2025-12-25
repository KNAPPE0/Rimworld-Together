using Shared.Files.Configs.Mods;

namespace Shared.Files
{
    public class MapFile
    {
        public int Tile { get; set; } = -1;

        public int[] Size { get; set; } = null;

        public string Username { get; set; } = string.Empty;

        public string SettlementName { get; set; } = string.Empty; // "Mushroomtas"
        public string FactionName { get; set; } = string.Empty;    // "Eratolior"

        public int Wealth { get; set; } = -1;

        public double WealthExact { get; set; } = -1;

        public int GameTicks { get; set; } = -1;

        public double RealPlayTimeInteractingSeconds { get; set; } = -1;

        public long LastSavedUtcTicks { get; set; } = 0;

        public string CurWeatherDefName { get; set; } = string.Empty;

        public ModsConfigFile Mods { get; set; } = null;

        public MapTileDetail[] Tiles { get; set; } = new MapTileDetail[0];

        public string[] FactionThings { get; set; } = null;

        public string[] NonFactionThings { get; set; } = null;

        public HumanFile[] FactionHumans { get; set; } = null;

        public HumanFile[] NonFactionHumans { get; set; } = null;

        public string[] FactionAnimals { get; set; } = null;

        public string[] NonFactionAnimals { get; set; } = null;
    }
}