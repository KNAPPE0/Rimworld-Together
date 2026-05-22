using Discord;
using Shared.Files.Economy;
using Shared.Files.Guilds;
using System;
using System.Collections.Generic;

namespace GameServer.Integrations.Discord
{
    /// <summary>
    /// Centralised, opinionated Discord announcement helpers.
    ///
    /// Every notable in-game event flows through one of these methods so the
    /// formatting stays consistent. Embeds are colour-coded by category and
    /// include a thumbnail or footer where useful.
    /// </summary>
    public static class DiscordAnnouncer
    {
        // Colour palette — colours stay consistent per category.
        private static readonly Color GuildColor = new Color(112, 161, 255);   // soft blue
        private static readonly Color AllianceColor = new Color(140, 220, 140); // soft green
        private static readonly Color HostileColor = new Color(220, 80, 80);    // red
        private static readonly Color MarketColor = new Color(255, 196, 97);    // amber
        private static readonly Color QuestColor = new Color(186, 132, 246);    // purple
        private static readonly Color SiteColor = new Color(99, 199, 161);      // teal
        private static readonly Color TreasuryColor = new Color(255, 220, 100); // gold
        private static readonly Color MilestoneColor = new Color(255, 230, 0);  // bright gold

        // -- guild events --

        public static void GuildCreated(string guildName, string foundedBy)
        {
            Send(new EmbedBuilder()
                .WithTitle("🏰 New Guild Founded")
                .WithDescription($"**{guildName}** has been founded by **{foundedBy}**.")
                .WithColor(GuildColor)
                .WithFooter("Guild · created"));
        }

        public static void GuildMemberJoined(string guildName, string username)
        {
            Send(new EmbedBuilder()
                .WithTitle("➕ Member Joined")
                .WithDescription($"**{username}** joined **{guildName}**.")
                .WithColor(GuildColor));
        }

        public static void GuildMemberLeft(string guildName, string username)
        {
            Send(new EmbedBuilder()
                .WithTitle("➖ Member Departed")
                .WithDescription($"**{username}** left **{guildName}**.")
                .WithColor(GuildColor));
        }

        public static void GuildMemberRankChanged(string guildName, string username, GuildMember.GuildRanks newRank)
        {
            Send(new EmbedBuilder()
                .WithTitle("📜 Rank Change")
                .WithDescription($"**{username}** is now **{newRank}** in **{guildName}**.")
                .WithColor(GuildColor));
        }

        public static void GuildPerkPurchased(string guildName, string perkName, int newLevel, int costSilver, string byUsername)
        {
            Send(new EmbedBuilder()
                .WithTitle("⚙ Perk Upgraded")
                .WithDescription($"**{guildName}** unlocked **{perkName} L{newLevel}** for `{costSilver}s`.")
                .AddField("Purchased by", byUsername, true)
                .WithColor(GuildColor));
        }

        public static void GuildBonusDispensed(string guildName, string byUsername, int perMember, int memberCount, int total)
        {
            Send(new EmbedBuilder()
                .WithTitle("💰 Bonus Dispensed")
                .WithDescription($"**{guildName}** distributed `{perMember}s` to each of **{memberCount}** members.")
                .AddField("Total", $"{total}s", true)
                .AddField("By", byUsername, true)
                .WithColor(TreasuryColor));
        }

        // -- diplomacy --

        public static void AllianceFormed(string guildA, string guildB)
        {
            Send(new EmbedBuilder()
                .WithTitle("🤝 Alliance Formed")
                .WithDescription($"**{guildA}** ↔ **{guildB}**\n_A new alliance has been forged._")
                .WithColor(AllianceColor));
        }

        public static void AllianceProposed(string fromGuild, string toGuild)
        {
            Send(new EmbedBuilder()
                .WithTitle("📨 Alliance Proposed")
                .WithDescription($"**{fromGuild}** has proposed an alliance to **{toGuild}**.")
                .WithColor(AllianceColor));
        }

