using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synchronous.Objects
{
    public class PlayerMentalState
    {
        public enum MentalMode { Add, Remove }

        public MentalMode Mode { get; set; } = MentalMode.Add;

        public int MapID { get; set; } = 0;

        public string PawnID { get; set; } = string.Empty;

        public string MentalStateDefName { get; set; } = string.Empty;
    }
}
