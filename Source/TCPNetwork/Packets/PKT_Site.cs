using Shared.Files.Sites;
using static Shared.CommonEnumerators;

namespace TCPNetwork.Packets
{
    public class PKT_Site : PKT_Base
    {
        public SiteStepMode _stepMode { get; set; } = SiteStepMode.Accept;

        public SiteFile _file { get; set; } = new SiteFile();

        public PKT_SiteRewardConfig _rewardConfig { get; set; } = null;

        public SiteReward[] _rewardFiles { get; set; } = null;
    }
}
