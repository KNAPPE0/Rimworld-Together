using GameClient.Dialogs;
using TCPNetwork.Packets;
using Shared;
using Shared.Files;
using System;
using GameClient.Misc;

namespace GameClient.Managers
{
    public static class StatisticalManager
    {
        private const int RequestTimeoutMs = 10000;

        private static bool _statsRequestInFlight;
        private static int _lastStatsRequestMs;

        public static void AskForStats()
        {
            int now = Environment.TickCount;

            if (_statsRequestInFlight && (now - _lastStatsRequestMs) < RequestTimeoutMs)
                return;

            _statsRequestInFlight = true;
            _lastStatsRequestMs = now;

            RT_Dialog_Base.PushNewDialog(new RT_Dialog_Wait("Waiting for server"));

            InformationData data = new InformationData();
            data._stepMode = InformationData.InfoStepMode.Stats;
            data._settlementTile = SessionHandler.ChosenSettlement.Tile;

            ClientNetwork.Instance.ClientListener.EnqueuePacket(PacketHeader.InformationManager, data);
        }

        public static void ReceiveStats(InformationData data)
        {
            _statsRequestInFlight = false;

            if (RT_Dialog_Wait.Instance != null) RT_Dialog_Wait.Instance.Close();

            MapStatsFile stats = data._settlementStats;

            if (stats == null)
            {
                RT_Dialog_Base.PushNewDialog(new RT_Dialog_Message("Colony Stats", new string[]
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

            RT_Dialog_Base.PushNewDialog(new RT_Dialog_ColonyStats(stats, data._isPlayerOnline, settlementName));
        }

        [OnUpdate]
        private static void UpdateTimeout()
        {
            if (!_statsRequestInFlight) return;

            int now = Environment.TickCount;
            if ((now - _lastStatsRequestMs) < RequestTimeoutMs) return;

            _statsRequestInFlight = false;

            if (RT_Dialog_Wait.Instance != null) RT_Dialog_Wait.Instance.Close();

            RT_Dialog_Base.PushNewDialog(new RT_Dialog_Message("Colony Stats", new string[]
            {
                "The server did not respond in time.",
                "Try again in a moment (or the settlement may not have been saved yet)."
            }));
        }
    }
}