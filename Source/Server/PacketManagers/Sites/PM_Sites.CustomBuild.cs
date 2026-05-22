using GameServer.Core;
using GameServer.Managers;
using Shared;
using Shared.Files.Sites;
using Shared.Misc;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static TCPNetwork.Packets.PKT_Site;

namespace GameServer.PacketManager
{
    /// <summary>
    /// Custom-site build flow + per-user rate limiting + input hardening.
    ///
    /// The server has no def database, so client-supplied
    /// <c>MarketValuePerUnit</c> is clamped to a trusted band and the cycle
    /// time is forced to a floor. This prevents the
    /// "tiny value -> tiny cost / short cycle for high-tier item" exploit.
    /// </summary>
    public partial class PM_Sites
    {
        // Hardening constants for client-untrusted custom-site inputs.
        private const float MinTrustedMarketValue = 1f;
        private const float MaxTrustedMarketValue = 5000f;
        private const int MinCustomCycleMinutes = 60;
        private const int MaxCustomCycleMinutes = 240;
        private const int CustomBuildCooldownMs = 30_000;

        private static readonly object CustomBuildLock = new object();
        private static readonly Dictionary<string, long> LastCustomBuildUtcTicks =
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        private static bool TryConsumeCustomBuildCooldown(string username)
        {
            long nowTicks = DateTime.UtcNow.Ticks;
            long cooldownTicks = TimeSpan.FromMilliseconds(CustomBuildCooldownMs).Ticks;

            lock (CustomBuildLock)
            {
                if (LastCustomBuildUtcTicks.TryGetValue(username, out long last))
                {
                    if (nowTicks - last < cooldownTicks)
                        return false;
                }

                LastCustomBuildUtcTicks[username] = nowTicks;
                return true;
            }
        }

