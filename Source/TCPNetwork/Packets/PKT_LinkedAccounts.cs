using System.Collections.Generic;

namespace TCPNetwork.Packets
{
    /// <summary>
    /// KMH: Server → client snapshot of every player's linked Discord
    /// display name. Pushed on login and re-broadcast when any user's
    /// link state changes.
    ///
    /// Map keys are case-insensitive in-game usernames; values are the
    /// Discord display name (e.g. "knappe", "varick"). Players without a
    /// linked Discord account are omitted from the map (so a missing entry
    /// = "not linked").
    /// </summary>
    public class PKT_LinkedAccounts : PKT_Base
    {
        public Dictionary<string, string> LinkedDiscordNamesByUsername { get; set; }
            = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
    }
}