        public static void AllianceBroken(string guildA, string guildB)
        {
            Send(new EmbedBuilder()
                .WithTitle("⚔ Alliance Broken")
                .WithDescription($"**{guildA}** and **{guildB}** are no longer allied.")
                .WithColor(HostileColor));
        }

        public static void HostilityDeclared(string fromGuild, string targetGuild)
        {
            Send(new EmbedBuilder()
                .WithTitle("🔥 Hostility Declared")
                .WithDescription($"**{fromGuild}** has declared **{targetGuild}** hostile!")
                .WithColor(HostileColor));
        }

        // -- marketplace --

        // Listings/sales above this silver value get a special embed announcement.
        private const int BigDealThreshold = 5_000;

        public static void NotableListingPosted(string seller, string item, int qty, int unitPrice)
        {
            int total = unitPrice * qty;
            if (total < BigDealThreshold) return;

            string label = GameServer.Managers.ItemLabelCache.LabelFor(item);
            Send(new EmbedBuilder()
                .WithTitle("🛒 Notable Listing")
                .WithDescription($"**{seller}** listed **{qty}× {label}** at `{unitPrice}s/ea`.")
                .AddField("Asking", $"{total}s", true)
                .WithColor(MarketColor));
        }

        public static void NotableSale(string seller, string buyer, string item, int qty, int totalSilver)
        {
            if (totalSilver < BigDealThreshold) return;

            string label = GameServer.Managers.ItemLabelCache.LabelFor(item);
            Send(new EmbedBuilder()
                .WithTitle("💱 Big Sale")
                .WithDescription($"**{buyer}** bought **{qty}× {label}** from **{seller}** for `{totalSilver}s`.")
                .WithColor(MarketColor));
        }

        // -- quests --

        public static void QuestPosted(QuestFile q)
        {
            string kindEmoji = q.Kind == QuestKind.DeliverItem ? "📦" : "⚔";
            string itemLabel = GameServer.Managers.ItemLabelCache.LabelFor(q.TargetItemDefName);
            string detail = q.Kind == QuestKind.DeliverItem
                ? $"Deliver **{q.TargetItemQty}× {itemLabel}**"
                : (string.IsNullOrEmpty(q.Description) ? "_(see in-game for details)_" : q.Description);

            EmbedBuilder eb = new EmbedBuilder()
                .WithTitle($"{kindEmoji} New Quest — {q.Title}")
                .WithDescription($"_Posted by **{q.PosterUsername}**_\n\n{detail}")
                .WithColor(QuestColor);

            if (q.BountySilver > 0) eb.AddField("Bounty", $"{q.BountySilver}s", true);
            if (q.BountyItems != null && q.BountyItems.Count > 0)
                eb.AddField("Bonus items", q.BountyItems.Count + " stack(s)", true);
            eb.WithFooter($"Quest #{q.Id}");

            Send(eb);
        }

        public static void QuestCompleted(QuestFile q, string completer)
        {
            Send(new EmbedBuilder()
                .WithTitle("✅ Quest Completed")
                .WithDescription($"**{completer}** finished **{q.Title}**.")
                .AddField("Bounty paid", $"{q.BountySilver}s", true)
                .AddField("Posted by", q.PosterUsername, true)
                .WithColor(QuestColor)
                .WithFooter($"Quest #{q.Id}"));
        }

        // -- sites --

        public static void CustomSiteBuilt(string owner, string itemDefName, int qtyPerCycle, int cycleMin, int cost)
        {
            string label = GameServer.Managers.ItemLabelCache.LabelFor(itemDefName);
            Send(new EmbedBuilder()
                .WithTitle("🏗 New Custom Site")
                .WithDescription($"**{owner}** built a custom production site.")
                .AddField("Output", $"{qtyPerCycle}× {label} / cycle", true)
                .AddField("Cycle", $"{cycleMin} min", true)
                .AddField("Cost", $"{cost}s", true)
                .WithColor(SiteColor));
        }

        // -- treasury milestones --

        private static readonly long[] SilverMilestones = { 10_000, 100_000, 1_000_000 };

