using GameServer.Core;
using GameServer.Managers;
using Shared;
using Shared.Files.Economy;
using Shared.Files.Sites;
using Shared.Misc;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static TCPNetwork.Packets.PKT_Site;

namespace GameServer.PacketManager
{
    /// <summary>
    /// Worker join/leave for custom sites.
    ///
    /// The previous "upgrade" flow was removed in favour of building additional
    /// sites — capacity scales horizontally now. Worker skill is no longer
    /// asserted by the client; it grows from cycles served (see WorkerProgress
    /// and PM_Sites.Rewards).
    /// </summary>
    public partial class PM_Sites
    {
        private static void HandleWorkerJoin(ServerClient client, PKT_Site data)
        {
            int tile = data._file?.Tile ?? -1;
            if (tile < 0) return;

            string customPath = Path.Combine(Master.SitesPath, $"{tile}_custom.json");
            CustomSiteData customData = SiteManagerHelper.LoadCustomSiteData(customPath);
            if (customData == null)
            {
                RespondCustomInfo(client, data, "Not a custom site.");
                return;
            }

            try
            {
                SiteFile siteFile = SiteManagerHelper.GetSiteFileFromTile(tile);

                string username = client.UserFile.Username;
                if (customData.AccessMode == SiteAccessMode.Private)
                {
                    RespondCustomInfo(client, data, "This site is private.");
                    return;
                }

                if (customData.AccessMode == SiteAccessMode.GuildOnly)
                {
                    if (string.IsNullOrEmpty(client.UserFile.GuildName) ||
                        client.UserFile.GuildName != siteFile?.GuildName)
                    {
                        RespondCustomInfo(client, data, "This site is guild-only. You're not in the right guild.");
                        return;
                    }
                }

                // KMH: Case-insensitive duplicate check — keeps worker list
                // in sync with the case-insensitive WorkerProgress dict.
                if (customData.Workers.Any(w => string.Equals(w, username, StringComparison.OrdinalIgnoreCase)))
                {
                    RespondCustomInfo(client, data, "You're already a worker at this site.");
                    return;
                }

                // KMH: Effective worker cap = stored base + the owner-guild's
                // current SiteMaxWorkers perk bonus. Letting it be live means
                // new perk purchases benefit existing sites too.
                int effectiveMax = ResolveEffectiveMaxWorkers(customData, siteFile);
                if (customData.Workers.Count >= effectiveMax)
                {
                    RespondCustomInfo(client, data, $"Site is full ({customData.Workers.Count}/{effectiveMax} workers).");
                    return;
                }

                customData.Workers.Add(username);

                // Track tenure + start the worker at level 0; XP grows on each cycle.
                if (customData.WorkerProgress == null)
                    customData.WorkerProgress = new Dictionary<string, WorkerProgress>(StringComparer.OrdinalIgnoreCase);

                customData.WorkerProgress[username] = new WorkerProgress
                {
                    JoinedUtcTicks = DateTime.UtcNow.Ticks,
                    Xp = 0,
                    CyclesCompleted = 0,
                    Destination = RewardDestination.Caravan
                };

                SiteManagerHelper.SaveCustomSiteData(customPath, customData);

                double effectiveMin = customData.GetEffectiveCycleTimeMs() / 60000.0;
                double skillEff = customData.GetSkillEfficiency();
                data._statusMessage = $"Joined as worker! Workers: {customData.Workers.Count}/{effectiveMax}\nSpeed: {customData.GetSpeedMultiplier():F1}x | Skill Eff: {skillEff:F0}% | Total: {customData.GetTotalProductionMultiplier():F1}x\nEffective Cycle: {(int)effectiveMin} min\nYou will gain {customData.RelevantSkillDef} XP each completed cycle.";
                data._customData = customData;
                data._stepMode = SiteStepMode.CustomInfo;
                client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);

                Printer.Warning($"[CustomSite] {username} joined site at tile {tile} as worker ({customData.Workers.Count}/{effectiveMax})");

                // KMH: Light chat notification to site owner + other workers
                // so they see the change without polling.
                NotifyStakeholders(siteFile, customData,
                    $"<color=#80ff80>+ {username}</color> joined site at tile {tile} ({customData.Workers.Count}/{effectiveMax} workers)",
                    excludeUsername: username);
            }
            catch (Exception e)
            {
                RespondCustomInfo(client, data, $"Failed to join: {e.Message}");
            }
        }

