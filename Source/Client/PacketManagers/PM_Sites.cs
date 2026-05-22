using GameClient.Defs;
using GameClient.Dialogs;
using GameClient.Dialogs.Default;
using GameClient.Managers;
using GameClient.Misc;
using GameClient.WorldObjects;
using RimWorld;
using RimWorld.Planet;
using Shared;
using Shared.Files.Sites;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;
using Verse;
using static Shared.CommonEnumerators;
using static Shared.Misc.Printer;
using static TCPNetwork.Packets.PKT_Site;

namespace GameClient.PacketManagers
{
    /// <summary>
    /// Site packet entry point + standard build / destroy / info handling
    /// + the periodic site-rewards request loop.
    ///
    /// Custom-site requests + pending-build payment tracking live in
    /// <c>Sites/PM_Sites.CustomSites.cs</c>.
    /// </summary>
    public partial class PM_Sites : PM_Base
    {
        public static List<SiteType> SiteValues { get; set; }

        public static List<WO_Site> PlayerSites { get; set; } = new List<WO_Site>();

        private static CancellationTokenSource Token { get; set; } = new CancellationTokenSource();

        public static double RewardDelay { get; set; } = -1;

        [HandlesPacket(PacketHeader.SiteManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_Site data = Serializer.ConvertBytesToObject<PKT_Site>(bytes);

            switch (data._stepMode)
            {
                case SiteStepMode.Accept:
                    OnSiteAccept();
                    break;

                case SiteStepMode.Info:
                    OnSiteInfo(data._file);
                    break;

                case SiteStepMode.Build:
                    OnSiteBuild(data._file);
                    break;

                case SiteStepMode.Destroy:
                    OnSiteDestroy(data._file);
                    break;

                case SiteStepMode.Rewards:
                    OnReceiveRewards(data._rewardFiles);
                    break;

                // KMH: Custom site info/status response
                case SiteStepMode.CustomInfo:
                    ReceiveCustomSiteInfo(data);
                    break;
            }
        }

        public static void RequestSiteBuild(SiteType configFile)
        {
            if (!RimworldManager.CheckIfHasEnoughItemInCaravan(SessionHandler.ChosenCaravan, ThingDefOf.Silver.defName, configFile.Cost))
            {
                DLG_Base.PushNewDialog(new DLG_Message("ERROR", new string[] { "You do not have enough silver!" }));
                return;
            }

            // Do NOT deduct silver yet.
            // Wait until the server accepts the site build.
            BeginPendingStandardSiteBuild(configFile.Cost, SessionHandler.ChosenCaravan);

            PKT_Site siteData = new PKT_Site();
            siteData._stepMode = SiteStepMode.Build;
            siteData._file.Tile = SessionHandler.ChosenCaravan.Tile;
            siteData._file.Type.DefName = configFile.DefName;

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.SiteManager, siteData);

            DLG_Base.PushNewDialog(new DLG_Wait());
        }

        public static void RequestDestroySite()
        {
            Action r1 = delegate
            {
                PKT_Site siteData = new PKT_Site();
                siteData._file.Tile = SessionHandler.ChosenSite.Tile;
                siteData._stepMode = SiteStepMode.Destroy;

                Network.ServerEndpoint.EnqueuePacket(PacketHeader.SiteManager, siteData);
            };

            DLG_YesNo d1 = new DLG_YesNo("Are you sure you want to destroy this site?", r1, null);
            DLG_Base.PushNewDialog(d1);
        }

        public static void RequestSiteChangeConfig(SiteType config, string reward)
        {
            PKT_SiteRewardConfig rewardConfig = new PKT_SiteRewardConfig();
            rewardConfig._siteDef = config.DefName;
            rewardConfig._rewardDef = reward;

            PKT_Site siteData = new PKT_Site();
            siteData._stepMode = SiteStepMode.Config;
            siteData._rewardConfig = rewardConfig;

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.SiteManager, siteData);
        }

        private static void OnReceiveRewards(SiteReward[] files)
        {
            List<Thing> rewards = new List<Thing>();
            foreach (SiteReward reward in files)
            {
                try
                {
                    // KMH: GetNamedSilentFail is O(1) vs the previous O(N) AllDefs scan.
                    ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(reward.DefName);
                    if (def == null) continue;

                    Thing toMake = ThingMaker.MakeThing(def);
                    toMake.stackCount = reward.Amount;
                    toMake.HitPoints = def.BaseMaxHitPoints;
                    rewards.Add(toMake);

                    Printer.Message($"Received {reward.Amount} of {reward.DefName}", LogImportanceMode.Verbose);
                }
                catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }
            }