        public static void TreasuryMilestone(string ownerKey, bool isGuild, long lifetimeSilverIn, long previousLifetimeSilverIn)
        {
            // Detect first crossing of any milestone.
            foreach (long m in SilverMilestones)
            {
                if (previousLifetimeSilverIn < m && lifetimeSilverIn >= m)
                {
                    string label = isGuild ? $"Guild **{ownerKey}**" : "A personal vault";
                    Send(new EmbedBuilder()
                        .WithTitle("🏆 Treasury Milestone")
                        .WithDescription($"{label} has earned **{m:N0} silver lifetime**!")
                        .WithColor(MilestoneColor));
                    return;
                }
            }
        }

        // -- leaderboard --

        public static void LeaderboardSummary(IList<GameServer.Managers.GuildManager.GuildSummary> top, int topCount = 5)
        {
            Embed embed = BuildLeaderboardEmbed(top, topCount);
            if (embed == null) return;
            try { DiscordBridge.TryRelayEmbedToDiscordChat(embed); }
            catch { }
        }

        /// <summary>
        /// KMH: Pure embed builder — used by both the one-shot post path and
        /// the edit-in-place poster.
        /// </summary>
        public static Embed BuildLeaderboardEmbed(IList<GameServer.Managers.GuildManager.GuildSummary> top, int topCount = 5)
        {
            if (top == null || top.Count == 0) return null;

            EmbedBuilder eb = new EmbedBuilder()
                .WithTitle("📊 Top Guilds")
                .WithColor(MilestoneColor)
                .WithTimestamp(DateTimeOffset.UtcNow)
                .WithFooter($"Leaderboard · last updated · server {GameServer.Core.Master.ServerConfig?.Name}");

            int n = Math.Min(Math.Max(1, topCount), top.Count);
            string medal(int i) => i == 0 ? "🥇" : i == 1 ? "🥈" : i == 2 ? "🥉" : $"#{i + 1}";

            for (int i = 0; i < n; i++)
            {
                var g = top[i];
                eb.AddField($"{medal(i)} {g.Name}",
                    $"Members: **{g.MemberCount}** · Treasury: **{g.TreasurySilver}s** · Lifetime in: **{g.LifetimeSilverIn:N0}s** · Perks: **{g.TotalPerkLevels}**",
                    inline: false);
            }
            return eb.Build();
        }

        /// <summary>
        /// KMH 2.7: Combined live leaderboard — Top Guilds AND Top Players in
        /// a single embed, code-block formatted for column alignment.
        /// </summary>
        public static Embed BuildCombinedLeaderboardEmbed(
            IList<GameServer.Managers.GuildManager.GuildSummary> guilds,
            IList<GameServer.Managers.PlayerStatsManager.PlayerSummary> players,
            int topCount,
            DateTime liveStartedUtc,
            int rolloverHours,
            bool isFinalised)
        {
            EmbedBuilder eb = new EmbedBuilder()
                .WithTitle(isFinalised ? "🏆 Leaderboard (final)" : "🏆 Leaderboard (live)")
                .WithColor(isFinalised ? GuildColor : MilestoneColor)
                .WithTimestamp(DateTimeOffset.UtcNow);

            // Header line up top.
            string serverName = GameServer.Core.Master.ServerConfig?.Name ?? "Server";
            DateTime endsAt = rolloverHours > 0 ? liveStartedUtc.AddHours(rolloverHours) : DateTime.MaxValue;

            string topLine;
            if (isFinalised)
            {
                topLine = $"**End of Live Updates** · {liveStartedUtc:yyyy-MM-dd HH:mm} → {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC";
            }
            else if (rolloverHours > 0)
            {
                topLine = $"Live · started <t:{ToUnix(liveStartedUtc)}:f> · resets <t:{ToUnix(endsAt)}:R>";
            }
            else
            {
                topLine = $"Live · started <t:{ToUnix(liveStartedUtc)}:R>";
            }

            eb.WithDescription(topLine);

            // -- Guild table --
            eb.AddField("📊 Top Guilds", BuildGuildTable(guilds, topCount), inline: false);

            // -- Player table --
            eb.AddField("🎖️ Top Players", BuildPlayerTable(players, topCount), inline: false);

            string footer = isFinalised
                ? $"{serverName} · finalised at {DateTime.UtcNow:HH:mm} UTC"
                : $"{serverName} · last updated · auto-refreshes every few minutes";
            eb.WithFooter(footer);

            return eb.Build();
        }

