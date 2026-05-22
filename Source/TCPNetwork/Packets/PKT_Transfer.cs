using System.Collections.Generic;

namespace TCPNetwork.Packets
{
    public class PKT_Transfer : PKT_Base
    {
        // Pod ported from upstream (May 2026 — "Fixed drop pod trading").
        // Recipient side treats Pod like Gift (auto-accept, materialise
        // into recipient's map). Appended at the end so existing
        // serialised TransferMode values keep their numeric positions.
        public enum TransferMode { Gift, Trade, Rebound, Pod }

        public enum TransferLocation { Caravan, Settlement, Pod }

        public enum TransferStepMode { TradeRequest, TradeAccept, TradeReject, TradeReRequest, TradeReAccept, TradeReReject, Recover, Pod }

        public TransferStepMode CurrentStepMode { get; set; } = TransferStepMode.TradeRequest;

        public TransferMode CurrentTransferMode { get; set; } = TransferMode.Gift;

        public bool IsDropPod { get; set; } = false;

        public int FromTile { get; set; } = int.MaxValue;

        public int ToTile { get; set; } = int.MaxValue;

        public List<string> Pawns { get; set; } = new List<string>();

        public List<string> Things { get; set; } = new List<string>();
    }
}