        private static void HandleCustomSiteBuild(ServerClient client, PKT_Site data)
        {
            if (data._customRequest == null) return;
            if (!Master.ActionConfigs.SiteAction.AllowCustomSites)
            {
                RespondCustomInfo(client, data, "Custom sites are disabled on this server.");
                return;
            }

            string username = client.UserFile?.Username;
            if (string.IsNullOrWhiteSpace(username)) return;

            if (!TryConsumeCustomBuildCooldown(username))
            {
                RespondCustomInfo(client, data, "You're building too fast. Please wait before building another custom site.");
                return;
            }

            var req = data._customRequest;

            if (string.IsNullOrWhiteSpace(req.ItemDefName))
            {
                RespondCustomInfo(client, data, "Invalid item selected.");
                return;
            }

            // Defense in depth — defNames from the client should be plain identifiers.
            for (int i = 0; i < req.ItemDefName.Length; i++)
            {
                char c = req.ItemDefName[i];
                if (!(char.IsLetterOrDigit(c) || c == '_'))
                {
                    RespondCustomInfo(client, data, "Invalid item identifier.");
                    return;
                }
            }
            if (req.ItemDefName.Length > 64)
            {
                RespondCustomInfo(client, data, "Item identifier too long.");
                return;
            }

            int maxAmount = Master.ActionConfigs.SiteAction.CustomSiteMaxRewardAmount;
            if (req.AmountPerCycle <= 0 || req.AmountPerCycle > maxAmount)
            {
                RespondCustomInfo(client, data, $"Amount must be 1-{maxAmount}.");
                return;
            }

            if (req.Tile < 0)
            {
                RespondCustomInfo(client, data, "Invalid tile.");
                return;
            }

            if (req.OwnerTaxPercent < 0 || req.OwnerTaxPercent > 50)
                req.OwnerTaxPercent = 10;

            // KMH: Server clamps the client-supplied market value to a trusted band.
            float marketValue = req.MarketValuePerUnit;
            if (float.IsNaN(marketValue) || float.IsInfinity(marketValue)) marketValue = MinTrustedMarketValue;
            if (marketValue < MinTrustedMarketValue) marketValue = MinTrustedMarketValue;
            if (marketValue > MaxTrustedMarketValue) marketValue = MaxTrustedMarketValue;

            double multiplier = Master.ActionConfigs.SiteAction.CustomSitePriceMultiplier;
            int cost = CustomSiteData.CalculateBuildCost(marketValue, req.AmountPerCycle, multiplier);

            // KMH: Apply guild discount perk (-0/-10/-20/-30%).
            string ownerGuildName = client.UserFile?.GuildName;
            if (!string.IsNullOrEmpty(ownerGuildName))
                cost = (int)Math.Ceiling(cost * GuildManager.GetCustomSiteCostMultiplier(ownerGuildName));

            // KMH: Force a minimum cycle time independent of client-supplied value.
            double cycleMs = CustomSiteData.CalculateCycleTimeMs(marketValue);
            int cycleMin = (int)(cycleMs / 60000.0);
            if (cycleMin < MinCustomCycleMinutes) cycleMin = MinCustomCycleMinutes;
            if (cycleMin > MaxCustomCycleMinutes) cycleMin = MaxCustomCycleMinutes;
            cycleMs = cycleMin * 60_000.0;

            if (PM_Settlements.CheckIfTileIsInUse(req.Tile) || SiteManagerHelper.CheckIfTileIsInUse(req.Tile))
            {
                RespondCustomInfo(client, data, "That tile is already in use.");
                return;
            }

            if (req.AccessMode == SiteAccessMode.GuildOnly && string.IsNullOrWhiteSpace(client.UserFile.GuildName))
            {
                RespondCustomInfo(client, data, "You must be in a guild to create a guild-only site.");
                return;
            }

            // KMH: Store base cap only; the SiteMaxWorkers guild perk is
            // added live by ResolveEffectiveMaxWorkers so post-build perk
            // purchases retroactively benefit existing sites.
            int maxWorkers = 5;

            SiteFile siteFile = new SiteFile();
            siteFile.Tile = req.Tile;
            // Resolve the friendly item label once and reuse it for
            // every user-visible string the site emits (description, status
            // popup, server console log). Falls back to the humanizer if the
            // cache hasn't been populated yet for this defName.
            string itemLabel = GameServer.Managers.ItemLabelCache.LabelFor(req.ItemDefName);

            siteFile.Username = username;
            siteFile.GuildName = client.UserFile.GuildName ?? string.Empty;
            siteFile.Type = new SiteType
            {
                DefName = "RTCustomOutpost",
                Cost = cost,
                Description = $"Custom site producing {req.AmountPerCycle}x {itemLabel} every {cycleMin} min",
                CycleTimeMinutes = cycleMin,
                IsCustom = true,
                CreatedBy = username,
                Rewards = new SiteReward[] { new SiteReward { DefName = req.ItemDefName, Amount = req.AmountPerCycle } }
            };

            string relevantSkill = CustomSiteData.DetermineRelevantSkill(req.ItemDefName);

            CustomSiteData customData = new CustomSiteData
            {
                ItemDefName = req.ItemDefName,
                BaseAmountPerCycle = req.AmountPerCycle,
                MarketValuePerUnit = marketValue,
                BaseCycleTimeMs = cycleMs,
                AccessMode = req.AccessMode,
                OwnerTaxPercent = req.OwnerTaxPercent,
                MaxWorkers = maxWorkers,
                LastRewardUtcTicks = DateTime.UtcNow.Ticks,
                RelevantSkillDef = relevantSkill,
                OwnerRewardDestination = req.OwnerRewardDestination,
                MarketplaceUnitPrice = Math.Max(1, req.MarketplaceUnitPrice)
            };
            // Don't auto-add the owner as a worker at build
            // time. The previous behaviour permanently kept the owner in
            // cd.Workers from the moment the site was created — which:
            //   * Made the site UI always show "Workers: 1/5 — [OWNER] L0"
            //     even on a freshly built site with no pawn assigned.
            //   * Made it impossible to "fully remove" the worker — retrieving
            //     the assigned pawn cleared WorkerString but couldn't clear
            //     the cd.Workers entry, leaving a ghost worker.
            //
            // New behaviour: cd.Workers starts empty. Owners (and everyone
            // else) only become workers by actually assigning a pawn via
            // the Assign Pawn gizmo — and they're removed from cd.Workers
            // the moment they retrieve that pawn.
            if (customData.WorkerProgress == null)
                customData.WorkerProgress = new Dictionary<string, Shared.Files.Economy.WorkerProgress>(StringComparer.OrdinalIgnoreCase);

            ConfirmNewSite(client, siteFile);

            string customPath = Path.Combine(Master.SitesPath, $"{req.Tile}_custom.json");
            SiteManagerHelper.SaveCustomSiteData(customPath, customData);

            data._statusMessage = $"Custom site built!\nProducing: {req.AmountPerCycle}x {itemLabel}\nCycle: {cycleMin} min | Cost: {cost} silver\nRelevant Skill: {relevantSkill}\nMore workers with high {relevantSkill} skill = faster production!";
            data._calculatedCost = cost;
            data._calculatedCycleMinutes = cycleMin;
            data._customData = customData;
            data._stepMode = SiteStepMode.CustomInfo;
            client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);

            Printer.Warning($"[CustomSite] {username} built custom site at tile {req.Tile}: {req.AmountPerCycle}x {req.ItemDefName} (cost: {cost}, cycle: {cycleMin}min)");

            GameServer.Integrations.Discord.DiscordAnnouncer.CustomSiteBuilt(
                username, req.ItemDefName, req.AmountPerCycle, cycleMin, cost);

            // Lifetime stats for the player leaderboard.
            try { GameServer.Managers.PlayerStatsManager.RecordSiteBuilt(username); } catch { }
        }
    }
}
