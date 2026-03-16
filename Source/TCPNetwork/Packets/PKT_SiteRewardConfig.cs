namespace TCPNetwork.Packets 
{
    public class PKT_SiteRewardConfig : PKT_Base
    {
        public string _siteDef { get; set; } = string.Empty;

        public string _rewardDef { get; set; } = string.Empty;
    }
}