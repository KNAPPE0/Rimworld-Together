using GameServer.PacketManager;
using Shared.Files.Economy;
using Shared.Files.Guilds;
using Shared.Misc;
using System;
using System.Collections.Generic;
using TCPNetwork.Files.Client;

namespace GameServer.Managers
{
    /// <summary>
    /// Inter-guild diplomacy + treasury bonus dispenser. Lives on the same
    /// static class as <see cref="GuildManager"/>; split into its own file
    /// so the core file stays readable.
    /// </summary>
    public static partial class GuildManager
    {
        // -- alliances --

        /// <summary>
        /// Propose an alliance from <paramref name="byUsername"/>'s guild to <paramref name="otherGuild"/>.
        /// If the other side has already proposed to us, this completes the handshake into <see cref="AllianceRelation.Allied"/>.
        /// </summary>
        public static (bool ok, string note) ProposeAlliance(string byUsername, string otherGuild)
        {
            UserFile uf = UserManagerH.GetUserFileFromName(byUsername);
            string ourGuild = uf?.GuildName;
            if (string.IsNullOrEmpty(ourGuild)) return (false, "You're not in a guild.");
            if (string.Equals(ourGuild, otherGuild, StringComparison.OrdinalIgnoreCase)) return (false, "Can't ally yourself.");

            GuildMember.GuildRanks? rank = TreasuryManager.ResolveRankInGuild(byUsername, ourGuild);
            if (rank == null || rank.Value != GuildMember.GuildRanks.Admin)
                return (false, "Admin required to manage alliances.");

            GuildFile ours = GuildManagerH.GetFactionFromName(ourGuild);
            GuildFile theirs = GuildManagerH.GetFactionFromName(otherGuild);
            if (ours == null || theirs == null) return (false, "Guild not found.");

            EnsureRelationshipsDict(ours);
            EnsureRelationshipsDict(theirs);

            ours.Relationships.TryGetValue(otherGuild, out AllianceRelation oursToTheirs);
            theirs.Relationships.TryGetValue(ourGuild, out AllianceRelation theirsToOurs);

            // If the other side already proposed to us, complete the alliance.
            if (theirsToOurs == AllianceRelation.AlliedRequested)
            {
                ours.Relationships[otherGuild] = AllianceRelation.Allied;
                theirs.Relationships[ourGuild] = AllianceRelation.Allied;
                ours.Persist();
                theirs.Persist();
                PM_GuildHall.BroadcastSnapshotToGuild(ourGuild);
                PM_GuildHall.BroadcastSnapshotToGuild(otherGuild);
                try { Integrations.Discord.DiscordAnnouncer.AllianceFormed(ourGuild, otherGuild); } catch { }
                return (true, $"Alliance with {otherGuild} accepted!");
            }

            // Otherwise mark our side as having proposed.
            ours.Relationships[otherGuild] = AllianceRelation.AlliedRequested;
            ours.Persist();
            PM_GuildHall.BroadcastSnapshotToGuild(ourGuild);
            try { Integrations.Discord.DiscordAnnouncer.AllianceProposed(ourGuild, otherGuild); } catch { }
            return (true, $"Alliance proposed to {otherGuild}. Awaiting their reply.");
        }

        /// <summary>
        /// Sever any alliance / proposal in either direction.
        /// </summary>
        public static (bool ok, string note) BreakAlliance(string byUsername, string otherGuild)
        {
            UserFile uf = UserManagerH.GetUserFileFromName(byUsername);
            string ourGuild = uf?.GuildName;
            if (string.IsNullOrEmpty(ourGuild)) return (false, "You're not in a guild.");

            GuildMember.GuildRanks? rank = TreasuryManager.ResolveRankInGuild(byUsername, ourGuild);
            if (rank == null || rank.Value != GuildMember.GuildRanks.Admin)
                return (false, "Admin required to manage alliances.");

            GuildFile ours = GuildManagerH.GetFactionFromName(ourGuild);
            GuildFile theirs = GuildManagerH.GetFactionFromName(otherGuild);
            if (ours == null) return (false, "Guild not found.");

            EnsureRelationshipsDict(ours);
            bool wasAllied = ours.Relationships.TryGetValue(otherGuild, out AllianceRelation oldRel) && oldRel == AllianceRelation.Allied;
            ours.Relationships.Remove(otherGuild);
            ours.Persist();
            PM_GuildHall.BroadcastSnapshotToGuild(ourGuild);

            if (theirs != null)
            {
                EnsureRelationshipsDict(theirs);
                theirs.Relationships.Remove(ourGuild);
                theirs.Persist();
                PM_GuildHall.BroadcastSnapshotToGuild(otherGuild);
            }

            if (wasAllied)
                try { Integrations.Discord.DiscordAnnouncer.AllianceBroken(ourGuild, otherGuild); } catch { }

            return (true, $"Alliance with {otherGuild} broken.");
        }