        /// <summary>
        /// Owner or worker switches their personal reward destination at this site.
        /// Owner updates the site's <see cref="CustomSiteData.OwnerRewardDestination"/>;
        /// workers update their <see cref="WorkerProgress.Destination"/> slot.
        /// </summary>
        public static void HandleWorkerSetDestination(ServerClient client, PKT_Site data, RewardDestination newDestination)
        {
            int tile = data._file?.Tile ?? -1;
            if (tile < 0) return;

            string customPath = Path.Combine(Master.SitesPath, $"{tile}_custom.json");
            CustomSiteData cd = SiteManagerHelper.LoadCustomSiteData(customPath);
            if (cd == null) return;

            string username = client.UserFile?.Username;
            if (string.IsNullOrEmpty(username)) return;

            SiteFile siteFile = SiteManagerHelper.GetSiteFileFromTile(tile);

            if (siteFile != null && siteFile.Username == username)
            {
                cd.OwnerRewardDestination = newDestination;
                SiteManagerHelper.SaveCustomSiteData(customPath, cd);
                RespondCustomInfo(client, data, $"Owner reward destination set to {newDestination}.");
                return;
            }

            if (cd.WorkerProgress != null && cd.WorkerProgress.TryGetValue(username, out WorkerProgress wp))
            {
                wp.Destination = newDestination;
                SiteManagerHelper.SaveCustomSiteData(customPath, cd);
                RespondCustomInfo(client, data, $"Your reward destination set to {newDestination}.");
            }
        }

        private static void HandleWorkerLeave(ServerClient client, PKT_Site data)
        {
            int tile = data._file?.Tile ?? -1;
            if (tile < 0) return;

            string customPath = Path.Combine(Master.SitesPath, $"{tile}_custom.json");
            CustomSiteData customData = SiteManagerHelper.LoadCustomSiteData(customPath);
            if (customData == null) return;

            try
            {
                string username = client.UserFile.Username;

                if (!customData.Workers.Contains(username))
                {
                    RespondCustomInfo(client, data, "You're not a worker at this site.");
                    return;
                }

                // KMH: Case-insensitive remove (mirrors the case-insensitive
                // duplicate check on join).
                customData.Workers.RemoveAll(w => string.Equals(w, username, StringComparison.OrdinalIgnoreCase));
                customData.WorkerProgress?.Remove(username);
                SiteManagerHelper.SaveCustomSiteData(customPath, customData);

                int effectiveMax = ResolveEffectiveMaxWorkers(customData, SiteManagerHelper.GetSiteFileFromTile(tile));
                data._statusMessage = $"Left the site. Workers: {customData.Workers.Count}/{effectiveMax}.";
                data._customData = customData;
                data._stepMode = SiteStepMode.CustomInfo;
                client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);

                Printer.Warning($"[CustomSite] {username} left site at tile {tile}");

                // KMH: Notify owner + remaining workers.
                SiteFile siteFileForNotify = SiteManagerHelper.GetSiteFileFromTile(tile);
                NotifyStakeholders(siteFileForNotify, customData,
                    $"<color=#ff8080>- {username}</color> left site at tile {tile} ({customData.Workers.Count}/{effectiveMax} workers)",
                    excludeUsername: username);
            }
            catch { }
        }

        /// <summary>
        /// KMH: Sends a server chat message to the site owner + all current
        /// workers (excluding one optional username — typically the actor).
        /// Uses the existing chat infrastructure so the message styles
        /// consistently with other server notifications.
        /// </summary>
        private static void NotifyStakeholders(SiteFile site, CustomSiteData cd, string message, string excludeUsername = null)
        {
            if (string.IsNullOrEmpty(message)) return;

            HashSet<string> recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (site != null && !string.IsNullOrEmpty(site.Username)) recipients.Add(site.Username);
            if (cd?.Workers != null)
            {
                foreach (string w in cd.Workers)
                    if (!string.IsNullOrEmpty(w)) recipients.Add(w);
            }
            if (!string.IsNullOrEmpty(excludeUsername)) recipients.Remove(excludeUsername);

            foreach (string username in recipients)
            {
                ServerClient sc = GameServer.Hooks.TCPNetwork.ServerNetwork.GetConnectedClientFromUsername(username);
                if (sc != null)
                {
                    try { PM_Chat.SendServerMessage(sc, message); }
                    catch { }
                }
            }
        }

        /// <summary>
        /// KMH: Computes the live worker cap for a site = stored base
        /// (<c>customData.MaxWorkers</c>) + the owner-guild's
        /// SiteMaxWorkers perk bonus, if any.
        ///
        /// Done dynamically so a guild buying the perk after a site exists
        /// retroactively expands that site's capacity.
        /// </summary>
        public static int ResolveEffectiveMaxWorkers(CustomSiteData cd, SiteFile siteFile)
        {
            int baseMax = cd?.MaxWorkers ?? 5;
            string ownerGuild = siteFile?.GuildName;
            if (string.IsNullOrEmpty(ownerGuild)) return baseMax;
            try { return baseMax + GuildManager.GetMaxWorkerBonus(ownerGuild); }
            catch { return baseMax; }
        }
    }
}
