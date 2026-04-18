using GameServer.Core;
using GameServer.Misc;
using Shared;
using TCPNetwork.Packets;
using TCPNetwork.Files.Client;
using Shared.Files.Sites;
using Shared.Misc;
using GameServer.Hooks.TCPNetwork;
using GameServer.Managers;
using static TCPNetwork.Packets.PKT_Site;
using TCPNetwork.PacketManagers;

namespace GameServer.PacketManager
{
    public class PM_Sites : PM_Base
    {
        [HandlesPacket(PacketHeader.SiteManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            if (!Master.ActionConfigs.SiteAction.IsEnabled)
            {
                ResponseShortcutManager.SendIllegalPacket(client, "Tried to use disabled feature!");
                return;
            }

            PKT_Site data = Serializer.ConvertBytesToObject<PKT_Site>(bytes);
            if (data == null) return;

            switch (data._stepMode)
            {
                case SiteStepMode.Build:
                    AddNewSite(client, data);
                    break;

                case SiteStepMode.Destroy:
                    DestroySite(client, data);
                    break;

                case SiteStepMode.Info:
                    SiteManagerHelper.GetSiteInfo(client, data);
                    break;

                case SiteStepMode.Config:
                    ChangeUserSiteConfig(client, data);
                    break;

                case SiteStepMode.Rewards:
                    SendRewardsToPlayer(client);
                    break;

                case SiteStepMode.Worker:
                    ManageWorker(client, data);
                    break;

                // KMH: Custom player-created sites
                case SiteStepMode.CustomBuild:
                    HandleCustomSiteBuild(client, data);
                    break;

                case SiteStepMode.CustomInfo:
                    HandleCustomSiteInfo(client, data);
                    break;

                case SiteStepMode.WorkerJoin:
                    HandleWorkerJoin(client, data);
                    break;

                case SiteStepMode.WorkerLeave:
                    HandleWorkerLeave(client, data);
                    break;

                case SiteStepMode.Upgrade:
                    HandleSiteUpgrade(client, data);
                    break;
            }
        }

        public static void ConfirmNewSite(ServerClient client, SiteFile siteFile)
        {
            siteFile.SaveSite();

            PKT_Site siteData = new PKT_Site();
            siteData._stepMode = SiteStepMode.Build;
            siteData._file = siteFile;

            foreach (ServerClient cClient in ServerNetwork.GetConnectedClients())
            {
                siteData._file.Goodwill = PM_Goodwills.GetSiteGoodwill(cClient, siteFile);
                cClient.Listener.EnqueuePacket(PacketHeader.SiteManager, siteData);
            }

            siteData._stepMode = SiteStepMode.Accept;
            client.Listener.EnqueuePacket(PacketHeader.SiteManager, siteData);

            InformationDisplayer.DisplayAddSite(siteFile.Tile.ToString());
        }

        private static void AddNewSite(ServerClient client, PKT_Site siteData)
        {
            if (PM_Settlements.CheckIfTileIsInUse(siteData._file.Tile)) ResponseShortcutManager.SendIllegalPacket(client, $"A site tried to be added to tile {siteData._file.Tile}, but that tile already has a settlement");
            else if (SiteManagerHelper.CheckIfTileIsInUse(siteData._file.Tile)) ResponseShortcutManager.SendIllegalPacket(client, $"A site tried to be added to tile {siteData._file.Tile}, but that tile already has a site");
            else
            {
                SiteFile siteFile = new SiteFile();

                siteFile.Tile = siteData._file.Tile;
                siteFile.Username = client.UserFile.Username;
                siteFile.Type = SiteManagerHelper.GetTypeFromDef(siteData._file.Type.DefName);
                if (!string.IsNullOrEmpty(client.UserFile.GuildName)) siteFile.GuildName = client.UserFile.GuildName;
                ConfirmNewSite(client, siteFile);
            }
        }

        private static void DestroySite(ServerClient client, PKT_Site siteData)
        {
            SiteFile siteFile = SiteManagerHelper.GetSiteFileFromTile(siteData._file.Tile);
            if (siteFile.Username == client.UserFile.Username) DestroySiteFromFile(siteFile);
            else ResponseShortcutManager.SendNoPowerPacket(client);
        }

        public static void DestroySiteFromFile(SiteFile siteFile)
        {
            PKT_Site siteData = new PKT_Site();
            siteData._stepMode = SiteStepMode.Destroy;
            siteData._file = siteFile;

            ServerNetwork.SendPacketToAllClients(PacketHeader.SiteManager, siteData);

            File.Delete(Path.Combine(Master.SitesPath, siteFile.Tile + CommonValues.DefaultSaveFormat));

            InformationDisplayer.DisplayRemoveSite(siteFile.Tile.ToString());
        }

        private static void ManageWorker(ServerClient client, PKT_Site data)
        {
            SiteFile site = SiteManagerHelper.GetSiteFileFromTile(data._file.Tile);
            site.WorkerString = data._file.WorkerString;
            site.SaveSite();
        }

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
            string guildName = client.UserFile?.GuildName;

            SiteFile[] allSites = SiteManagerHelper.GetAllSites();
            if (allSites.Length == 0) return;

            List<SiteReward> toReward = new List<SiteReward>();

            foreach (SiteFile site in allSites)
            {
                if (site.Type != null && site.Type.IsCustom)
                {
                    string customPath = System.IO.Path.Combine(Master.SitesPath, $"{site.Tile}_custom.json");
                    if (!System.IO.File.Exists(customPath)) continue;

                    try
                    {
                        CustomSiteData customData = Serializer.SerializeFromFile<CustomSiteData>(customPath);

                        bool isWorker = customData.Workers?.Contains(username) ?? false;
                        bool isOwner = site.Username == username;
                        if (!isWorker && !isOwner) continue;

                        // Per-site cycle time check
                        long nowTicks = System.DateTime.UtcNow.Ticks;
                        double cycleMs = customData.GetEffectiveCycleTimeMs();
                        double elapsedMs = (nowTicks - customData.LastRewardUtcTicks) / (double)System.TimeSpan.TicksPerMillisecond;
                        if (elapsedMs < cycleMs) continue;

                        customData.LastRewardUtcTicks = nowTicks;
                        try { Serializer.SerializeToFile(customPath, customData); } catch { }

                        if (site.Type.Rewards != null && site.Type.Rewards.Length > 0)
                        {
                            foreach (SiteReward reward in site.Type.Rewards)
                            {
                                int amount = reward.Amount;
                                double totalMult = customData.GetTotalProductionMultiplier();
                                amount = (int)System.Math.Ceiling(amount * totalMult);

                                if (!isOwner && customData.AccessMode == SiteAccessMode.Public && customData.OwnerTaxPercent > 0)
                                {
                                    double taxRate = customData.OwnerTaxPercent / 100.0;
                                    amount = (int)(amount * (1.0 - taxRate));
                                    if (amount < 1) amount = 1;
                                }

                                toReward.Add(new SiteReward { DefName = reward.DefName, Amount = amount });
                            }
                        }
                    }
                    catch { }
                }
                else
                {
                    bool isOwnedOrGuild = site.Username == username ||
                        (guildName != null && guildName == site.GuildName);
                    if (!isOwnedOrGuild) continue;
                    if (string.IsNullOrEmpty(site.WorkerString)) continue;

                    PlayerSiteConfig config = client.UserFile.SiteConfigs?.FirstOrDefault(fetch => fetch.DefName == site.Type?.DefName);
                    if (config?.Reward != null)
                        toReward.Add(config.Reward);
                }
            }

            if (toReward.Count > 0)
            {
                PKT_Site siteData = new PKT_Site();
                siteData._stepMode = SiteStepMode.Rewards;
                siteData._rewardFiles = toReward.ToArray();
                client.Listener.EnqueuePacket(PacketHeader.SiteManager, siteData);
            }
        }

