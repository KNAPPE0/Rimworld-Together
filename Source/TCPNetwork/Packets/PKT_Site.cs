using Shared.Files.Sites;

namespace TCPNetwork.Packets
{
    public class PKT_Site : PKT_Base
    {
        public enum SiteStepMode { Accept, Build, Destroy, Info, Config, Rewards, Worker, CustomBuild, CustomInfo, WorkerJoin, WorkerLeave, Upgrade }

        public SiteStepMode _stepMode { get; set; } = SiteStepMode.Accept;

        public SiteFile _file { get; set; } = new SiteFile();

        public PKT_SiteRewardConfig _rewardConfig { get; set; } = null;

        public SiteReward[] _rewardFiles { get; set; } = null;

        /// <summary>Custom site request data (for CustomBuild step).</summary>
        public CustomSiteRequest _customRequest { get; set; } = null;

        /// <summary>Custom site data returned for info display.</summary>
        public CustomSiteData _customData { get; set; } = null;

        /// <summary>Server-calculated cost for custom site (sent back to client for confirmation).</summary>
        public int _calculatedCost { get; set; } = 0;

        /// <summary>Server-calculated cycle time in minutes.</summary>
        public int _calculatedCycleMinutes { get; set; } = 0;

        /// <summary>Message from server about the custom site operation.</summary>
        public string _statusMessage { get; set; } = string.Empty;

        /// <summary>Best relevant skill level of the joining worker (0-20).</summary>
        public int _workerSkillLevel { get; set; } = 0;
    }
}
