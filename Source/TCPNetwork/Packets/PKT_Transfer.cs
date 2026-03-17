using Shared.Files;
using System.Collections.Generic;
using static Shared.CommonEnumerators;

namespace TCPNetwork.Packets
{
    public class PKT_Transfer : PKT_Base
    {
        public enum TransferMode { Gift, Trade, Rebound, Pod }

        public enum TransferLocation { Caravan, Settlement, Pod }

        public enum TransferStepMode { TradeRequest, TradeAccept, TradeReject, TradeReRequest, TradeReAccept, TradeReReject, Recover, Pod }

        public TransferStepMode _stepMode { get; set; } = TransferStepMode.TradeRequest;

        public TransferMode _transferMode { get; set; } = TransferMode.Gift;

        public int _fromTile { get; set; } = -1;

        public int _toTile { get; set; } = -1;

        public List<string> _pawns { get; set; } = new List<string>();

        public List<string> _things { get; set; } = new List<string>();
    }
}
