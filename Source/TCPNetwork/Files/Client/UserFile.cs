using Shared;
using Shared.Files;
using Shared.Files.Guilds;
using Shared.Files.Sites;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using TCPNetwork.Packets;
using static Shared.CommonEnumerators;

namespace TCPNetwork.Files.Client
{
    public class UserFile
    {
        //DO NOT USE PRIVATE SET ON THIS VARIABLES
        //IT WILL CAUSE DESERIALIZATION TO WORK INCORRECTLY

        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string Hash { get; set; } = string.Empty;

        public string LatestIP { get; set; } = null;

        public string GuildName { get; set; } = null;

        public bool IsAdmin { get; set; } = false;

        public bool IsBanned { get; set; } = false;

        public PlayerCooldown Cooldowns { get; set; } = new PlayerCooldown();

        public List<PlayerGoodwill> Goodwills { get; set; } = new List<PlayerGoodwill>();

        public PlayerSiteConfig[] SiteConfigs { get; set; } = Array.Empty<PlayerSiteConfig>();

        private Semaphore SavingSemaphore { get; set; } = new Semaphore(1, 1);

        public void UpdateLoginDetails(PKT_Login data)
        {
            Username = data._username;
            Password = data._password;
        }

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

        public void UpdateSiteConfigs(SiteType[] configs)
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
