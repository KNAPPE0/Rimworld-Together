using GameServer.Core;
using GameServer.Hooks.TCPNetwork;
using GameServer.Managers;
using Shared;
using Shared.Files.Economy;
using Shared.Files.Sites;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static TCPNetwork.Packets.PKT_Site;

namespace GameServer.PacketManager
{
    /// <summary>
    /// Periodic site reward distribution.
    ///
    /// On each cycle:
    ///   * Compute production multiplier (worker count × skill efficiency)
    ///   * For each reward item, split into owner share + worker share
    ///   * Route each share via the recipient's chosen <see cref="RewardDestination"/>
    ///     (caravan delivery / treasury deposit / auto-list on marketplace)
    ///   * Award XP to every worker (raises their level over time)
    /// </summary>
    public partial class PM_Sites
    {
        public static void SendRewardsToEveryPlayer()
        {
            foreach (ServerClient client in ServerNetwork.GetConnectedClients())
            {
                SendRewardsToPlayer(client);
            }
        }

        public static void SendRewardsToPlayer(ServerClient client)
        {
            string username = client.UserFile?.Username;
            if (string.IsNullOrWhiteSpace(username)) return;

            string guildName = client.UserFile?.GuildName;

            SiteFile[] allSites = SiteManagerHelper.GetAllSites();
            if (allSites.Length == 0) return;

            List<SiteReward> caravanDeliveries = new List<SiteReward>();

            foreach (SiteFile site in allSites)
            {
                if (site.Type != null && site.Type.IsCustom)
                {
                    HandleCustomSiteCycleForUser(site, username, caravanDeliveries);
                }
                else
                {
                    bool isOwnedOrGuild = site.Username == username ||
                        (guildName != null && guildName == site.GuildName);
                    if (!isOwnedOrGuild) continue;
                    if (string.IsNullOrEmpty(site.WorkerString)) continue;

                    // KMH 26.5.20.1: Was a LINQ FirstOrDefault scan per site
                    // per reward poll. Plain foreach saves the LINQ delegate
                    // allocation and is marginally faster.
                    PlayerSiteConfig config = null;
                    string siteDef = site.Type?.DefName;
                    if (!string.IsNullOrEmpty(siteDef) && client.UserFile.SiteConfigs != null)
                    {
                        foreach (PlayerSiteConfig cfg in client.UserFile.SiteConfigs)
                        {
                            if (cfg != null && cfg.DefName == siteDef) { config = cfg; break; }
                        }
                    }
                    if (config?.Reward != null)
                        caravanDeliveries.Add(config.Reward);
                }
            }

            if (caravanDeliveries.Count > 0)
            {
                PKT_Site siteData = new PKT_Site();
                siteData._stepMode = SiteStepMode.Rewards;
                siteData._rewardFiles = caravanDeliveries.ToArray();
                client.Listener.EnqueuePacket(PacketHeader.SiteManager, siteData);
            }
        }

        // -- core custom-site cycle logic --