        // KMH 2.7: Table formatting rules for Discord code blocks.
        //   * NO emojis inside the table — Discord's monospace code-block
        //     font renders 🥇/🥈/🥉 at ~2× the width of an ASCII char, which
        //     desyncs every column after the rank. Use plain " 1 " etc.
        //   * Keep total line width ≤ 62 chars. Discord wraps code blocks at
        //     a (resolution-dependent) ~65-char limit on desktop and lower
        //     on mobile, so anything wider wraps mid-row and looks broken.
        //   * Single space between columns is enough in monospace — double
        //     spaces add up fast.
        private const int MaxTableLineWidth = 62;

        // Column widths for the guild table. Keep total ≤ MaxTableLineWidth
        // counting one space separator between every column.
        private const int GuildColRank = 3;
        private const int GuildColName = 14;
        private const int GuildColMem = 3;
        private const int GuildColTreas = 7;
        private const int GuildColLife = 8;
        private const int GuildColSites = 3;
        private const int GuildColAllies = 3;

        // Column widths for the player table.
        private const int PlayerColRank = 3;
        private const int PlayerColName = 12;
        private const int PlayerColGuild = 10;
        private const int PlayerColScore = 7;
        private const int PlayerColDonated = 7;
        private const int PlayerColSales = 6;
        private const int PlayerColSites = 3;

        private static string BuildGuildTable(IList<GameServer.Managers.GuildManager.GuildSummary> guilds, int topCount)
        {
            if (guilds == null || guilds.Count == 0)
                return "*No guilds yet.*";

            List<GameServer.Managers.GuildManager.GuildSummary> sorted = new List<GameServer.Managers.GuildManager.GuildSummary>(guilds);
            sorted.Sort((a, b) => b.LifetimeSilverIn.CompareTo(a.LifetimeSilverIn));

            int n = Math.Min(Math.Max(1, topCount), sorted.Count);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("```");

            // Build the header using the same padding helpers as the data row
            // so columns align regardless of what we change later. " #" is
            // right-padded to GuildColRank (so " # " for width 3).
            sb.Append(Pad("#", GuildColRank, right: false)).Append(' ');
            sb.Append(Pad("Guild", GuildColName, right: true)).Append(' ');
            sb.Append(Pad("Mem", GuildColMem, right: false)).Append(' ');
            sb.Append(Pad("Treas", GuildColTreas, right: false)).Append(' ');
            sb.Append(Pad("Lifetime", GuildColLife, right: false)).Append(' ');
            sb.Append(Pad("Sit", GuildColSites, right: false)).Append(' ');
            sb.Append(Pad("All", GuildColAllies, right: false));
            sb.AppendLine();

            for (int i = 0; i < n; i++)
            {
                var g = sorted[i];
                sb.Append(Pad(FormatRankAscii(i), GuildColRank, right: true)).Append(' ');
                sb.Append(Pad(Truncate(g.Name ?? "?", GuildColName), GuildColName, right: true)).Append(' ');
                sb.Append(Pad(g.MemberCount.ToString(), GuildColMem, right: false)).Append(' ');
                sb.Append(Pad(ShortSilver(g.TreasurySilver), GuildColTreas, right: false)).Append(' ');
                sb.Append(Pad(ShortSilver(g.LifetimeSilverIn), GuildColLife, right: false)).Append(' ');
                sb.Append(Pad(g.TotalSites.ToString(), GuildColSites, right: false)).Append(' ');
                sb.Append(Pad(g.AlliesCount.ToString(), GuildColAllies, right: false));
                sb.AppendLine();
            }
            sb.AppendLine("```");
            return sb.ToString();
        }

