using System.Collections.Generic;

namespace TCPNetwork.Packets
{
    /// <summary>
    /// Client → server snapshot of (defName → human label) for every
    /// <c>ThingDef</c> the client has loaded. The server merges these into a
    /// shared cache so Discord output and server-side messaging can show
    /// "Plasteel" / "Melee Weapon Knife" instead of the raw defName.
    /// </summary>
    public class PKT_ItemLabels : PKT_Base
    {
        public Dictionary<string, string> LabelsByDefName { get; set; } = new Dictionary<string, string>();
    }
}
