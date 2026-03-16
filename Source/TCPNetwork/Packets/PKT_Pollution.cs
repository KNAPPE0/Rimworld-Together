using Shared.Details.Planet;

namespace TCPNetwork.Packets
{
    public class PKT_Pollution : PKT_Base
    {
        public PollutionDetail _pollutionData { get; set; } = new PollutionDetail();
    }
}