using Shared.Files.Sites;

namespace TCPNetwork.Packets 
{

    public class PKT_Reward 
    {
        public SiteReward[] _rewardData { get; set; } = null;

        public override string ToString()
        {
            return $"RewardData:|{_rewardData?.Length ?? 0}";
        }
    }
}