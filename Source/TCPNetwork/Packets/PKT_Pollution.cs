using Shared.Details.Planet;

namespace TCPNetwork.Packets
{
    public class PKT_Pollution 
    {
        public PollutionDetail _pollutionData { get; set; } = new PollutionDetail();

        public override string ToString()
        {
            return $"PollutionData:|{_pollutionData}";
        }
    }
}