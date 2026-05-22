using Shared.Files.Economy;
using System.Collections.Generic;

namespace TCPNetwork.Packets
{
    /// <summary>
    /// All treasury-related client↔server traffic uses this single envelope.
    /// The <see cref="StepMode"/> tells the receiver what action to take.
    /// </summary>
    public class PKT_Treasury : PKT_Base
    {
        public enum StepMode
        {
            /// <summary>Client → server: send me the treasury identified by OwnerKey (or my own if blank).</summary>
            Request,

            /// <summary>Server → client: a snapshot reply.</summary>
            Snapshot,

            /// <summary>Client → server: deposit silver (Amount) and items (ItemDefName/Amount or ItemBundle).</summary>
            Deposit,

            /// <summary>Client → server: withdraw silver (Amount) and items (ItemBundle).</summary>
            Withdraw,

            /// <summary>Server → client: status reply (Note carries human-readable text).</summary>
            Result
        }

        public StepMode CurrentStep { get; set; } = StepMode.Request;

        /// <summary>Treasury identifier — guild name or "_personal:&lt;username&gt;".</summary>
        public string OwnerKey { get; set; } = string.Empty;

        public bool IsGuildOwned { get; set; }

        public int SilverAmount { get; set; }

        /// <summary>
        /// Item bundle for deposit/withdraw and snapshot replies.
        /// For client requests, the keys must already exist in the caravan/treasury.
        /// </summary>
        public Dictionary<string, int> ItemBundle { get; set; } = new Dictionary<string, int>();

        /// <summary>Lifetime in/out for snapshot replies.</summary>
        public long LifetimeSilverIn { get; set; }
        public long LifetimeSilverOut { get; set; }

        /// <summary>Most recent transactions (snapshot replies only).</summary>
        public List<TreasuryTransaction> RecentTransactions { get; set; } = new List<TreasuryTransaction>();

        /// <summary>Free-form text — used for Result step's status message.</summary>
        public string Note { get; set; } = string.Empty;

        /// <summary>Server fills this so client UIs can render a permissions banner.</summary>
        public bool CanDeposit { get; set; }
        public bool CanWithdraw { get; set; }
    }
}
