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

        private Semaphore SavingSemaphore { get; set; } = new Semaphore(1, 1);

        public void SaveUserFile()
        {
            SavingSemaphore.WaitOne();

            try { Serializer.SerializeToFile(Path.Combine(CommonValues.ServerUsersPath, Username + CommonValues.DefaultSaveFormat), this); }
            catch (Exception e) { throw new Exception(e.ToString()); }

            SavingSemaphore.Release();
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
