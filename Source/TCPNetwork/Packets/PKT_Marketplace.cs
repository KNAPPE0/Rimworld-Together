using Shared.Files.Economy;
using System.Collections.Generic;

namespace TCPNetwork.Packets
{
    /// <summary>
    /// All marketplace-related client↔server traffic uses this envelope.
    /// </summary>
    public class PKT_Marketplace : PKT_Base
    {
        public enum StepMode
        {
            /// <summary>Client → server: send me the current open listings.</summary>
            RequestListings,

            /// <summary>Server → client: snapshot of all listings + house pool / lifetime stats.</summary>
            ListingsSnapshot,

            /// <summary>Client → server: create a listing for ItemDefName×Quantity at UnitPriceSilver.
            /// Server pulls the items from caravan or seller treasury (UseTreasury flag).</summary>
            CreateListing,

            /// <summary>Client → server: buy ListingId × Quantity. Silver pulled from caravan,
            /// items delivered to caravan (or buyer treasury if BuyerWantsTreasuryDelivery).</summary>
            Buy,

            /// <summary>Client → server: cancel ListingId — only the seller may do this.
            /// Unsold stock returns to seller treasury.</summary>
            Cancel,

            /// <summary>Server → client: status reply (Note carries human-readable text).</summary>
            Result
        }

        public StepMode CurrentStep { get; set; } = StepMode.RequestListings;

        public string ItemDefName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int UnitPriceSilver { get; set; }

        /// <summary>For CreateListing: pull stock from the seller's treasury rather than caravan.</summary>
        public bool UseTreasury { get; set; }

        /// <summary>For Buy: deliver bought items to buyer's treasury rather than caravan.</summary>
        public bool BuyerWantsTreasuryDelivery { get; set; }

        public long ListingId { get; set; }

        /// <summary>For ListingsSnapshot only.</summary>
        public List<MarketplaceListing> Listings { get; set; } = new List<MarketplaceListing>();
        public long HouseSilverPool { get; set; }
        public long LifetimeTradesCompleted { get; set; }
        public long LifetimeSilverTraded { get; set; }

        public string Note { get; set; } = string.Empty;

        /// <summary>
        /// KMH 2.7: Discriminates what kind of action a Result packet refers
        /// to. Without this, the client mis-treated CreateListing replies as
        /// Buy replies and spawned the listed items at the seller's home — i.e.
        /// the "I listed items but they came back" bug.
        /// </summary>
        public enum ResultKindCode
        {
            Unspecified = 0,
            BuyResult = 1,
            ListResult = 2,
            CancelResult = 3
        }

        public ResultKindCode ResultKind { get; set; } = ResultKindCode.Unspecified;

        // KMH 2.7: Quality + stuff so the marketplace honours RimWorld's
        // crafting variants. Optional — vanilla resources leave both blank.

        /// <summary>QualityCategory cast to int (1=Awful…6=Legendary). 0 = "no quality".</summary>
        public int QualityIndex { get; set; }

        /// <summary>Stuff defName for stuffable items (e.g. "Steel" for a steel knife). Empty for non-stuffable.</summary>
        public string StuffDefName { get; set; } = string.Empty;
    }
}
