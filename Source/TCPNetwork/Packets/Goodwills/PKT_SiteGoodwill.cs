using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Shared.CommonEnumerators;

namespace TCPNetwork.Packets.Goodwills
{
    public class PKT_SiteGoodwill
    {
        public int Tile { get; set; } = -1;

        public Goodwill Goodwill { get; set; } = Goodwill.Neutral;
    }
}
