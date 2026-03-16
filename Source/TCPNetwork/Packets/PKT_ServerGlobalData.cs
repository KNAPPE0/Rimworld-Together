using Shared.Details.Planet;
using Shared.Files;
using Shared.Files.Actions;
using Shared.Files.Configs;
using Shared.Files.Configs.Mods;
using Shared.Files.Sites;

namespace TCPNetwork.Packets
{
    public class PKT_ServerGlobalData : PKT_Base
    {
        public string _serverName { get; set; } = null;

        public bool _isClientAdmin { get; set; } = false;

        public bool _isClientFactionMember { get; set; } = false;

        public SiteType[] _siteValues { get; set; } = null;

        public EventFile[] _eventValues { get; set; } = null;

        public ActionsConfigFile _actionValues { get; set; } = null;

        public RoadsConfigFile _roadValues { get; set; } = null;

        public ScenarioConfigFile _scenarioValues { get; set; } = null;

        public StorytellerConfigFile _storytellerValues { get; set; } = null;

        public DifficultyConfigFile _difficultyValues { get; set; } = null;

        public NPCSettlementDetail[] _npcSettlements { get; set; } = null;

        public SettlementFile[] _playerSettlements { get; set; } = null;

        public SiteFile[] _playerSites { get; set; } = null;

        public RoadDetail[] _roads { get; set; } = null;

        public PollutionDetail[] _pollutedTiles { get; set; } = null;

        public ModsConfigFile _modConfigs { get; set; } = null;
    }
}