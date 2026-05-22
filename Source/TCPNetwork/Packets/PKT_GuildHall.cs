using Shared.Files.Guilds;
using System.Collections.Generic;

namespace TCPNetwork.Packets
{
    /// <summary>
    /// Guild Hall envelope: snapshot/update settings/purchase perks/set rank.
    /// Membership add/remove still flows through the legacy
    /// <see cref="PKT_PlayerGuild"/>; this packet only handles the new
    /// economy/policy surface.
    /// </summary>
    public class PKT_GuildHall : PKT_Base
    {
        public enum StepMode
        {
            RequestSnapshot,
            Snapshot,
            UpdateSettings,
            PurchasePerk,
            SetRank,
            Result,
            // KMH: diplomacy + bonus + leaderboard
            ProposeAlliance,
            BreakAlliance,
            DeclareHostile,
            DistributeBonus,
            RequestLeaderboard,
            LeaderboardSnapshot
        }

        public StepMode CurrentStep { get; set; } = StepMode.RequestSnapshot;

        /// <summary>For Snapshot replies — full guild file.</summary>
        public GuildFile Guild { get; set; }

        /// <summary>For UpdateSettings — server overlays this onto the guild's Settings.</summary>
        public GuildSettings UpdatedSettings { get; set; }

        /// <summary>For PurchasePerk — which perk to buy (cast to/from GuildManager.PerkKind).</summary>
        public int PerkKind { get; set; }

        /// <summary>For SetRank — the target member's username.</summary>
        public string TargetUsername { get; set; } = string.Empty;

        /// <summary>For SetRank — new rank as int (cast to/from GuildMember.GuildRanks).</summary>
        public int NewRank { get; set; }

        /// <summary>For Result step.</summary>
        public string Note { get; set; } = string.Empty;

        // KMH: diplomacy / bonus / leaderboard payloads.

        /// <summary>For ProposeAlliance / BreakAlliance / DeclareHostile.</summary>
        public string OtherGuildName { get; set; } = string.Empty;

        /// <summary>For DistributeBonus — total silver to split equally among all members.</summary>
        public int BonusSilverTotal { get; set; }

        /// <summary>For LeaderboardSnapshot replies.</summary>
        public List<GuildLeaderboardEntry> Leaderboard { get; set; } = new List<GuildLeaderboardEntry>();
    }

    /// <summary>One row in the global guild leaderboard snapshot.</summary>
    public class GuildLeaderboardEntry
    {
        public string Name { get; set; }
        public int MemberCount { get; set; }
        public int TreasurySilver { get; set; }
        public long LifetimeSilverIn { get; set; }
        public int TotalPerkLevels { get; set; }
        public long QuestsCompletedByMembers { get; set; }
        public long SilverContributedByMembers { get; set; }

        // KMH 2.7: deeper guild metrics — sites, alliances, hostilities,
        // tenure, total worker XP. Populated server-side in
        // GuildManager.ComputeLeaderboard.
        public int TotalSites { get; set; }
        public int AlliesCount { get; set; }
        public int HostilesCount { get; set; }
        public double AvgMemberTenureDays { get; set; }
        public long TotalMemberWorkerXp { get; set; }
        public long TotalMemberSilverEarned { get; set; }
        public int TotalMemberSitesBuilt { get; set; }
    }
}