        private static void HandleCustomSiteCycleForUser(SiteFile site, string username, List<SiteReward> caravanDeliveries)
        {
            string customPath = Path.Combine(Master.SitesPath, $"{site.Tile}_custom.json");
            CustomSiteData cd = SiteManagerHelper.LoadCustomSiteData(customPath);
            if (cd == null) return;

            bool isOwner = site.Username == username;
            bool isWorker = cd.Workers != null && cd.Workers.Contains(username);
            if (!isOwner && !isWorker) return;

            long nowTicks = DateTime.UtcNow.Ticks;
            double cycleMs = cd.GetEffectiveCycleTimeMs();
            double elapsedMs = (nowTicks - cd.LastRewardUtcTicks) / (double)TimeSpan.TicksPerMillisecond;
            if (elapsedMs < cycleMs) return;

            cd.LastRewardUtcTicks = nowTicks;

            // Cycle elapsed for the site as a whole — distribute to ALL participants,
            // not just the requesting user. Avoids one player "claiming" the cycle
            // and forcing the rest to wait another full one.
            //
            // KMH 26.5.20.1: De-dupe via HashSet instead of List.Contains —
            // List.Contains is O(n), so building the recipient list of N
            // workers was O(n²). HashSet.Add is O(1) average.
            HashSet<string> recipientsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> recipients = new List<string>();
            if (!string.IsNullOrEmpty(site.Username) && recipientsSet.Add(site.Username))
                recipients.Add(site.Username);
            if (cd.Workers != null)
            {
                foreach (string w in cd.Workers)
                {
                    if (!string.IsNullOrEmpty(w) && recipientsSet.Add(w)) recipients.Add(w);
                }
            }

            double totalMult = cd.GetTotalProductionMultiplier();
            double xpMult = Master.ActionConfigs?.SiteAction?.WorkerXpMultiplier ?? 1.0;

            // KMH 26.5.20.1: Was redundantly calling GetSiteFileFromTile(site.Tile)
            // to re-fetch the same `site` object we already have in scope.
            // Read GuildName directly off the parameter.
            string ownerGuildName = site.GuildName;
            if (!string.IsNullOrEmpty(ownerGuildName))
                xpMult *= GuildManager.GetWorkerXpMultiplier(ownerGuildName);

            if (cd.WorkerProgress == null)
                cd.WorkerProgress = new Dictionary<string, WorkerProgress>(StringComparer.OrdinalIgnoreCase);

            // Award XP to every worker present this cycle.
            // KMH 26.5.20.1: Avoid allocating an empty List<string> when
            // there are no workers (was `cd.Workers ?? new List<string>()`).
            if (cd.Workers != null)
            foreach (string worker in cd.Workers)
            {
                if (!cd.WorkerProgress.TryGetValue(worker, out WorkerProgress wp) || wp == null)
                {
                    wp = new WorkerProgress { JoinedUtcTicks = nowTicks };
                    cd.WorkerProgress[worker] = wp;
                }
                double beforeXp = wp.Xp;
                wp.AwardCycleXp(xpMult);
                double xpDelta = wp.Xp - beforeXp;
                // KMH 2.7: Lifetime worker XP for the player leaderboard.
                if (xpDelta > 0)
                    try { GameServer.Managers.PlayerStatsManager.RecordWorkerXp(worker, (long)Math.Round(xpDelta)); }
                    catch { }
            }

            // Distribute item rewards.
            if (site.Type.Rewards != null && site.Type.Rewards.Length > 0)
            {
                foreach (SiteReward reward in site.Type.Rewards)
                {
                    int totalProduced = (int)Math.Ceiling(reward.Amount * totalMult);
                    if (totalProduced <= 0) continue;

                    foreach (string recipient in recipients)
                    {
                        bool recipientIsOwner = recipient == site.Username;
                        int share = totalProduced;

                        // Public sites: owner takes a tax cut from each non-owner worker.
                        if (!recipientIsOwner && cd.AccessMode == SiteAccessMode.Public && cd.OwnerTaxPercent > 0)
                        {
                            double taxRate = cd.OwnerTaxPercent / 100.0;
                            share = (int)(totalProduced * (1.0 - taxRate));
                            if (share < 1) share = 1;
                        }

                        RewardDestination dest = ResolveDestination(cd, recipient, recipientIsOwner);
                        DeliverShare(recipient, dest, cd, reward.DefName, share, site.Tile, caravanDeliveries, requesterUsername: username);
                    }
                }
            }

            SiteManagerHelper.SaveCustomSiteData(customPath, cd);
        }

        private static RewardDestination ResolveDestination(CustomSiteData cd, string recipient, bool isOwner)
        {
            if (isOwner) return cd.OwnerRewardDestination;
            if (cd.WorkerProgress != null && cd.WorkerProgress.TryGetValue(recipient, out WorkerProgress wp) && wp != null)
                return wp.Destination;
            return RewardDestination.Caravan;
        }

        private static void DeliverShare(
            string recipientUsername,
            RewardDestination destination,
            CustomSiteData cd,
            string defName,
            int amount,
            int siteTile,
            List<SiteReward> caravanDeliveriesForRequester,
            string requesterUsername)
        {
            if (amount <= 0) return;

            // KMH: Guild silver tax — only meaningful when the reward is silver
            // and the recipient is in a guild. Tax % comes from GuildSettings.
            int netAmount = amount;
            if (string.Equals(defName, "Silver", StringComparison.OrdinalIgnoreCase))
                netAmount = GuildManager.ApplyGuildSiteRewardTax(recipientUsername, amount);
            if (netAmount <= 0) return;

            switch (destination)
            {
                case RewardDestination.Caravan:
                    // Only enqueue caravan delivery if this recipient is the user
                    // currently polling — others pick up their share on their own poll.
                    if (string.Equals(recipientUsername, requesterUsername, StringComparison.OrdinalIgnoreCase))
                        caravanDeliveriesForRequester.Add(new SiteReward { DefName = defName, Amount = netAmount });
                    break;

                case RewardDestination.Treasury:
                    TreasuryManager.DepositItemForUser(recipientUsername, defName, netAmount,
                        TreasuryTransaction.TxKind.SiteRewardItem, $"site#{siteTile}");
                    break;

                case RewardDestination.Marketplace:
                    int unitPrice = Math.Max(1, cd.MarketplaceUnitPrice);
                    MarketplaceManager.AutoListFromSite(recipientUsername, defName, netAmount, unitPrice, siteTile);
                    break;
            }
        }
    }
}
