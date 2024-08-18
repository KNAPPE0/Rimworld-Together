using System;

namespace Shared
{
    [Serializable]
    public class WorldValuesFile
    {
        // Misc
        public int PersistentRandomValue { get; set; } = 0;

        // World Values
        public string SeedString { get; set; } = string.Empty;
        public float PlanetCoverage { get; set; } = 0.0f;
        public int Rainfall { get; set; } = 0;
        public int Temperature { get; set; } = 0;
        public int Population { get; set; } = 0;
        public float Pollution { get; set; } = 0.0f;

        // World Features
        public PlanetFeature[] Features { get; set; } = Array.Empty<PlanetFeature>();
        public RoadDetails[] Roads { get; set; } = Array.Empty<RoadDetails>();
        public RiverDetails[] Rivers { get; set; } = Array.Empty<RiverDetails>();
        public PollutionDetails[] PollutedTiles { get; set; } = Array.Empty<PollutionDetails>();
        public PlanetNPCFaction[] NPCFactions { get; set; } = Array.Empty<PlanetNPCFaction>();
        public PlanetNPCSettlement[] NPCSettlements { get; set; } = Array.Empty<PlanetNPCSettlement>();

        // Parameterless constructor for default initialization
        public WorldValuesFile() { }

        // Constructor for specific value initialization
        public WorldValuesFile(
            int persistentRandomValue,
            string seedString,
            float planetCoverage,
            int rainfall,
            int temperature,
            int population,
            float pollution,
            PlanetFeature[] features,
            RoadDetails[] roads,
            RiverDetails[] rivers,
            PollutionDetails[] pollutedTiles,
            PlanetNPCFaction[] npcFactions,
            PlanetNPCSettlement[] npcSettlements)
        {
            PersistentRandomValue = persistentRandomValue;
            SeedString = seedString ?? string.Empty; // Handle potential null value
            PlanetCoverage = planetCoverage;
            Rainfall = rainfall;
            Temperature = temperature;
            Population = population;
            Pollution = pollution;
            Features = features ?? Array.Empty<PlanetFeature>();
            Roads = roads ?? Array.Empty<RoadDetails>();
            Rivers = rivers ?? Array.Empty<RiverDetails>();
            PollutedTiles = pollutedTiles ?? Array.Empty<PollutionDetails>();
            NPCFactions = npcFactions ?? Array.Empty<PlanetNPCFaction>();
            NPCSettlements = npcSettlements ?? Array.Empty<PlanetNPCSettlement>();
        }
    }
}