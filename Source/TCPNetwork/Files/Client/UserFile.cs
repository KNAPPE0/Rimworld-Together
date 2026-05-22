using Shared;
using Shared.Files.Guilds;
using Shared.Files.Sites;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using TCPNetwork.Packets;
using static Shared.CommonEnumerators;

namespace TCPNetwork.Files.Client
{
    public class UserFile
    {
        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string Hash { get; set; } = string.Empty;

        public string LatestIP { get; set; } = null;

        public string GuildName { get; set; } = null;

        public bool IsAdmin { get; set; } = false;

        public bool IsBanned { get; set; } = false;

        public ServerClient SynchronousClient { get; set; } = null;

        public PlayerCooldown Cooldowns { get; set; } = new PlayerCooldown();

        public List<PlayerGoodwill> Goodwills { get; set; } = new List<PlayerGoodwill>();

        public PlayerSiteConfig[] SiteConfigs { get; set; } = Array.Empty<PlayerSiteConfig>();

        // KMH: Discord account linking
        public string DiscordId { get; set; } = null;
        public string DiscordUsername { get; set; } = null;
        public string DiscordLinkToken { get; set; } = null;
        public long DiscordLinkTokenExpiry { get; set; } = 0;

        // Per-user marketplace showcase tracking. When a linked
        // player runs `!showcase`, the bot posts a formatted embed of
        // their current listings to the configured marketplace channel /
        // forum and stores the message ID here so subsequent `!showcase
        // update` and `!showcase delete` calls can find it. Stored as a
        // string because Discord IDs are 64-bit unsigned and we don't want
        // to round-trip through long on serialise.
        public string DiscordShowcaseMessageId { get; set; } = null;
        public string DiscordShowcaseChannelId { get; set; } = null;
        // Optional personal "tag-line" the user supplies via
        // `!showcase tagline <text>` — e.g. "DM me on Discord to haggle".
        // Renders at the bottom of the embed. Capped server-side.
        public string DiscordShowcaseTagline { get; set; } = null;
        // Sweep deletes showcases stale beyond ShowcaseStaleHours.
        public long DiscordShowcaseLastUpdatedUtcTicks { get; set; } = 0;

        // Want-To-Buy board — `!wtb add/remove/list/post`.
        public List<WantToBuyEntry> WantToBuyEntries { get; set; } = new List<WantToBuyEntry>();
        public string DiscordWtbMessageId { get; set; } = null;
        public string DiscordWtbChannelId { get; set; } = null;
        public string DiscordWtbTagline { get; set; } = null;
        public long DiscordWtbLastUpdatedUtcTicks { get; set; } = 0;

        // Per-player lifetime stats for the player leaderboard.
        // These accumulate forever and are mirrored from the gameplay
        // managers (TreasuryManager, MarketplaceManager, QuestManager,
        // SiteManager) on the relevant action paths.
        public long LifetimeSilverDonated { get; set; } = 0;
        public long LifetimeSilverEarnedFromSales { get; set; } = 0;
        public long LifetimeSilverSpentOnPurchases { get; set; } = 0;
        public int LifetimeQuestsCompleted { get; set; } = 0;
        public int LifetimeQuestsPosted { get; set; } = 0;
        public int LifetimeMarketplaceSalesCount { get; set; } = 0;
        public int LifetimeSitesBuilt { get; set; } = 0;
        public int LifetimeSitesRaided { get; set; } = 0;
        public long LifetimeWorkerXpEarned { get; set; } = 0;
        public long FirstSeenUtcTicks { get; set; } = 0;

        private Semaphore SavingSemaphore { get; set; } = new Semaphore(1, 1);

        // KMH: Lets the server-side user-cache observe writes without TCPNetwork
        // taking a dependency on the GameServer assembly.
        public static event Action<UserFile> OnUserFileSaved;

        public void SaveUserFile()
        {
            SavingSemaphore.WaitOne();

            try { Serializer.SerializeToFile(Path.Combine(CommonValues.ServerUsersPath, Username + CommonValues.DefaultSaveFormat), this); }
            catch (Exception e) { throw new Exception(e.ToString()); }
            finally { SavingSemaphore.Release(); }

            try { OnUserFileSaved?.Invoke(this); }
            catch { }
        }

        public void UpdateFaction(GuildFile toUpdateWith)
        {
            if (toUpdateWith == null) GuildName = null;
            else GuildName = toUpdateWith.Name;

            SaveUserFile();
        }

        public void UpdateAdmin(bool mode)
        {
            IsAdmin = mode;
            SaveUserFile();
        }

        public void UpdateBan(bool mode)
        {
            IsBanned = mode;
            SaveUserFile();
        }

        public void UpdateIP(string IP)
        {
            LatestIP = IP;
            SaveUserFile();
        }

        public void UpdateGoodwill(string username, Goodwill goodwill)
        {
            PlayerGoodwill toFind = Goodwills.FirstOrDefault(fetch => fetch.Name == username);
            if (toFind != null) toFind.Goodwill = goodwill;
            else
            {
                PlayerGoodwill newGoodwill = new PlayerGoodwill();
                newGoodwill.Name = username;
                newGoodwill.Goodwill = goodwill;

                Goodwills.Add(newGoodwill);
            }

            SaveUserFile();
        }

        public void UpdateSiteConfigs(List<SiteType> configs)
        {
            List<PlayerSiteConfig> newConfigs = new List<PlayerSiteConfig>();
            foreach (SiteType type in configs)
            {
                PlayerSiteConfig newConfig = new PlayerSiteConfig();
                newConfig.DefName = type.DefName;
                newConfig.Reward = type.Rewards[0];

                newConfigs.Add(newConfig);
            }

            SiteConfigs = newConfigs.ToArray();

            SaveUserFile();
        }

        public void UpdateHash() 
        { 
            Hash = Hasher.GetHashFromString($"{Username}:{Password}");
            SaveUserFile();
        }
    }
}