        private static string BuildPlayerTable(IList<GameServer.Managers.PlayerStatsManager.PlayerSummary> players, int topCount)
        {
            if (players == null || players.Count == 0)
                return "*No players yet.*";

            List<GameServer.Managers.PlayerStatsManager.PlayerSummary> sorted = new List<GameServer.Managers.PlayerStatsManager.PlayerSummary>(players);
            sorted.Sort((a, b) => b.TotalEconomyScore.CompareTo(a.TotalEconomyScore));

            int n = Math.Min(Math.Max(1, topCount), sorted.Count);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("```");

            sb.Append(Pad("#", PlayerColRank, right: false)).Append(' ');
            sb.Append(Pad("Player", PlayerColName, right: true)).Append(' ');
            sb.Append(Pad("Guild", PlayerColGuild, right: true)).Append(' ');
            sb.Append(Pad("Score", PlayerColScore, right: false)).Append(' ');
            sb.Append(Pad("Donated", PlayerColDonated, right: false)).Append(' ');
            sb.Append(Pad("Sales", PlayerColSales, right: false)).Append(' ');
            sb.Append(Pad("Sit", PlayerColSites, right: false));
            sb.AppendLine();

            for (int i = 0; i < n; i++)
            {
                var p = sorted[i];
                sb.Append(Pad(FormatRankAscii(i), PlayerColRank, right: true)).Append(' ');
                sb.Append(Pad(Truncate(p.Username ?? "?", PlayerColName), PlayerColName, right: true)).Append(' ');
                sb.Append(Pad(Truncate(string.IsNullOrEmpty(p.GuildName) ? "-" : p.GuildName, PlayerColGuild), PlayerColGuild, right: true)).Append(' ');
                sb.Append(Pad(ShortSilver(p.TotalEconomyScore), PlayerColScore, right: false)).Append(' ');
                sb.Append(Pad(ShortSilver(p.SilverDonated), PlayerColDonated, right: false)).Append(' ');
                sb.Append(Pad(ShortSilver(p.SalesEarned), PlayerColSales, right: false)).Append(' ');
                sb.Append(Pad(p.SitesBuilt.ToString(), PlayerColSites, right: false));
                sb.AppendLine();
            }
            sb.AppendLine("```");
            return sb.ToString();
        }

        /// <summary>
        /// KMH 2.7: Width-stable pad helper. <paramref name="right"/>: when
        /// true → left-align (pad on the right side), when false → right-align
        /// (pad on the left). Truncates if longer than <paramref name="width"/>.
        /// </summary>
        private static string Pad(string s, int width, bool right)
        {
            s = s ?? string.Empty;
            if (s.Length >= width) return s.Substring(0, width);
            return right ? s.PadRight(width) : s.PadLeft(width);
        }

        /// <summary>
        /// KMH 2.7: 3-char-wide rank tag. " 1.", " 2.", " 3.", "10.", "100".
        /// Avoids medal emojis because they break monospace alignment in
        /// Discord code blocks.
        /// </summary>
        private static string FormatRankAscii(int zeroBasedRank)
        {
            int n = zeroBasedRank + 1;
            string s = n.ToString();
            if (s.Length >= 3) return s.Substring(0, 3);
            return s.PadLeft(2) + ".";
        }

        // Compact silver (e.g. 1.2M, 45K).
        private static string ShortSilver(long s)
        {
            if (s >= 1_000_000) return $"{s / 1_000_000.0:0.##}M";
            if (s >= 1_000) return $"{s / 1_000.0:0.#}K";
            return s.ToString();
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= max ? s : s.Substring(0, Math.Max(1, max - 1)) + "…";
        }

        // Right-pad even when the leading char is a wide emoji (medal).
        private static string PadAnsi(string s, int width)
        {
            if (s == null) return new string(' ', width);
            if (s.Length >= width) return s + " ";
            return s + new string(' ', Math.Max(0, width - s.Length));
        }

        private static long ToUnix(DateTime utc)
        {
            return new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();
        }

        // -- showcase --

