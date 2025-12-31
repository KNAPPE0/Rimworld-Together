using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synchronous.Objects
{
    public class PlayerDraft
    {
        public PlayerDraft(int mapID, string pawnID, bool draftValue)
        {
            MapID = mapID;
            PawnID = pawnID;
            DraftValue = draftValue;
        }

        public int MapID { get; set; } = 0;

        public string PawnID { get; set; } = string.Empty;

        public bool DraftValue { get; set; } = false;
    }
}
