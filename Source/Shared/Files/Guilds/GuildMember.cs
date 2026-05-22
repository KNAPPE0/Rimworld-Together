namespace Shared.Files.Guilds
{
    public class GuildMember
    {
        // KMH: Officer added between Moderator and Admin so we have a path
        // for "trusted but not in charge" without granting full admin powers.
        public enum GuildRanks { Member, Moderator, Officer, Admin }

        public string Username { get; set; } = string.Empty;

        public GuildRanks Rank { get; set; } = GuildRanks.Member;

        // -- KMH: contribution + activity tracking --

        /// <summary>Total silver this member has put into the guild treasury (manual deposits + tax).</summary>
        public long SilverContributed { get; set; }

        /// <summary>Total individual item units this member has contributed via deposit/tax.</summary>
        public long ItemsContributed { get; set; }

        /// <summary>Quest count this member finished while in the guild.</summary>
        public int QuestsCompleted { get; set; }

        /// <summary>UTC ticks at which this member joined the guild.</summary>
        public long JoinedUtcTicks { get; set; }

        /// <summary>Daily silver withdraw cap tracking — resets when the UTC day changes.</summary>
        public long DailyWithdrawnSilver { get; set; }

        /// <summary>UTC day-of-year that DailyWithdrawnSilver corresponds to (year * 1000 + dayOfYear).</summary>
        public int DailyWithdrawDayKey { get; set; }
    }
}