        public static (bool ok, string note) DeclareHostile(string byUsername, string otherGuild)
        {
            UserFile uf = UserManagerH.GetUserFileFromName(byUsername);
            string ourGuild = uf?.GuildName;
            if (string.IsNullOrEmpty(ourGuild)) return (false, "You're not in a guild.");

            GuildMember.GuildRanks? rank = TreasuryManager.ResolveRankInGuild(byUsername, ourGuild);
            if (rank == null || rank.Value != GuildMember.GuildRanks.Admin)
                return (false, "Admin required.");

            GuildFile ours = GuildManagerH.GetFactionFromName(ourGuild);
            if (ours == null) return (false, "Guild not found.");

            EnsureRelationshipsDict(ours);
            ours.Relationships[otherGuild] = AllianceRelation.Hostile;
            ours.Persist();
            PM_GuildHall.BroadcastSnapshotToGuild(ourGuild);
            try { Integrations.Discord.DiscordAnnouncer.HostilityDeclared(ourGuild, otherGuild); } catch { }
            return (true, $"Declared hostile against {otherGuild}.");
        }

        public static bool AreAllied(string guildA, string guildB)
        {
            if (string.IsNullOrEmpty(guildA) || string.IsNullOrEmpty(guildB)) return false;
            if (string.Equals(guildA, guildB, StringComparison.OrdinalIgnoreCase)) return true;
            GuildFile a = GuildManagerH.GetFactionFromName(guildA);
            if (a?.Relationships == null) return false;
            return a.Relationships.TryGetValue(guildB, out AllianceRelation r) && r == AllianceRelation.Allied;
        }

        public static IEnumerable<string> GetAlliedGuildNames(string guildName)
        {
            GuildFile g = GuildManagerH.GetFactionFromName(guildName);
            if (g?.Relationships == null) yield break;
            foreach (var kv in g.Relationships)
                if (kv.Value == AllianceRelation.Allied) yield return kv.Key;
        }

        private static void EnsureRelationshipsDict(GuildFile g)
        {
            if (g.Relationships == null)
                g.Relationships = new Dictionary<string, AllianceRelation>(StringComparer.OrdinalIgnoreCase);
        }

        // -- bonus dispenser --

        /// <summary>
        /// Withdraws <paramref name="totalSilver"/> from the guild treasury and
        /// distributes equal shares into each member's personal vault.
        /// Admin-only. Any leftover from integer division stays in the treasury.
        /// </summary>
        public static (bool ok, string note) DistributeSilverBonus(string byUsername, int totalSilver)
        {
            UserFile uf = UserManagerH.GetUserFileFromName(byUsername);
            string guildName = uf?.GuildName;
            if (string.IsNullOrEmpty(guildName)) return (false, "You're not in a guild.");

            GuildMember.GuildRanks? rank = TreasuryManager.ResolveRankInGuild(byUsername, guildName);
            if (rank == null || rank.Value != GuildMember.GuildRanks.Admin)
                return (false, "Admin required to dispense bonuses.");

            GuildFile g = GuildManagerH.GetFactionFromName(guildName);
            if (g == null) return (false, "Guild not found.");
            int memberCount = g.GuildMembers?.Count ?? 0;
            if (memberCount <= 0) return (false, "Guild has no members.");

            int perMember = totalSilver / memberCount;
            if (perMember <= 0) return (false, "Bonus too small to split (per-member share would be 0).");

            int actualSpent = perMember * memberCount;

            TreasuryFile t = TreasuryManager.GetOrCreate(guildName, isGuildOwned: true);
            if (!TreasuryManager.TryWithdrawSilver(t, byUsername, actualSpent,
                    TreasuryTransaction.TxKind.Withdraw, $"bonus-dispense:{perMember}/m"))
                return (false, $"Treasury short on silver ({actualSpent} required).");

            foreach (GuildMember m in g.GuildMembers)
            {
                TreasuryManager.DepositSilverForUser(m.Username, perMember,
                    TreasuryTransaction.TxKind.Deposit, $"guild-bonus:{guildName}");
            }

            try { Integrations.Discord.DiscordAnnouncer.GuildBonusDispensed(guildName, byUsername, perMember, memberCount, actualSpent); }
            catch { }

            return (true, $"Dispensed {perMember} silver to each of {memberCount} members ({actualSpent} total).");
        }

        // -- leaderboard data --

        public class GuildSummary
        {
            public string Name { get; set; }
            public int MemberCount { get; set; }
            public int TreasurySilver { get; set; }
            public long LifetimeSilverIn { get; set; }
            public int TotalPerkLevels { get; set; }
            public long QuestsCompletedByMembers { get; set; }
            public long SilverContributedByMembers { get; set; }
            // KMH 2.7: deeper metrics
            public int TotalSites { get; set; }
            public int AlliesCount { get; set; }
            public int HostilesCount { get; set; }
            public double AvgMemberTenureDays { get; set; }
            public long TotalMemberWorkerXp { get; set; }
            public long TotalMemberSilverEarned { get; set; }
            public int TotalMemberSitesBuilt { get; set; }
        }

