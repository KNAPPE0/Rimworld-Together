using GameClient.Dialogs;
using Shared;
using Shared.Files;
using System;
using System.Globalization;
using System.Linq;
using TCPNetwork.Packets;
using static Shared.CommonEnumerators;

namespace GameClient.Managers
{
    public static class LeaderboardManager
    {

        public static void AskForLeaderboard()
        {
            AskForLeaderboard(
                InformationData.LeaderboardSortMode.WealthExact,
                InformationData.LeaderboardOrder.Desc,
                10,
                0
            );
        }

        public static void AskForLeaderboard(
            InformationData.LeaderboardSortMode sort,
            InformationData.LeaderboardOrder order,
            int limit = 10,
            int offset = 0)
        {
            RequestLeaderboard(sort, order, limit, offset);
        }

        public static void AskForLeaderboard(
            InformationData.LeaderboardSortMode sort,
            int limit,
            InformationData.LeaderboardOrder order,
            int offset = 0)
        {
            RequestLeaderboard(sort, order, limit, offset);
        }

        public static void AskForLeaderboard(
            InformationData.LeaderboardSortMode sort,
            int limit,
            int offset,
            InformationData.LeaderboardOrder order)
        {
            RequestLeaderboard(sort, order, limit, offset);
        }

        public static void AskForLeaderboard(
            InformationData.LeaderboardSortMode sort,
            int limit,
            int offset)
        {
            RequestLeaderboard(sort, InformationData.LeaderboardOrder.Desc, limit, offset);
        }

        public static void RequestLeaderboard(
            InformationData.LeaderboardSortMode sort,
            InformationData.LeaderboardOrder order,
            int limit = 10,
            int offset = 0)
        {
            RT_Dialog_Base.PushNewDialog(new RT_Dialog_Wait("Waiting for server"));

            InformationData data = new InformationData();
            data._stepMode = InformationData.InfoStepMode.Leaderboard;
            data._leaderboardSort = sort;
            data._leaderboardOrder = order;
            data._leaderboardLimit = limit;
            data._leaderboardOffset = offset;

            ClientNetwork.Instance.ClientListener.EnqueuePacket(PacketHeader.InformationManager, data);
        }

        public static void ReceiveLeaderboard(InformationData data)
        {
            RT_Dialog_Wait.Instance?.Close();

            LeaderboardEntryFile[] entries = data._leaderboardEntries ?? new LeaderboardEntryFile[0];

            ChatManager.AddMessageToChat(
                "Server",
                $"Leaderboard: {data._leaderboardSort} ({data._leaderboardOrder}) — showing {entries.Length} / {data._leaderboardTotal}",
                ChatColor.Server,
                ChatColor.Server);

            if (entries.Length == 0)
            {
                ChatManager.AddMessageToChat(
                    "Server",
                    "No leaderboard entries found yet (are .stats files being written?).",
                    ChatColor.Server,
                    ChatColor.Server);
                return;
            }

            foreach (LeaderboardEntryFile e in entries)
            {
                string who = string.IsNullOrWhiteSpace(e.Username) ? "Unknown" : e.Username;
                string community = string.IsNullOrWhiteSpace(e.SettlementName) ? "Unknown" : e.SettlementName;
                string faction = string.IsNullOrWhiteSpace(e.FactionName) ? "Unknown" : e.FactionName;

                string wealth = FormatWealth(e);
                string colonists = e.ColonistCount < 0 ? "?" : e.ColonistCount.ToString("N0", CultureInfo.InvariantCulture);

                ChatManager.AddMessageToChat(
                    "Server",
                    $"#{e.Rank} {who} | Community: {community} | Faction: {faction} | Wealth: {wealth} | Colonists: {colonists}",
                    ChatColor.Server,
                    ChatColor.Server);
            }
        }

        public static bool TryHandleChatCommand(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return false;

            string raw = message.Trim();
            if (!raw.StartsWith("/lb", StringComparison.OrdinalIgnoreCase) &&
                !raw.StartsWith("/leaderboard", StringComparison.OrdinalIgnoreCase))
                return false;

            string[] parts = raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            InformationData.LeaderboardSortMode sort = InformationData.LeaderboardSortMode.WealthExact;
            InformationData.LeaderboardOrder order = InformationData.LeaderboardOrder.Desc;
            int limit = 10;

            foreach (string p in parts.Skip(1))
            {
                if (int.TryParse(p, out int maybeLimit))
                {
                    limit = maybeLimit;
                    continue;
                }

                if (p.Equals("asc", StringComparison.OrdinalIgnoreCase)) { order = InformationData.LeaderboardOrder.Asc; continue; }
                if (p.Equals("desc", StringComparison.OrdinalIgnoreCase)) { order = InformationData.LeaderboardOrder.Desc; continue; }

                if (EnumTryParseSort(p, out InformationData.LeaderboardSortMode parsedSort))
                    sort = parsedSort;
            }

            AskForLeaderboard(sort, order, limit, 0);
            return true;
        }

        private static bool EnumTryParseSort(string token, out InformationData.LeaderboardSortMode mode)
        {
            mode = InformationData.LeaderboardSortMode.WealthExact;

            if (string.IsNullOrWhiteSpace(token)) return false;

            string t = token.Trim().ToLowerInvariant();

            switch (t)
            {
                case "wealth":
                    mode = InformationData.LeaderboardSortMode.Wealth; return true;

                case "wealthexact":
                case "exactwealth":
                case "wealth_exact":
                    mode = InformationData.LeaderboardSortMode.WealthExact; return true;

                case "colonists":
                case "colonist":
                    mode = InformationData.LeaderboardSortMode.Colonists; return true;

                case "ticks":
                case "playtimeticks":
                    mode = InformationData.LeaderboardSortMode.PlaytimeTicks; return true;

                case "days":
                    mode = InformationData.LeaderboardSortMode.Days; return true;

                case "settlement":
                case "settlementname":
                case "community":
                case "communityname":
                    mode = InformationData.LeaderboardSortMode.SettlementName; return true;

                case "faction":
                case "factionname":
                    mode = InformationData.LeaderboardSortMode.FactionName; return true;

                case "lastsaved":
                case "lastsavedutcticks":
                    mode = InformationData.LeaderboardSortMode.LastSavedUtcTicks; return true;
            }

            return false;
        }

        private static string FormatWealth(LeaderboardEntryFile e)
        {
            if (e.WealthExact >= 0) return "$" + e.WealthExact.ToString("N2", CultureInfo.InvariantCulture);
            if (e.Wealth >= 0) return "$" + e.Wealth.ToString("N0", CultureInfo.InvariantCulture);
            return "Unknown";
        }
    }
}