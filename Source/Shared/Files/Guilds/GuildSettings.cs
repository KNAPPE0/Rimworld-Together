namespace Shared.Files.Guilds
{
    /// <summary>
    /// Per-guild policy: tax rates, withdrawal caps, MOTD shown to members on
    /// connect. Mutable by Admins via the Guild Hall dialog.
    /// </summary>
    public class GuildSettings
    {
        /// <summary>Message of the day — broadcast to members on login.</summary>
        public string MessageOfTheDay { get; set; } = string.Empty;

        /// <summary>0–50: % of every member's site silver reward routed to guild treasury.</summary>
        public int SiteRewardSilverTaxPercent { get; set; } = 5;

        /// <summary>0–50: % of every member's marketplace sale silver routed to guild treasury.</summary>
        public int MarketplaceSaleTaxPercent { get; set; } = 5;

        /// <summary>Daily silver withdraw cap for Members. 0 = no withdraw allowed.</summary>
        public int MemberDailyWithdrawCap { get; set; } = 0;

        /// <summary>Daily silver withdraw cap for Officers. -1 = unlimited.</summary>
        public int OfficerDailyWithdrawCap { get; set; } = 5_000;

        /// <summary>Daily silver withdraw cap for Moderators. -1 = unlimited.</summary>
        public int ModeratorDailyWithdrawCap { get; set; } = 50_000;

        /// <summary>Daily silver withdraw cap for Admins. -1 = unlimited.</summary>
        public int AdminDailyWithdrawCap { get; set; } = -1;

        /// <summary>If true, marketplace listings posted by guild members default to "guild only" (members + allies).</summary>
        public bool DefaultListingsGuildOnly { get; set; } = false;
    }
}
