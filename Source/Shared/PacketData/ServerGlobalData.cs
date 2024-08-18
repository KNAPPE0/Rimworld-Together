using System;

namespace Shared
{
    [Serializable]
    public class ServerGlobalData
    {
        // Indicates if the client is an admin
        public bool IsClientAdmin { get; set; } = false; // Default to false

        // Indicates if custom scenarios are allowed
        public bool AllowCustomScenarios { get; set; } = false; // Default to false

        // Indicates if the client is a member of a faction
        public bool IsClientFactionMember { get; set; } = false; // Default to false

        // Server-wide values and configurations
        public SiteValuesFile SiteValues { get; set; } = new SiteValuesFile(); // Initialize with a new SiteValuesFile object
        public EventValuesFile EventValues { get; set; } = new EventValuesFile(); // Initialize with a new EventValuesFile object
        public ActionValuesFile ActionValues { get; set; } = new ActionValuesFile(); // Initialize with a new ActionValuesFile object
        public RoadValuesFile RoadValues { get; set; } = new RoadValuesFile(); // Initialize with a new RoadValuesFile object
        public DifficultyValuesFile DifficultyValues { get; set; } = new DifficultyValuesFile(); // Initialize with a new DifficultyValuesFile object

        // Data related to NPC settlements, player settlements, sites, caravans, roads, and polluted tiles
        public PlanetNPCSettlement[] NpcSettlements { get; set; } = new PlanetNPCSettlement[0]; // Initialize with an empty array
        public OnlineSettlementFile[] PlayerSettlements { get; set; } = new OnlineSettlementFile[0]; // Initialize with an empty array
        public OnlineSiteFile[] PlayerSites { get; set; } = new OnlineSiteFile[0]; // Initialize with an empty array
        public CaravanDetails[] PlayerCaravans { get; set; } = new CaravanDetails[0]; // Initialize with an empty array
        public RoadDetails[] Roads { get; set; } = new RoadDetails[0]; // Initialize with an empty array
        public PollutionDetails[] PollutedTiles { get; set; } = new PollutionDetails[0]; // Initialize with an empty array
    }
}