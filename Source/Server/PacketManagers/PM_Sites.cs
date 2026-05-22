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
    /// <summary>
    /// Site packet entry point. Routes incoming PKT_Site by step mode.
    ///
    /// Standard build/destroy/config and shared helpers live in this file.
    /// Custom-site flow is in <c>Sites/PM_Sites.CustomBuild.cs</c>,
    /// worker join/leave/upgrade in <c>Sites/PM_Sites.Workers.cs</c>,
    /// info responses in <c>Sites/PM_Sites.Info.cs</c>, and reward
    /// distribution in <c>Sites/PM_Sites.Rewards.cs</c>.
    /// </summary>
    public partial class PM_Sites : PM_Base
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

                case SiteStepMode.SetDestination:
                    HandleWorkerSetDestination(client, data,
                        (Shared.Files.Economy.RewardDestination)data._newRewardDestination);
                    break;
            }
        }

        public static void ConfirmNewSite(ServerClient client, SiteFile siteFile)
        {
            siteFile.SaveSite();
            SiteManagerHelper.InvalidateCache();

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
            if (siteData == null || siteData._file == null || siteData._file.Type == null)
                return;

            if (siteData._file.Tile < 0)
                return;

            if (PM_Settlements.CheckIfTileIsInUse(siteData._file.Tile))
            {
                PKT_Site response = new PKT_Site();
                response._stepMode = SiteStepMode.CustomInfo;
                response._statusMessage = $"That tile already has a settlement.";
                client.Listener.EnqueuePacket(PacketHeader.SiteManager, response);
                return;
            }

            if (SiteManagerHelper.CheckIfTileIsInUse(siteData._file.Tile))
            {
                PKT_Site response = new PKT_Site();
                response._stepMode = SiteStepMode.CustomInfo;
                response._statusMessage = $"That tile already has a site.";
                client.Listener.EnqueuePacket(PacketHeader.SiteManager, response);
                return;
            }

            SiteFile siteFile = new SiteFile();
            siteFile.Tile = siteData._file.Tile;
            siteFile.Username = client.UserFile.Username;
            siteFile.Type = SiteManagerHelper.GetTypeFromDef(siteData._file.Type.DefName);

            if (siteFile.Type == null)
            {
                Printer.Warning($"[Sites] {client.UserFile.Username} requested unknown site def '{siteData._file.Type.DefName}'.");
                return;
            }

            if (!string.IsNullOrEmpty(client.UserFile.GuildName))
                siteFile.GuildName = client.UserFile.GuildName;

            ConfirmNewSite(client, siteFile);

            // Lifetime stats — every standard site built counts.
            try { PlayerStatsManager.RecordSiteBuilt(client.UserFile.Username); } catch { }
        }

        private static void DestroySite(ServerClient client, PKT_Site siteData)
        {
            SiteFile siteFile = SiteManagerHelper.GetSiteFileFromTile(siteData._file.Tile);
            if (siteFile == null) return;
            if (siteFile.Username == client.UserFile.Username) DestroySiteFromFile(siteFile);
            else ResponseShortcutManager.SendNoPowerPacket(client);
        }

        public static void DestroySiteFromFile(SiteFile siteFile)
        {
            PKT_Site siteData = new PKT_Site();
            siteData._stepMode = SiteStepMode.Destroy;
            siteData._file = siteFile;

            ServerNetwork.SendPacketToAllClients(PacketHeader.SiteManager, siteData);

            try
            {
                File.Delete(Path.Combine(Master.SitesPath, siteFile.Tile + CommonValues.DefaultSaveFormat));
            }
            catch (Exception e) { Printer.Warning($"[Sites] Failed to delete site file: {e}"); }

            try
            {
                string customPath = Path.Combine(Master.SitesPath, $"{siteFile.Tile}_custom.json");
                if (File.Exists(customPath)) File.Delete(customPath);
            }
            catch { }

            SiteManagerHelper.InvalidateCache();
            InformationDisplayer.DisplayRemoveSite(siteFile.Tile.ToString());
        }

        private static void ManageWorker(ServerClient client, PKT_Site data)
        {
            SiteFile site = SiteManagerHelper.GetSiteFileFromTile(data._file.Tile);
            if (site == null) return;
            if (site.Username != client.UserFile.Username) return;

            bool assigning = !string.IsNullOrEmpty(data._file.WorkerString);
            site.WorkerString = data._file.WorkerString;
            site.SaveSite();
            SiteManagerHelper.InvalidateCache();

            // If this site has custom-site data, ALSO sync
            // the cd.Workers list and per-worker progress. Previously the
            // standard ManageWorker path only touched WorkerString, leaving
            // the custom site's reward-distribution loop unable to recognise
            // newly-assigned pawns — workers stayed at level 0 forever and
            // retrieval never cleared the worker list. Now:
            //   * Assign → caller added to cd.Workers, BaseSkillLevel stamped
            //     from the client-supplied pawn skill (clamped 0-20).
            //     A fresh-or-existing WorkerProgress is created.
            //   * Retrieve → caller removed from cd.Workers (their XP +
            //     BaseSkillLevel stay in cd.WorkerProgress so re-joining
            //     resumes where they left off).
            try
            {
                string username = client.UserFile.Username;
                string customPath = System.IO.Path.Combine(Master.SitesPath, $"{site.Tile}_custom.json");
                if (!System.IO.File.Exists(customPath)) return;

                Shared.Files.Sites.CustomSiteData cd = SiteManagerHelper.LoadCustomSiteData(customPath);
                if (cd == null) return;

                if (cd.Workers == null)
                    cd.Workers = new System.Collections.Generic.List<string>();
                if (cd.WorkerProgress == null)
                    cd.WorkerProgress = new System.Collections.Generic.Dictionary<string, Shared.Files.Economy.WorkerProgress>(System.StringComparer.OrdinalIgnoreCase);

                if (assigning)
                {
                    // Respect MaxWorkers cap.
                    int effectiveMax = ResolveEffectiveMaxWorkers(cd, site);
                    bool alreadyIn = cd.Workers.Contains(username);
                    if (!alreadyIn)
                    {
                        if (cd.Workers.Count >= effectiveMax)
                        {
                            // Hit the cap. Don't add. (Caller still got their
                            // WorkerString slot — but they won't accumulate
                            // XP or count for production. Mostly defensive;
                            // the client UI usually filters this case.)
                        }
                        else
                        {
                            cd.Workers.Add(username);
                        }
                    }

                    // Server-clamp the client-supplied skill to 0..20.
                    int claimedSkill = data._workerSkillLevel;
                    if (claimedSkill < 0) claimedSkill = 0;
                    if (claimedSkill > 20) claimedSkill = 20;

                    if (!cd.WorkerProgress.TryGetValue(username, out var wp) || wp == null)
                    {
                        wp = new Shared.Files.Economy.WorkerProgress
                        {
                            JoinedUtcTicks = DateTime.UtcNow.Ticks,
                            Destination = (username == site.Username)
                                ? cd.OwnerRewardDestination
                                : Shared.Files.Economy.RewardDestination.Caravan
                        };
                        cd.WorkerProgress[username] = wp;
                    }
                    // Update BaseSkillLevel — take the higher of the existing
                    // and the new claim (so swapping in a higher-skill pawn
                    // upgrades the base, but swapping a lower-skill one in
                    // doesn't downgrade what they've already earned).
                    if (claimedSkill > wp.BaseSkillLevel) wp.BaseSkillLevel = claimedSkill;
                }
                else
                {
                    // Retrieval — remove from active workers list.
                    // Keep cd.WorkerProgress[username] so XP/BaseSkillLevel
                    // persists for next time they assign.
                    cd.Workers.RemoveAll(w => string.Equals(w, username, System.StringComparison.OrdinalIgnoreCase));
                }

                SiteManagerHelper.SaveCustomSiteData(customPath, cd);
            }
            catch (Exception e)
            {
                Printer.Warning($"[Sites] ManageWorker custom-site sync failed: {e}");
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

        // Shared helper used by all custom-site flows.
        private static void RespondCustomInfo(ServerClient client, PKT_Site data, string message)
        {
            data._statusMessage = message;
            data._stepMode = SiteStepMode.CustomInfo;
            client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);
        }
    }
}