        /// <summary>
        /// KMH 2.7: Builds the per-user marketplace showcase embed. Includes
        /// every active listing the player owns, with item label, quality +
        /// stuff annotation, qty, unit price, and a buy hint (`!buy &lt;id&gt;`).
        /// Falls back to a single "no listings" embed when the player has
        /// nothing posted.
        /// </summary>
        public static Embed BuildShowcaseEmbed(
            string sellerUsername,
            string sellerDiscordHandle,
            string tagline,
            IList<Shared.Files.Economy.MarketplaceListing> myListings)
        {
            string title = $"🛒 {sellerUsername}'s Marketplace";
            EmbedBuilder eb = new EmbedBuilder()
                .WithTitle(title)
                .WithColor(MarketColor)
                .WithTimestamp(DateTimeOffset.UtcNow);

            // Top description: tagline (if any) + a quick how-to-buy line.
            System.Text.StringBuilder header = new System.Text.StringBuilder();
            if (!string.IsNullOrWhiteSpace(tagline))
                header.Append("_").Append(tagline.Trim()).Append("_\n\n");
            header.Append("Buy in-game: `!buy <listing-id> [qty]` from Discord, or open the in-game Marketplace.");
            eb.WithDescription(header.ToString());

            int activeCount = 0;
            long totalAsking = 0;

            if (myListings != null && myListings.Count > 0)
            {
                // Sort newest-first so the top of the embed always shows
                // what was most recently added.
                List<Shared.Files.Economy.MarketplaceListing> sorted =
                    new List<Shared.Files.Economy.MarketplaceListing>(myListings);
                sorted.Sort((a, b) => b.ListedUtcTicks.CompareTo(a.ListedUtcTicks));

                // Discord embeds cap at 25 fields; for the rest we add a
                // "+ N more — see `!market mine`" line at the bottom.
                int shown = 0;
                const int maxFields = 22; // leave room for the footer-field
                foreach (var l in sorted)
                {
                    activeCount++;
                    totalAsking += (long)l.UnitPriceSilver * l.RemainingQty;
                    if (shown >= maxFields) continue;

                    string label = GameServer.Managers.ItemLabelCache.LabelFor(l.ItemDefName);
                    string emoji = DiscordItemIconMap.EmojiFor(l.ItemDefName);

                    System.Text.StringBuilder qual = new System.Text.StringBuilder();
                    if (l.QualityIndex > 0)
                    {
                        // 0..6 maps to Awful..Legendary in RimWorld.
                        string[] names = { "—", "Awful", "Poor", "Normal", "Good", "Excellent", "Masterwork", "Legendary" };
                        int qi = Math.Max(0, Math.Min(names.Length - 1, l.QualityIndex));
                        qual.Append(names[qi]);
                    }
                    if (!string.IsNullOrEmpty(l.StuffDefName))
                    {
                        if (qual.Length > 0) qual.Append(", ");
                        qual.Append(GameServer.Managers.ItemLabelCache.LabelFor(l.StuffDefName));
                    }
                    string qualSuffix = qual.Length > 0 ? $" _({qual})_" : string.Empty;

                    string body = $"**{l.RemainingQty}**× @ `{l.UnitPriceSilver}s`/ea · total `{l.UnitPriceSilver * l.RemainingQty}s` · `!buy {l.Id}`";
                    eb.AddField($"{emoji} #{l.Id} · {label}{qualSuffix}", body, inline: false);
                    shown++;
                }

                if (activeCount > shown)
                {
                    eb.AddField("​",
                        $"_+ {activeCount - shown} more listing(s). Use `!market mine` to see them all._",
                        inline: false);
                }
            }
            else
            {
                eb.AddField("No active listings",
                    "Use `!sell <item> <qty> <price>` from Discord, or post from the in-game Marketplace.",
                    inline: false);
            }

            string footerSeller = string.IsNullOrEmpty(sellerDiscordHandle)
                ? sellerUsername
                : $"{sellerUsername} ({sellerDiscordHandle})";
            string footerText = activeCount > 0
                ? $"{footerSeller} · {activeCount} listing(s) · {totalAsking:N0}s asking total"
                : $"{footerSeller}";
            eb.WithFooter(footerText);

            return eb.Build();
        }

