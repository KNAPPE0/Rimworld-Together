using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synchronous.Objects
{
    public class PlayerJob
    {
        public int MapTile { get; set; } = -1;

        public string PawnID { get; set; } = string.Empty;

        public string Job { get; set; } = string.Empty;
    }
}