        public static List<GuildSummary> ComputeLeaderboard()
        {
            List<GuildSummary> result = new List<GuildSummary>();
            try
            {
                // KMH 2.7: Pre-count sites per guild once so we don't re-scan
                // the sites directory inside the per-guild loop.
                // KMH 26.5.20.1: Read through GuildManagerH cache instead of
                // re-scanning the guilds directory + re-deserialising every
                // file on every leaderboard refresh.
                Dictionary<string, int> sitesPerGuild = CountSitesPerGuild();

                GuildFile[] cachedFactions = GuildManagerH.GetAllFactions();
                foreach (GuildFile g in cachedFactions)
                {
                    if (g == null || string.IsNullOrEmpty(g.Name)) continue;

                    TreasuryFile t = TreasuryManager.GetOrCreate(g.Name, isGuildOwned: true);

                    long silverContrib = 0;
                    long questsDone = 0;
                    long memberXp = 0;
                    long memberSilverEarned = 0;
                    int memberSitesBuilt = 0;
                    double tenureDaysSum = 0;
                    int tenureCount = 0;

                    if (g.GuildMembers != null)
                    {
                        foreach (GuildMember m in g.GuildMembers)
                        {
                            silverContrib += m.SilverContributed;
                            questsDone += m.QuestsCompleted;

                            if (m.JoinedUtcTicks > 0)
                            {
                                try
                                {
                                    double days = (DateTime.UtcNow - new DateTime(m.JoinedUtcTicks, DateTimeKind.Utc)).TotalDays;
                                    if (days > 0) { tenureDaysSum += days; tenureCount++; }
                                }
                                catch { }
                            }

                            // Pull individual lifetime player stats too (for "deep" guild numbers).
                            UserFile uf = UserManagerH.GetUserFileFromName(m.Username);
                            if (uf != null)
                            {
                                memberXp += uf.LifetimeWorkerXpEarned;
                                memberSilverEarned += uf.LifetimeSilverEarnedFromSales;
                                memberSitesBuilt += uf.LifetimeSitesBuilt;
                            }
                        }
                    }

                    int totalPerkLevels = (g.Perks?.SiteMaxWorkersBonusLevel ?? 0)
                        + (g.Perks?.MarketplaceTaxReductionLevel ?? 0)
                        + (g.Perks?.WorkerXpBonusLevel ?? 0)
                        + (g.Perks?.CustomSiteCostDiscountLevel ?? 0);

                    int allies = 0, hostiles = 0;
                    if (g.Relationships != null)
                    {
                        foreach (var kv in g.Relationships)
                        {
                            if (kv.Value == AllianceRelation.Allied) allies++;
                            else if (kv.Value == AllianceRelation.Hostile) hostiles++;
                        }
                    }

                    sitesPerGuild.TryGetValue(g.Name, out int siteCount);

                    result.Add(new GuildSummary
                    {
                        Name = g.Name,
                        MemberCount = g.GuildMembers?.Count ?? 0,
                        TreasurySilver = t?.SilverBalance ?? 0,
                        LifetimeSilverIn = t?.LifetimeSilverIn ?? 0,
                        TotalPerkLevels = totalPerkLevels,
                        QuestsCompletedByMembers = questsDone,
                        SilverContributedByMembers = silverContrib,
                        TotalSites = siteCount,
                        AlliesCount = allies,
                        HostilesCount = hostiles,
                        AvgMemberTenureDays = tenureCount > 0 ? tenureDaysSum / tenureCount : 0.0,
                        TotalMemberWorkerXp = memberXp,
                        TotalMemberSilverEarned = memberSilverEarned,
                        TotalMemberSitesBuilt = memberSitesBuilt
                    });
                }
            }
            catch (Exception e) { Printer.Warning($"[Guild] Leaderboard build failed: {e}"); }
            return result;
        }

        private static Dictionary<string, int> CountSitesPerGuild()
        {
            // KMH 26.5.20.1: Use the SiteManagerHelper cache instead of
            // re-scanning + re-deserialising every site file on every
            // leaderboard rebuild. The leaderboard refreshes frequently;
            // every tick saved here multiplies across all the connected
            // clients getting their snapshot.
            Dictionary<string, int> map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            try
            {
                Shared.Files.Sites.SiteFile[] all = GameServer.PacketManager.SiteManagerHelper.GetAllSites();
                foreach (Shared.Files.Sites.SiteFile sf in all)
                {
                    if (sf == null || string.IsNullOrEmpty(sf.GuildName)) continue;
                    if (!map.TryGetValue(sf.GuildName, out int n)) n = 0;
                    map[sf.GuildName] = n + 1;
                }
            }
            catch { }
            return map;
        }
    }
}
