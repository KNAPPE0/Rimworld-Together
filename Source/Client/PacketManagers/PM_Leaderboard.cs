using GameClient.Dialogs;
using Shared;
using Shared.Files;
using System;
using System.Linq;
using TCPNetwork.Packets;
using Verse;

namespace GameClient.PacketManagers
{
    public static class PM_Leaderboard
    {
        private const int RequestTimeoutMs = 8000;

        private static bool _requestInFlight;
        private static int _lastRequestMs;

        public static InformationData.LeaderboardSortMode CurrentSort { get; private set; } = InformationData.LeaderboardSortMode.WealthExact;
        public static InformationData.LeaderboardOrder CurrentOrder { get; private set; } = InformationData.LeaderboardOrder.Desc;

        public static int CurrentLimit { get; private set; } = 10;
        public static int CurrentOffset { get; private set; } = 0;

        public static int Total { get; private set; } = -1;
        public static LeaderboardEntryFile[] Entries { get; private set; } = Array.Empty<LeaderboardEntryFile>();

        public static void OpenLeaderboardDialog(bool requestFresh = true)
        {
            TryOpenDialog();

            if (requestFresh)
                AskForLeaderboard(CurrentSort, CurrentOrder, CurrentLimit, CurrentOffset);
        }

        private static void TryOpenDialog()
        {
            try
            {
                if (Find.WindowStack == null) return;

                if (!Find.WindowStack.IsOpen<DLG_Leaderboard>())
                    Find.WindowStack.Add(new DLG_Leaderboard());
            }
            catch
            {
            }
        }

        public static bool TryHandleChatCommand(string messageToSend)
        {
            if (string.IsNullOrWhiteSpace(messageToSend))
                return false;

            string msg = messageToSend.Trim();
            if (!msg.StartsWith("/"))
                return false;

            string[] parts = msg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return false;

            string cmd = parts[0].ToLowerInvariant();
            if (cmd != "/lb" && cmd != "/leaderboard")
                return false;

            InformationData.LeaderboardSortMode sort = CurrentSort;
            InformationData.LeaderboardOrder order = CurrentOrder;
            int limit = CurrentLimit;

            for (int i = 1; i < parts.Length; i++)
            {
                string token = parts[i].ToLowerInvariant();

                if (token == "asc" || token == "a")
                {
                    order = InformationData.LeaderboardOrder.Asc;
                    continue;
                }

                if (token == "desc" || token == "d")
                {
                    order = InformationData.LeaderboardOrder.Desc;
                    continue;
                }

                if (int.TryParse(token, out int parsedLimit))
                {
                    limit = parsedLimit;
                    continue;
                }

                sort = ParseSort(token, sort);
            }

            if (limit <= 0) limit = 10;
            if (limit > 100) limit = 100;

            TryOpenDialog();
            AskForLeaderboard(sort, order, limit, 0);
            return true;
        }

        private static InformationData.LeaderboardSortMode ParseSort(string token, InformationData.LeaderboardSortMode fallback)
        {
            switch (token)
            {
                case "wealth":
                case "w":
                    return InformationData.LeaderboardSortMode.Wealth;

                case "exact":
                case "wealthexact":
                case "wx":
                    return InformationData.LeaderboardSortMode.WealthExact;

                case "colonists":
                case "cols":
                case "c":
                    return InformationData.LeaderboardSortMode.Colonists;

                case "playtime":
                case "time":
                case "pt":
                    return InformationData.LeaderboardSortMode.PlaytimeTicks;

                case "days":
                case "dys":
                    return InformationData.LeaderboardSortMode.Days;

                case "settlement":
                case "community":
                case "colony":
                case "name":
                    return InformationData.LeaderboardSortMode.SettlementName;

                case "faction":
                case "fac":
                    return InformationData.LeaderboardSortMode.FactionName;

                case "lastsaved":
                case "saved":
                case "last":
                    return InformationData.LeaderboardSortMode.LastSavedUtcTicks;

                default:
                    return fallback;
            }
        }

        public static void AskForLeaderboard(
            InformationData.LeaderboardSortMode sort,
            InformationData.LeaderboardOrder order,
            int limit,
            int offset)
        {
            int now = Environment.TickCount;

            if (_requestInFlight && (now - _lastRequestMs) < RequestTimeoutMs)
                return;

            _requestInFlight = true;
            _lastRequestMs = now;

            if (limit <= 0) limit = 10;
            if (limit > 100) limit = 100;
            if (offset < 0) offset = 0;

            CurrentSort = sort;
            CurrentOrder = order;
            CurrentLimit = limit;
            CurrentOffset = offset;

            InformationData data = new InformationData();
            data._stepMode = InformationData.InfoStepMode.Leaderboard;
            data._leaderboardSort = sort;
            data._leaderboardOrder = order;
            data._leaderboardLimit = limit;
            data._leaderboardOffset = offset;

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.InformationManager, data);
        }

        public static void ReceiveLeaderboard(InformationData data)
        {
            _requestInFlight = false;

            if (data == null)
            {
                Total = -1;
                Entries = Array.Empty<LeaderboardEntryFile>();
                return;
            }

            Total = data._leaderboardTotal;
            Entries = data._leaderboardEntries ?? Array.Empty<LeaderboardEntryFile>();

            CurrentSort = data._leaderboardSort;
            CurrentOrder = data._leaderboardOrder;
            CurrentLimit = data._leaderboardLimit;
            CurrentOffset = data._leaderboardOffset;
        }

        [OnUpdate]
        private static void UpdateTimeout()
        {
            if (!_requestInFlight) return;

            int now = Environment.TickCount;
            if ((now - _lastRequestMs) < RequestTimeoutMs) return;

            _requestInFlight = false;
        }

        public static bool CanPagePrev()
        {
            return CurrentOffset > 0;
        }

        public static bool CanPageNext()
        {
            if (Total < 0) return false;
            return (CurrentOffset + CurrentLimit) < Total;
        }

        public static void PrevPage()
        {
            int next = CurrentOffset - CurrentLimit;
            if (next < 0) next = 0;
            AskForLeaderboard(CurrentSort, CurrentOrder, CurrentLimit, next);
        }

        public static void NextPage()
        {
            int next = CurrentOffset + CurrentLimit;
            AskForLeaderboard(CurrentSort, CurrentOrder, CurrentLimit, next);
        }
    }
}