        public static void ChangeUserSiteConfig(ServerClient client, PKT_Site data)
        {
            PKT_SiteRewardConfig config = data._rewardConfig;
            if (config == null) return;

            PlayerSiteConfig toFind = client.UserFile.SiteConfigs?.FirstOrDefault(fetch => fetch.DefName == config._siteDef);
            if (toFind == null || toFind.Reward == null) return;

            toFind.Reward.DefName = config._rewardDef;

            SiteType type = Master.ActionConfigs.SiteAction.SiteTypes.FirstOrDefault(fetch => fetch.DefName == config._siteDef);
            if (type == null) return;

            SiteReward matchingReward = type.Rewards?.FirstOrDefault(fetch => fetch.DefName == config._rewardDef);
            if (matchingReward != null)
                toFind.Reward.Amount = matchingReward.Amount;

            client.UserFile.SaveUserFile();
        }

        public static void SetSiteInfoForClient(ServerClient client)
        {
            if (client.UserFile.SiteConfigs.Length > 0) return;
            else client.UserFile.UpdateSiteConfigs(Master.ActionConfigs.SiteAction.SiteTypes);
        }

        public static List<SiteFile> GetSitesFromGoodwill(ServerClient client)
        {
            List<SiteFile> tempList = new List<SiteFile>();
            foreach (SiteFile site in SiteManagerHelper.GetAllSites())
            {
                SiteFile file = new SiteFile();

                file.Tile = site.Tile;
                file.Username = site.Username;
                file.Goodwill = PM_Goodwills.GetSiteGoodwill(client, site);
                file.Type = site.Type;
                file.GuildName = site.GuildName;

                tempList.Add(file);
            }

            return tempList;
        }