        /// <summary>
        /// KMH 26.5.20: Builds the per-user Want-To-Buy embed. Mirrors
        /// <see cref="BuildShowcaseEmbed"/> in style so a player browsing
        /// the WTB channel and the sells channel sees consistent layout.
        /// </summary>
        public static Embed BuildWtbEmbed(
            string buyerUsername,
            string buyerDiscordHandle,
            string tagline,
            System.Collections.Generic.IList<TCPNetwork.Files.Client.WantToBuyEntry> entries)
        {
            string title = $"🛍️ {buyerUsername} is Buying";
            EmbedBuilder eb = new EmbedBuilder()
                .WithTitle(title)
                .WithColor(new Color(186, 132, 246)) // purple, distinct from amber-sells.
                .WithTimestamp(DateTimeOffset.UtcNow);

            System.Text.StringBuilder header = new System.Text.StringBuilder();
            if (!string.IsNullOrWhiteSpace(tagline))
                header.Append("_").Append(tagline.Trim()).Append("_\n\n");
            header.Append("Fulfill from in-game (post a listing matching the buyer's terms) or DM them on Discord to negotiate.");
            eb.WithDescription(header.ToString());

            int activeCount = 0;
            long totalBudget = 0;

            if (entries != null && entries.Count > 0)
            {
                System.Collections.Generic.List<TCPNetwork.Files.Client.WantToBuyEntry> sorted =
                    new System.Collections.Generic.List<TCPNetwork.Files.Client.WantToBuyEntry>(entries);
                sorted.Sort((a, b) => b.AddedUtcTicks.CompareTo(a.AddedUtcTicks));

                int shown = 0;
                const int maxFields = 22;
                foreach (var w in sorted)
                {
                    activeCount++;
                    totalBudget += (long)w.MaxUnitPriceSilver * w.MaxQty;
                    if (shown >= maxFields) continue;

                    string label = GameServer.Managers.ItemLabelCache.LabelFor(w.ItemDefName);
                    string emoji = DiscordItemIconMap.EmojiFor(w.ItemDefName);
                    string body = $"Up to **{w.MaxQty}**× @ `≤{w.MaxUnitPriceSilver}s`/ea · budget `{w.MaxUnitPriceSilver * w.MaxQty}s`";
                    eb.AddField($"{emoji} {label}", body, inline: false);
                    shown++;
                }

                if (activeCount > shown)
                    eb.AddField("​", $"_+ {activeCount - shown} more — use `!wtb list` to see them all._", inline: false);
            }
            else
            {
                eb.AddField("Empty list", "Use `!wtb add <item> <max-qty> <max-price>` to add what you're looking for.", inline: false);
            }

            string footerBuyer = string.IsNullOrEmpty(buyerDiscordHandle)
                ? buyerUsername
                : $"{buyerUsername} ({buyerDiscordHandle})";
            string footerText = activeCount > 0
                ? $"{footerBuyer} · {activeCount} want(s) · {totalBudget:N0}s total budget"
                : $"{footerBuyer}";
            eb.WithFooter(footerText);

            return eb.Build();
        }

        // -- shared helper --

        private static void Send(EmbedBuilder eb)
        {
            try
            {
                eb.WithTimestamp(DateTimeOffset.UtcNow);

                // KMH: Always tag the source server. If a footer text was already
                // set by the caller, prepend the server tag; otherwise use it alone.
                string serverTag = DiscordBridge.ServerTag ?? "S?";
                string existingFooter = eb.Footer?.Text;
                string footer = string.IsNullOrEmpty(existingFooter)
                    ? $"Server · {serverTag}"
                    : $"Server · {serverTag}  ·  {existingFooter}";
                eb.WithFooter(footer);

                DiscordBridge.TryRelayEmbedToDiscordChat(eb.Build());
            }
            catch
            {
                // Discord may be down or disabled — never crash the gameplay path.
            }
        }
    }
}
