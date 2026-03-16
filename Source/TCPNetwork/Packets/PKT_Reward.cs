using Shared.Files.Sites;

namespace TCPNetwork.Packets 
{
    public class PKT_Reward : PKT_Base
    {
        public SiteReward[] _rewardData { get; set; } = null;
    }
}