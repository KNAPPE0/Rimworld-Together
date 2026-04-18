using GameClient.Dialogs;
using GameClient.Dialogs.Default;
using GameClient.Misc;
using TCPNetwork.Packets;
using Shared;
using Shared.Files;
using System;

namespace GameClient.PacketManagers
{
    public static class PM_Statistical
    {
        private const int RequestTimeoutMs = 10000;

        private static bool _statsRequestInFlight;
        private static int _lastStatsRequestMs;

        public static void AskForStats()
        {
            int now = Environment.TickCount;

            if (_statsRequestInFlight && now - _lastStatsRequestMs < RequestTimeoutMs)
                return;

            _statsRequestInFlight = true;
            _lastStatsRequestMs = now;

            DLG_Base.PushNewDialog(new DLG_Wait());

            PKT_Information data = new PKT_Information();
            data._stepMode = PKT_Information.InfoStepMode.Stats;
            data._settlementTile = SessionHandler.ChosenSettlement.Tile;

            TCPNetwork.Network.ServerEndpoint.EnqueuePacket(PacketHeader.InformationManager, data);
        }

        public static void ReceiveStats(PKT_Information data)
        {
            _statsRequestInFlight = false;

            if (DLG_Wait.Instance != null) DLG_Wait.Instance.Close();

            MapStatsFile stats = data._settlementStats;

            if (stats == null)
            {
                DLG_Base.PushNewDialog(new DLG_Message("Colony Stats", new string[]
                {
                    "No stats were found for this settlement (has it been saved yet?)."
                }));
                return;
            }

            string settlementName = "Unknown";
            try
            {
                if (SessionHandler.ChosenSettlement != null)
                    settlementName = SessionHandler.ChosenSettlement.Label;
            }
            catch { }

            DLG_Base.PushNewDialog(new DLG_ColonyStats(stats, data._isPlayerOnline, settlementName));
        }

        [OnUpdate]
        private static void UpdateTimeout()
        {
            if (!_statsRequestInFlight) return;

            int now = Environment.TickCount;
            if (now - _lastStatsRequestMs < RequestTimeoutMs) return;

            _statsRequestInFlight = false;

            if (DLG_Wait.Instance != null) DLG_Wait.Instance.Close();

            DLG_Base.PushNewDialog(new DLG_Message("Colony Stats", new string[]
            {
                "The server did not respond in time.",
                "Try again in a moment (or the settlement may not have been saved yet)."
            }));
        }
    }
}
