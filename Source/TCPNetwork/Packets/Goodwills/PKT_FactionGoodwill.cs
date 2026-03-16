using System.Collections;
using System.Collections.Generic;
using static Shared.CommonEnumerators;

namespace TCPNetwork.Packets.Goodwills
{
    public class PKT_FactionGoodwill : PKT_Base
    {
        public int _tile { get; set; } = -1;

        public string _username { get; set; } = string.Empty;

        public Goodwill _goodwill { get; set; } = Goodwill.Neutral;

        public List<PKT_SettlementGoodwill> _settlements { get; set; } = new List<PKT_SettlementGoodwill>();

        public List<PKT_SiteGoodwill> _sites { get; set; } = new List<PKT_SiteGoodwill>();
    }
}
