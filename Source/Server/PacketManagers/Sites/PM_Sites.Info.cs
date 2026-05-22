using GameServer.Core;
using Shared;
using Shared.Files.Sites;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static TCPNetwork.Packets.PKT_Site;

namespace GameServer.PacketManager
{
    /// <summary>
    /// Custom-site info responder. Renders a human-readable summary that the
    /// client surfaces in DLG_Message after a CustomInfo request.
    /// </summary>
    public partial class PM_Sites
    {
        private static void HandleCustomSiteInfo(ServerClient client, PKT_Site data)
        {
            int tile = data._file?.Tile ?? -1;
            if (tile < 0) return;

            string customPath = Path.Combine(Master.SitesPath, $"{tile}_custom.json");
            SiteFile siteFile = SiteManagerHelper.GetSiteFileFromTile(tile);

            if (!File.Exists(customPath))
            {
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
                CustomSiteData cd = SiteManagerHelper.LoadCustomSiteData(customPath);
                if (cd == null)
                {
                    RespondCustomInfo(client, data, "Failed to load custom site data.");
                    return;
                }
                data._customData = cd;

                double speedMult = cd.GetSpeedMultiplier();
                double skillEff = cd.GetSkillEfficiency();
                double totalMult = cd.GetTotalProductionMultiplier();
                int effectiveAmount = (int)Math.Ceiling(cd.BaseAmountPerCycle * totalMult);
                double effectiveCycleMin = cd.GetEffectiveCycleTimeMs() / 60000.0;
                double baseCycleMin = cd.BaseCycleTimeMs / 60000.0;

                // KMH 2.7: Use ItemLabelCache so the report shows "Power armor"
                // not "Apparel_PowerArmor". Falls back to humanized defName for
                // anything the cache hasn't seen.
                string itemLabel = GameServer.Managers.ItemLabelCache.LabelFor(cd.ItemDefName);

                var sb = new System.Text.StringBuilder(512);
                sb.AppendLine($"=== Custom Site (KMH) ===");
                sb.AppendLine($"Item: {itemLabel} x{cd.BaseAmountPerCycle}/cycle");
                string ownerName = siteFile?.Username ?? "Unknown";
                sb.AppendLine($"Owner: {ownerName}");
                string guildName = string.IsNullOrEmpty(siteFile?.GuildName) ? "None" : siteFile.GuildName;
                sb.AppendLine($"Guild: {guildName}");
                sb.AppendLine($"Access: {cd.AccessMode}");
                if (cd.AccessMode == SiteAccessMode.Public)
                    sb.AppendLine($"Owner Tax: {cd.OwnerTaxPercent}%");
                sb.AppendLine($"Owner Reward Destination: {cd.OwnerRewardDestination}");
                sb.AppendLine();

                int effectiveMax = ResolveEffectiveMaxWorkers(cd, siteFile);
                sb.AppendLine($"--- Workers ({cd.Workers?.Count ?? 0}/{effectiveMax}) ---");
                if (cd.Workers != null && cd.Workers.Count > 0)
                {
                    foreach (string worker in cd.Workers)
                    {
                        int level = 0;
                        double progress = 0;
                        Shared.Files.Economy.RewardDestination dest = Shared.Files.Economy.RewardDestination.Caravan;
                        if (cd.WorkerProgress != null && cd.WorkerProgress.TryGetValue(worker, out var wp) && wp != null)
                        {
                            level = wp.CurrentLevel;
                            progress = wp.ProgressToNextLevel;
                            dest = wp.Destination;
                        }
                        string ownerTag = (worker == siteFile?.Username) ? " [OWNER]" : "";
                        sb.AppendLine($"  {worker}{ownerTag} — {cd.RelevantSkillDef} L{level} ({progress:P0}) → {dest}");
                    }
                }
                else
                {
                    sb.AppendLine("  No workers assigned");
                }
                sb.AppendLine();

                sb.AppendLine($"--- Production ---");
                sb.AppendLine($"Relevant Skill: {cd.RelevantSkillDef}");
                sb.AppendLine($"Worker Speed: {speedMult:F2}x");
                sb.AppendLine($"Skill Efficiency: {skillEff:P0}");
                sb.AppendLine($"Total Multiplier: {totalMult:F2}x");
                sb.AppendLine($"Effective Output: {effectiveAmount}x {itemLabel}/cycle");
                sb.AppendLine($"Base Cycle: {(int)baseCycleMin} min");
                sb.AppendLine($"Effective Cycle: {(int)effectiveCycleMin} min");

                double valuePerCycle = cd.MarketValuePerUnit * effectiveAmount;
                double cyclesPerHour = effectiveCycleMin > 0 ? 60.0 / effectiveCycleMin : 0;
                sb.AppendLine($"Value: ~${valuePerCycle:F0}/cycle (~${(valuePerCycle * cyclesPerHour):F0}/hr)");

                data._statusMessage = sb.ToString();
                data._calculatedCycleMinutes = (int)baseCycleMin;
            }
            catch (Exception e)
            {
                data._statusMessage = $"Failed to load custom site data: {e.Message}";
            }

            data._stepMode = SiteStepMode.CustomInfo;
            client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);
        }
    }
}
