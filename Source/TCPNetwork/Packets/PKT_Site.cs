using Shared.Files.Sites;

namespace TCPNetwork.Packets
{
    public class PKT_Site : PKT_Base
    {
        public enum SiteStepMode { Accept, Build, Destroy, Info, Config, Rewards, Worker, CustomBuild, CustomInfo, WorkerJoin, WorkerLeave, Upgrade, SetDestination }

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

        /// <summary>
        /// KMH 26.5.20.1: Re-enabled. Best relevant skill level of the
        /// joining worker (0-20). Sent by the client when assigning a pawn
        /// to a custom site so the server can stamp WorkerProgress.BaseSkillLevel.
        /// Server-clamped — anti-cheat impact is bounded (max +60% production
        /// multiplier even if claimed L20 by a fresh client).
        /// </summary>
        public int _workerSkillLevel { get; set; } = 0;

        /// <summary>For SetDestination step: the new RewardDestination as int.</summary>
        public int _newRewardDestination { get; set; } = 0;
    }
}
