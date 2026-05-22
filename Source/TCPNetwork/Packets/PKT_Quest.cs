using Shared.Files.Economy;
using System.Collections.Generic;

namespace TCPNetwork.Packets
{
    /// <summary>
    /// Single envelope for all quest-board traffic.
    /// </summary>
    public class PKT_Quest : PKT_Base
    {
        public enum StepMode
        {
            /// <summary>Client → server: send me the current quest board.</summary>
            RequestBoard,

            /// <summary>Server → client: snapshot of all open + recently-completed quests.</summary>
            BoardSnapshot,

            /// <summary>Client → server: post a new quest (Quest filled in).</summary>
            Post,

            /// <summary>Client → server: claim Quest.Id. Reserves it for you.</summary>
            Claim,

            /// <summary>Client → server: abandon a claimed quest. Returns to Open.</summary>
            Abandon,

            /// <summary>Client → server: submit delivery items for a DeliverItem quest. Server verifies + completes.</summary>
            SubmitDelivery,

            /// <summary>Client → server: poster confirms a Bounty quest done. Pays out.</summary>
            ConfirmCompletion,

            /// <summary>Client → server: poster cancels their open quest. Refunds escrow.</summary>
            Cancel,

            /// <summary>Server → client: status reply (Note carries human-readable text).</summary>
            Result
        }

        public StepMode CurrentStep { get; set; } = StepMode.RequestBoard;

        public long QuestId { get; set; }

        /// <summary>For Post: the new quest's content (Id is server-assigned).</summary>
        public QuestFile Quest { get; set; }

        /// <summary>For BoardSnapshot.</summary>
        public List<QuestFile> Board { get; set; } = new List<QuestFile>();
        public long LifetimeQuestsPosted { get; set; }
        public long LifetimeQuestsCompleted { get; set; }
        public long LifetimeBountySilverPaid { get; set; }

        /// <summary>Free-form note for Result step.</summary>
        public string Note { get; set; } = string.Empty;
    }
}