            if (rewards.Count > 0)
            {
                Map map = Find.AnyPlayerHomeMap;
                IntVec3 position = RimworldManager.GetTransferLocationInMap(map);
                foreach (Thing thing in rewards) RimworldManager.PlaceThingIntoMap(thing, map, position, true);

                RimworldManager.GenerateLetter("Site rewards", $"You've received your site rewards", LetterDefOf.PositiveEvent);
                Printer.Message("Rewards delivered", LogImportanceMode.Verbose);
            }
        }

        public static void AddSites(List<SiteFile> sites)
        {
            foreach (SiteFile toAdd in sites)
            {
                OnSiteBuild(toAdd);
            }
        }

        public static void ClearAllSites()
        {
            PlayerSites.Clear();

            foreach (WorldObject site in Finder.GetAllRTSites())
            {
                SiteFile siteFile = new SiteFile();
                siteFile.Tile = site.Tile;
                OnSiteDestroy(siteFile);
            }
        }

        public static void OnSiteBuild(SiteFile toAdd)
        {
            if (toAdd == null || toAdd.Type == null)
            {
                Printer.Error("Failed to spawn site because SiteFile or SiteType was null.");
                return;
            }

            if (Find.WorldObjects.Sites.FirstOrDefault(fetch => fetch.Tile == toAdd.Tile) != null)
                return;

            try
            {
                SitePartDef siteDef = RTSitePartDefs.GetByDefName(toAdd.Type.DefName);
                if (siteDef == null)
                {
                    Printer.Error($"Failed to spawn site at {toAdd.Tile}. Missing SitePartDef '{toAdd.Type.DefName}'.");
                    return;
                }

                WorldObjectDef rtSiteDef = DefDatabase<WorldObjectDef>.GetNamedSilentFail("RTSite");
                if (rtSiteDef == null)
                {
                    Printer.Error($"Failed to spawn site at {toAdd.Tile}. WorldObjectDef 'RTSite' is not registered.");
                    return;
                }

                WO_Site site = (WO_Site)WorldObjectMaker.MakeWorldObject(rtSiteDef);

                if (site == null)
                {
                    Printer.Error($"Failed to spawn site at {toAdd.Tile}. WorldObjectDef 'RTSite' was null.");
                    return;
                }

                site.Tile = toAdd.Tile;
                site.SetFaction(PlanetManagerHelper.GetPlayerFactionFromGoodwill(toAdd.Goodwill));

                RTSitePart part = new RTSitePart(site, siteDef);
                site.AddPart(part);

                if (site.parts == null || site.parts.Count == 0 || site.MainSitePartDef == null)
                {
                    Printer.Error($"Failed to spawn site at {toAdd.Tile}. Site part was not added correctly.");
                    return;
                }

                PlayerSites.Add(site);
                Find.WorldObjects.Add(site);
            }
            catch (Exception e)
            {
                Printer.Error($"Failed to spawn site at {toAdd.Tile}. Reason: {e}");
            }
        }

        public static void OnSiteDestroy(SiteFile toRemove)
        {
            try
            {
                WO_Site toGet = Finder.GetRTSiteFromTile(toRemove.Tile);
                if (!RimworldManager.CheckIfMapHasPlayerPawns(toGet.Map))
                {
                    if (PlayerSites.Contains(toGet)) PlayerSites.Remove(toGet);
                    Find.WorldObjects.Remove(toGet);
                }
                else Printer.Warning($"Ignored removal of site at {toGet.Tile} because player was inside");
            }
            catch (Exception e) { Printer.Error($"Failed to remove site at {toRemove.Tile}. Reason: {e}"); }
        }

        public static void RecalculateSiteGoodwill(WO_Site site, Goodwill goodwill)
        {
            SiteFile file = new SiteFile();
            file.Tile = site.Tile;
            file.Goodwill = goodwill;
            file.Type = SiteValues.FirstOrDefault(fetch => fetch.DefName == site.MainSitePartDef.defName);

            OnSiteDestroy(file);
            OnSiteBuild(file);
        }

        private static void OnSiteAccept()
        {
            FinalizePendingStandardSiteBuild();
            RimworldManager.GenerateLetter("Site built", $"You've built a site!", LetterDefOf.PositiveEvent);
            DLG_Wait.Instance.Close();
            PM_Saves.ForceSave();
        }

        private static void OnSiteInfo(SiteFile file)
        {
            DLG_Wait.Instance.Close();

            Action selectWorker = delegate
            {
                Pawn toSend = SessionHandler.ChosenCaravan.PawnsListForReading.Where(fetch => RimworldManager.CheckIfThingIsHuman(fetch)).ToList()
                    [DLG_ListingWithButton.ResultInt];

                PKT_Site siteData = new PKT_Site();
                siteData._stepMode = SiteStepMode.Worker;
                siteData._file.Tile = SessionHandler.ChosenSite.Tile;
                siteData._file.WorkerString = ScribeManager.SerializeToString(toSend, ScribeManager.SerializableType.Thing);

                // KMH 26.5.20.1: Read the pawn's best production skill and
                // send it alongside the assign packet. The server stamps
                // it as WorkerProgress.BaseSkillLevel so a freshly-assigned
                // Crafting-15 pawn starts contributing at level 15 instead
                // of L0 — matches what the player expects. Server clamps
                // 0..20 so a modded client can't inflate beyond a sane max.
                siteData._workerSkillLevel = GetBestProductionSkillLevel(toSend);

                SessionHandler.ChosenCaravan.RemovePawn(toSend);
                Find.WorldPawns.RemovePawn(toSend);
                toSend.Destroy();

                Network.ServerEndpoint.EnqueuePacket(PacketHeader.SiteManager, siteData);
                PM_Saves.ForceSave();
            };

            Action retrieveWorker = delegate
            {
                Pawn toRetrieve = ScribeManager.SerializeFromString<Pawn>(file.WorkerString, ScribeManager.SerializableType.Pawn);
                RimworldManager.PlaceThingIntoCaravan(toRetrieve, SessionHandler.ChosenCaravan);

                PKT_Site siteData = new PKT_Site();
                siteData._stepMode = SiteStepMode.Worker;
                siteData._file.Tile = SessionHandler.ChosenSite.Tile;

                Network.ServerEndpoint.EnqueuePacket(PacketHeader.SiteManager, siteData);
            };

            if (file.WorkerString == null)
            {
                List<string> contents = new List<string>();
                foreach (Pawn pawn in SessionHandler.ChosenCaravan.PawnsListForReading.Where(fetch => RimworldManager.CheckIfThingIsHuman(fetch)))
                {
                    contents.Add(pawn.LabelCap);
                }

                string title = "Available pawns";
                string description = "Choose the pawn you want to send as a worker";
                DLG_Base.PushNewDialog(new DLG_ListingWithButton(title, description, contents.ToArray(), selectWorker, null));
            }
            else { DLG_Base.PushNewDialog(new DLG_YesNo("Do you want to retrieve the worker from the site?", retrieveWorker)); }
        }

        /// <summary>
        /// KMH 26.5.20.1: Return the maximum skill level across a pawn's
        /// "production-flavoured" skills — Crafting, Mining, Cooking,
        /// Construction, Plants, Animals. This is what we pass to the
        /// server when assigning a pawn to a custom site, so the server
        /// can stamp the worker's BaseSkillLevel. We pick the MAX across
        /// these because the client doesn't know which RimWorld skill the
        /// site declared as relevant — the server picks the right one.
        ///
        /// Returns 0 on any error (null pawn, no skill tracker, etc.).
        /// </summary>
        private static int GetBestProductionSkillLevel(Pawn pawn)
        {
            try
            {
                if (pawn?.skills?.skills == null) return 0;
                string[] productionDefs = new[]
                {
                    "Crafting", "Mining", "Cooking", "Construction", "Plants", "Animals",
                    "Artistic", "Intellectual" // Intellectual covers tech, Artistic covers high-value crafts.
                };
                int best = 0;
                foreach (var skill in pawn.skills.skills)
                {
                    if (skill?.def?.defName == null) continue;
                    bool isProduction = false;
                    foreach (string d in productionDefs)
                    {
                        if (skill.def.defName == d) { isProduction = true; break; }
                    }
                    if (!isProduction) continue;
                    int lvl = skill.Level;
                    if (lvl > best) best = lvl;
                }
                if (best < 0) best = 0;
                if (best > 20) best = 20;
                return best;
            }
            catch { return 0; }
        }

        [OnSessionStart]
        private static void StartTickingSites()
        {
            Token = new CancellationTokenSource();
            double currentRewardDelay = 0;
            int tickDuration = 100;
            Task.Run(async () =>
            {
                while (!Token.Token.IsCancellationRequested)
                {
                    if (currentRewardDelay >= RewardDelay)
                    {
                        MainThreadHandler.Instance.Enqueue(AskForSiteRewards);
                        currentRewardDelay = 0;
                    }

                    else
                    {
                        await Task.Delay(tickDuration, Token.Token);
                        currentRewardDelay += tickDuration;
                    }
                }
            });
        }

        [OnSessionEnd]
        private static void StopTickingSites()
        {
            Token.Cancel();
            ClearPendingCustomSiteBuild();
            ClearPendingStandardSiteBuild();
        }

        public static void AskForSiteRewards()
        {
            PKT_Site siteData = new PKT_Site();
            siteData._stepMode = SiteStepMode.Rewards;

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.SiteManager, siteData);
        }

        public static void AskForInformation()
        {
            PKT_Site siteData = new PKT_Site();
            siteData._stepMode = SiteStepMode.Info;
            siteData._file.Tile = SessionHandler.ChosenSite.Tile;

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.SiteManager, siteData);
        }

        public static void SetValues()
        {
            PM_Sites.SiteValues = SessionHandler.GlobalData._siteValues;
            PM_Sites.RewardDelay = SessionHandler.GlobalData._actionValues.SiteAction.TimeInterval;
        }
    }
}