        private static void HandleCustomSiteBuild(ServerClient client, PKT_Site data)
        {
            if (data._customRequest == null) return;
            if (!Master.ActionConfigs.SiteAction.AllowCustomSites)
            {
                data._statusMessage = "Custom sites are disabled on this server.";
                data._stepMode = SiteStepMode.CustomInfo;
                client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);
                return;
            }

            var req = data._customRequest;

            // Validate
            if (string.IsNullOrWhiteSpace(req.ItemDefName))
            {
                data._statusMessage = "Invalid item selected.";
                data._stepMode = SiteStepMode.CustomInfo;
                client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);
                return;
            }

            if (req.AmountPerCycle <= 0 || req.AmountPerCycle > Master.ActionConfigs.SiteAction.CustomSiteMaxRewardAmount)
            {
                data._statusMessage = $"Amount must be 1-{Master.ActionConfigs.SiteAction.CustomSiteMaxRewardAmount}.";
                data._stepMode = SiteStepMode.CustomInfo;
                client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);
                return;
            }

            if (req.OwnerTaxPercent < 0 || req.OwnerTaxPercent > 50)
                req.OwnerTaxPercent = 10;

            // Calculate cost and cycle time
            float marketValue = System.Math.Max(1f, req.MarketValuePerUnit);
            double multiplier = Master.ActionConfigs.SiteAction.CustomSitePriceMultiplier;
            int cost = CustomSiteData.CalculateBuildCost(marketValue, req.AmountPerCycle, multiplier);
            double cycleMs = CustomSiteData.CalculateCycleTimeMs(marketValue);
            int cycleMin = (int)(cycleMs / 60000.0);

            // Check tile
            if (PM_Settlements.CheckIfTileIsInUse(req.Tile) || SiteManagerHelper.CheckIfTileIsInUse(req.Tile))
            {
                data._statusMessage = "That tile is already in use.";
                data._stepMode = SiteStepMode.CustomInfo;
                client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);
                return;
            }

            // Validate guild requirement for guild-only sites
            if (req.AccessMode == SiteAccessMode.GuildOnly && string.IsNullOrWhiteSpace(client.UserFile.GuildName))
            {
                data._statusMessage = "You must be in a guild to create a guild-only site.";
                data._stepMode = SiteStepMode.CustomInfo;
                client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);
                return;
            }

            // Build the site
            SiteFile siteFile = new SiteFile();
            siteFile.Tile = req.Tile;
            siteFile.Username = client.UserFile.Username;
            siteFile.GuildName = client.UserFile.GuildName ?? string.Empty;
            siteFile.Type = new SiteType
            {
                DefName = "RTCustomOutpost",
                Cost = cost,
                Description = $"Custom site producing {req.AmountPerCycle}x {req.ItemDefName} every {cycleMin} min",
                CycleTimeMinutes = cycleMin,
                IsCustom = true,
                CreatedBy = client.UserFile.Username,
                Rewards = new SiteReward[] { new SiteReward { DefName = req.ItemDefName, Amount = req.AmountPerCycle } }
            };

            // Save custom data alongside
            string relevantSkill = CustomSiteData.DetermineRelevantSkill(req.ItemDefName);

            CustomSiteData customData = new CustomSiteData
            {
                ItemDefName = req.ItemDefName,
                BaseAmountPerCycle = req.AmountPerCycle,
                MarketValuePerUnit = marketValue,
                BaseCycleTimeMs = cycleMs,
                AccessMode = req.AccessMode,
                OwnerTaxPercent = req.OwnerTaxPercent,
                MaxWorkers = 5,
                LastRewardUtcTicks = System.DateTime.UtcNow.Ticks,
                RelevantSkillDef = relevantSkill
            };
            customData.Workers.Add(client.UserFile.Username);
            if (customData.WorkerSkills == null)
                customData.WorkerSkills = new System.Collections.Generic.Dictionary<string, int>();

            // Save both files
            ConfirmNewSite(client, siteFile);

            // Save custom data
            try
            {
                string customPath = System.IO.Path.Combine(Master.SitesPath, $"{req.Tile}_custom.json");
                Serializer.SerializeToFile(customPath, customData);
            }
            catch { }

            data._statusMessage = $"Custom site built!\nProducing: {req.AmountPerCycle}x {req.ItemDefName}\nCycle: {cycleMin} min | Cost: {cost} silver\nRelevant Skill: {relevantSkill}\nMore workers with high {relevantSkill} skill = faster production!";
            data._calculatedCost = cost;
            data._calculatedCycleMinutes = cycleMin;
            data._customData = customData;
            data._stepMode = SiteStepMode.CustomInfo;
            client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);

            Printer.Warning($"[CustomSite] {client.UserFile.Username} built custom site at tile {req.Tile}: {req.AmountPerCycle}x {req.ItemDefName} (cost: {cost}, cycle: {cycleMin}min)");
        }

        private static void HandleWorkerJoin(ServerClient client, PKT_Site data)
        {
            int tile = data._file?.Tile ?? -1;
            if (tile < 0) return;

            string customPath = System.IO.Path.Combine(Master.SitesPath, $"{tile}_custom.json");
            if (!System.IO.File.Exists(customPath))
            {
                data._statusMessage = "Not a custom site.";
                data._stepMode = SiteStepMode.CustomInfo;
                client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);
                return;
            }

            try
            {
                CustomSiteData customData = Serializer.SerializeFromFile<CustomSiteData>(customPath);
                SiteFile siteFile = SiteManagerHelper.GetSiteFileFromTile(tile);

                // Check access
                string username = client.UserFile.Username;
                if (customData.AccessMode == SiteAccessMode.Private)
                {
                    data._statusMessage = "This site is private.";
                    data._stepMode = SiteStepMode.CustomInfo;
                    client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);
                    return;
                }

                if (customData.AccessMode == SiteAccessMode.GuildOnly)
                {
                    if (string.IsNullOrEmpty(client.UserFile.GuildName) ||
                        client.UserFile.GuildName != siteFile?.GuildName)
                    {
                        data._statusMessage = "This site is guild-only. You're not in the right guild.";
                        data._stepMode = SiteStepMode.CustomInfo;
                        client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);
                        return;
                    }
                }

                if (customData.Workers.Contains(username))
                {
                    data._statusMessage = "You're already a worker at this site.";
                    data._stepMode = SiteStepMode.CustomInfo;
                    client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);
                    return;
                }

                if (customData.Workers.Count >= customData.MaxWorkers)
                {
                    data._statusMessage = $"Site is full ({customData.Workers.Count}/{customData.MaxWorkers} workers).";
                    data._stepMode = SiteStepMode.CustomInfo;
                    client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);
                    return;
                }

                customData.Workers.Add(username);

                // Record worker skill level
                int skillLevel = data._workerSkillLevel;
                if (skillLevel < 0) skillLevel = 0;
                if (skillLevel > 20) skillLevel = 20;
                if (customData.WorkerSkills == null)
                    customData.WorkerSkills = new System.Collections.Generic.Dictionary<string, int>();
                customData.WorkerSkills[username] = skillLevel;

                Serializer.SerializeToFile(customPath, customData);

                double effectiveMin = customData.GetEffectiveCycleTimeMs() / 60000.0;
                double skillEff = customData.GetSkillEfficiency();
                data._statusMessage = $"Joined as worker! Workers: {customData.Workers.Count}/{customData.MaxWorkers}\nSpeed: {customData.GetSpeedMultiplier():F1}x | Skill Eff: {skillEff:F0}% | Total: {customData.GetTotalProductionMultiplier():F1}x\nEffective Cycle: {(int)effectiveMin} min\nYour {customData.RelevantSkillDef} skill: {skillLevel}";
                data._customData = customData;
                data._stepMode = SiteStepMode.CustomInfo;
                client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);

                Printer.Warning($"[CustomSite] {username} joined site at tile {tile} as worker ({customData.Workers.Count}/{customData.MaxWorkers})");
            }
            catch (System.Exception e)
            {
                data._statusMessage = $"Failed to join: {e.Message}";
                data._stepMode = SiteStepMode.CustomInfo;
                client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);
            }
        }

        private static void HandleWorkerLeave(ServerClient client, PKT_Site data)
        {
            int tile = data._file?.Tile ?? -1;
            if (tile < 0) return;

            string customPath = System.IO.Path.Combine(Master.SitesPath, $"{tile}_custom.json");
            if (!System.IO.File.Exists(customPath)) return;

            try
            {
                CustomSiteData customData = Serializer.SerializeFromFile<CustomSiteData>(customPath);
                string username = client.UserFile.Username;

                if (!customData.Workers.Contains(username))
                {
                    data._statusMessage = "You're not a worker at this site.";
                    data._stepMode = SiteStepMode.CustomInfo;
                    client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);
                    return;
                }

                customData.Workers.Remove(username);
                if (customData.WorkerSkills != null)
                    customData.WorkerSkills.Remove(username);
                Serializer.SerializeToFile(customPath, customData);

                data._statusMessage = $"Left the site. Workers: {customData.Workers.Count}/{customData.MaxWorkers}.";
                data._customData = customData;
                data._stepMode = SiteStepMode.CustomInfo;
                client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);

                Printer.Warning($"[CustomSite] {username} left site at tile {tile}");
            }
            catch { }
        }

        private static void HandleSiteUpgrade(ServerClient client, PKT_Site data)
        {
            int tile = data._file?.Tile ?? -1;
            if (tile < 0) return;

            string customPath = System.IO.Path.Combine(Master.SitesPath, $"{tile}_custom.json");
            if (!System.IO.File.Exists(customPath))
            {
                data._statusMessage = "Not a custom site.";
                data._stepMode = SiteStepMode.CustomInfo;
                client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);
                return;
            }

            SiteFile siteFile = SiteManagerHelper.GetSiteFileFromTile(tile);
            if (siteFile == null || siteFile.Username != client.UserFile.Username)
            {
                data._statusMessage = "Only the site owner can upgrade.";
                data._stepMode = SiteStepMode.CustomInfo;
                client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);
                return;
            }

            try
            {
                CustomSiteData cd = Serializer.SerializeFromFile<CustomSiteData>(customPath);

                // Upgrade tiers: 5 -> 8 -> 12 -> 16 -> 20
                int[] tiers = { 5, 8, 12, 16, 20 };
                int currentTier = 0;
                for (int i = 0; i < tiers.Length; i++)
                {
                    if (cd.MaxWorkers <= tiers[i]) { currentTier = i; break; }
                }

                if (currentTier >= tiers.Length - 1)
                {
                    data._statusMessage = $"Site is already at max upgrade level ({cd.MaxWorkers} workers).";
                    data._stepMode = SiteStepMode.CustomInfo;
                    client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);
                    return;
                }

                int nextMax = tiers[currentTier + 1];
                // Upgrade cost: 500 silver per tier level
                int upgradeCost = (currentTier + 1) * 500;

                cd.MaxWorkers = nextMax;
                Serializer.SerializeToFile(customPath, cd);

                data._statusMessage = $"Site upgraded! Max workers: {nextMax}\nUpgrade cost: {upgradeCost} silver\n(Note: Silver deducted from next reward cycle)";
                data._customData = cd;
                data._stepMode = SiteStepMode.CustomInfo;
                client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);

                Printer.Warning($"[CustomSite] {client.UserFile.Username} upgraded site at tile {tile} to {nextMax} max workers");
            }
            catch
            {
                data._statusMessage = "Upgrade failed.";
                data._stepMode = SiteStepMode.CustomInfo;
                client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);
            }
        }

        private static void HandleCustomSiteInfo(ServerClient client, PKT_Site data)
        {
            int tile = data._file?.Tile ?? -1;
            if (tile < 0) return;

            // Check if it's a custom site
            string customPath = System.IO.Path.Combine(Master.SitesPath, $"{tile}_custom.json");
            SiteFile siteFile = SiteManagerHelper.GetSiteFileFromTile(tile);

            if (!System.IO.File.Exists(customPath))
            {
                // Standard site info
                if (siteFile != null)
                {
                    string siteTypeName = siteFile.Type?.DefName ?? "Unknown";
                    string siteGuild = string.IsNullOrEmpty(siteFile.GuildName) ? "None" : siteFile.GuildName;
                    string workerStatus = string.IsNullOrEmpty(siteFile.WorkerString) ? "None assigned" : "Active";
                    int siteCost = siteFile.Type?.Cost ?? 0;
                    data._statusMessage = $"Standard Site: {siteTypeName}\nOwner: {siteFile.Username}\nGuild: {siteGuild}\nWorker: {workerStatus}\nCost: {siteCost} silver";
                }
                else
                {
                    data._statusMessage = "Site data not found.";
                }
                data._stepMode = SiteStepMode.CustomInfo;
                client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);
                return;
            }

            try
            {
                CustomSiteData cd = Serializer.SerializeFromFile<CustomSiteData>(customPath);
                data._customData = cd;

                double speedMult = cd.GetSpeedMultiplier();
                double skillEff = cd.GetSkillEfficiency();
                double totalMult = cd.GetTotalProductionMultiplier();
                int effectiveAmount = (int)System.Math.Ceiling(cd.BaseAmountPerCycle * totalMult);
                double effectiveCycleMin = cd.GetEffectiveCycleTimeMs() / 60000.0;
                double baseCycleMin = cd.BaseCycleTimeMs / 60000.0;

                // Build detailed info
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"=== Custom Site (KMH) ===");
                sb.AppendLine($"Item: {cd.ItemDefName} x{cd.BaseAmountPerCycle}/cycle");
                string ownerName = siteFile?.Username ?? "Unknown";
                sb.AppendLine($"Owner: {ownerName}");
                string guildName = string.IsNullOrEmpty(siteFile?.GuildName) ? "None" : siteFile.GuildName;
                sb.AppendLine($"Guild: {guildName}");
                sb.AppendLine($"Access: {cd.AccessMode}");
                if (cd.AccessMode == SiteAccessMode.Public)
                    sb.AppendLine($"Owner Tax: {cd.OwnerTaxPercent}%");
                sb.AppendLine();

                // Workers section
                sb.AppendLine($"--- Workers ({cd.Workers?.Count ?? 0}/{cd.MaxWorkers}) ---");
                if (cd.Workers != null && cd.Workers.Count > 0)
                {
                    foreach (string worker in cd.Workers)
                    {
                        int skill = 0;
                        if (cd.WorkerSkills != null && cd.WorkerSkills.ContainsKey(worker))
                            skill = cd.WorkerSkills[worker];
                        string ownerTag = (worker == siteFile?.Username) ? " [OWNER]" : "";
                        sb.AppendLine($"  {worker}{ownerTag} - {cd.RelevantSkillDef}: {skill}");
                    }
                }
                else
                {
                    sb.AppendLine("  No workers assigned");
                }
                sb.AppendLine();

                // Production stats
                sb.AppendLine($"--- Production ---");
                sb.AppendLine($"Relevant Skill: {cd.RelevantSkillDef}");
                sb.AppendLine($"Worker Speed: {speedMult:F2}x");
                sb.AppendLine($"Skill Efficiency: {skillEff:P0}");
                sb.AppendLine($"Total Multiplier: {totalMult:F2}x");
                sb.AppendLine($"Effective Output: {effectiveAmount}x {cd.ItemDefName}/cycle");
                sb.AppendLine($"Base Cycle: {(int)baseCycleMin} min");
                sb.AppendLine($"Effective Cycle: {(int)effectiveCycleMin} min");

                // Value estimate
                double valuePerCycle = cd.MarketValuePerUnit * effectiveAmount;
                double cyclesPerHour = 60.0 / effectiveCycleMin;
                sb.AppendLine($"Value: ~${valuePerCycle:F0}/cycle (~${(valuePerCycle * cyclesPerHour):F0}/hr)");

                data._statusMessage = sb.ToString();
                data._calculatedCycleMinutes = (int)baseCycleMin;
            }
            catch (System.Exception e)
            {
                data._statusMessage = $"Failed to load custom site data: {e.Message}";
            }

            data._stepMode = SiteStepMode.CustomInfo;
            client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);
        }

    }

    public static class SiteManagerHelper
    {
        public static SiteFile[] GetAllSitesFromUsername(string username)
        {
            List<SiteFile> sitesList = new List<SiteFile>();

            string[] sites = Directory.GetFiles(Master.SitesPath);
            foreach (string site in sites)
            {
                if (site.Contains("_custom")) continue;
                try
                {
                    SiteFile siteFile = Serializer.SerializeFromFile<SiteFile>(site);
                    if (siteFile != null && siteFile.Username == username) sitesList.Add(siteFile);
                }
                catch { }
            }

            return sitesList.ToArray();
        }

        public static SiteFile GetSiteFileFromTile(int tileToGet)
        {
            string[] sites = Directory.GetFiles(Master.SitesPath);
            foreach (string site in sites)
            {
                if (site.Contains("_custom")) continue;
                try
                {
                    SiteFile siteFile = Serializer.SerializeFromFile<SiteFile>(site);
                    if (siteFile != null && siteFile.Tile == tileToGet) return siteFile;
                }
                catch { }
            }

            return null;
        }

        public static void GetSiteInfo(ServerClient client, PKT_Site data)
        {
            SiteFile siteFile = GetSiteFileFromTile(data._file.Tile);
            data._stepMode = SiteStepMode.Info;
            data._file = siteFile;

            client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);
        }

        public static SiteFile[] GetAllSites()
        {
            List<SiteFile> sitesList = new List<SiteFile>();
            try
            {
                string[] sites = Directory.GetFiles(Master.SitesPath);
                foreach (string site in sites)
                {
                    // Skip custom site data files
                    if (site.Contains("_custom")) continue;

                    try
                    {
                        SiteFile siteFile = Serializer.SerializeFromFile<SiteFile>(site);
                        if (siteFile != null) sitesList.Add(siteFile);
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Printer.Error($"Sites could not be loaded: {ex.Message}"); }

            return sitesList.ToArray();
        }

        public static bool CheckIfTileIsInUse(int tileToCheck)
        {
            string[] sites = Directory.GetFiles(Master.SitesPath);
            foreach (string site in sites)
            {
                if (site.Contains("_custom")) continue;
                try
                {
                    SiteFile siteFile = Serializer.SerializeFromFile<SiteFile>(site);
                    if (siteFile != null && siteFile.Tile == tileToCheck) return true;
                }
                catch { }
            }

            return false;
        }

        public static SiteType GetTypeFromDef(string defName)
        {
            SiteType site = Master.ActionConfigs.SiteAction.SiteTypes.Where(S => S.DefName == defName).FirstOrDefault();
            if (site != null) return site;
            return null;
        }
    }
}
