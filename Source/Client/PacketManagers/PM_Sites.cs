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
    public class PM_Sites : PM_Base
    {
        public static List<SiteType> SiteValues { get; set; }

        public static List<WO_Site> PlayerSites { get; set; } = new List<WO_Site>();

        private static CancellationTokenSource Token { get; set; } = new CancellationTokenSource();

        public static double RewardDelay { get; set; } = -1;

        // KMH: pending custom build payment is charged only after server approval
        private static bool PendingCustomSiteBuild { get; set; } = false;
        private static int PendingCustomSiteTile { get; set; } = -1;
        private static int PendingCustomSiteCost { get; set; } = 0;
        private static Caravan PendingCustomSiteCaravan { get; set; } = null;
        private static bool PendingStandardSiteBuild { get; set; } = false;
        private static int PendingStandardSiteCost { get; set; } = 0;
        private static Caravan PendingStandardSiteCaravan { get; set; } = null;

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

        public static void BeginPendingCustomSiteBuild(int tile, int cost, Caravan caravan)
        {
            PendingCustomSiteBuild = true;
            PendingCustomSiteTile = tile;
            PendingCustomSiteCost = cost;
            PendingCustomSiteCaravan = caravan;
        }

        private static void ClearPendingCustomSiteBuild()
        {
            PendingCustomSiteBuild = false;
            PendingCustomSiteTile = -1;
            PendingCustomSiteCost = 0;
            PendingCustomSiteCaravan = null;
        }

        private static void TryFinalizePendingCustomSiteBuild(PKT_Site data)
        {
            if (!PendingCustomSiteBuild)
                return;

            try
            {
                string status = data?._statusMessage ?? string.Empty;
                bool wasApproved = status.StartsWith("Custom site built!", StringComparison.OrdinalIgnoreCase);

                if (wasApproved)
                {
                    Caravan caravan = PendingCustomSiteCaravan;
                    int cost = PendingCustomSiteCost;

                    if (caravan != null && cost > 0)
                    {
                        bool hasEnough = RimworldManager.CheckIfHasEnoughItemInCaravan(caravan, ThingDefOf.Silver.defName, cost);
                        if (hasEnough)
                        {
                            RimworldManager.RemoveThingFromCaravan(
                                caravan,
                                DefDatabase<ThingDef>.GetNamed(ThingDefOf.Silver.defName),
                                cost);

                            Printer.Message($"Custom site build approved. Deducted {cost} silver.", LogImportanceMode.Verbose);
                        }
                        else
                        {
                            Printer.Warning($"Custom site approved, but caravan no longer had {cost} silver when attempting client-side deduction.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Printer.Warning($"Failed to finalize pending custom site build payment: {e}");
            }
            finally
            {
                // Any CustomInfo response after a pending build request is considered the result for that request.
                ClearPendingCustomSiteBuild();
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
                    ThingDef def = DefDatabase<ThingDef>.AllDefs.FirstOrDefault(fetch => fetch.defName == reward.DefName);
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

                WO_Site site = (WO_Site)WorldObjectMaker.MakeWorldObject(
                    DefDatabase<WorldObjectDef>.AllDefs.FirstOrDefault(fetch => fetch.defName == "RTSite"));

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

        // KMH: Send worker join request - uses caravan pawn selection for skill
        public static void RequestWorkerJoin(int tile)
        {
            try
            {
                Caravan caravan = SessionHandler.ChosenCaravan;
                if (caravan == null)
                {
                    // Use map colonists if no caravan
                    int bestSkill = 0;
                    if (Find.CurrentMap != null)
                    {
                        foreach (Pawn pawn in Find.CurrentMap.mapPawns.FreeColonists)
                        {
                            if (pawn.skills == null) continue;
                            foreach (SkillRecord sr in pawn.skills.skills)
                                if (sr.Level > bestSkill) bestSkill = sr.Level;
                        }
                    }
                    SendWorkerJoinPacket(tile, bestSkill);
                    return;
                }

                // Show pawn selection from caravan
                List<Pawn> humans = caravan.PawnsListForReading
                    .Where(p => RimworldManager.CheckIfThingIsHuman(p)).ToList();

                if (humans.Count == 0)
                {
                    DLG_Base.PushNewDialog(new DLG_Message("Error", new string[] { "No colonists in this caravan." }));
                    return;
                }

                List<string> labels = new List<string>();
                foreach (Pawn p in humans)
                {
                    string skillInfo = "";
                    if (p.skills != null)
                    {
                        int best = 0;
                        string bestName = "";
                        foreach (SkillRecord sr in p.skills.skills)
                        {
                            if (sr.Level > best) { best = sr.Level; bestName = sr.def.defName; }
                        }
                        skillInfo = $" (Best: {bestName} {best})";
                    }
                    labels.Add($"{p.LabelCap}{skillInfo}");
                }

                Action onSelect = delegate
                {
                    int idx = DLG_ListingWithButton.ResultInt;
                    if (idx < 0 || idx >= humans.Count) return;
                    Pawn chosen = humans[idx];
                    int skill = 0;
                    if (chosen.skills != null)
                    {
                        foreach (SkillRecord sr in chosen.skills.skills)
                            if (sr.Level > skill) skill = sr.Level;
                    }
                    SendWorkerJoinPacket(tile, skill);
                };

                DLG_Base.PushNewDialog(new DLG_ListingWithButton(
                    "Select Worker", "Choose a colonist to represent your colony at this site. Their skills affect production efficiency.",
                    labels.ToArray(), onSelect, null));
            }
            catch { }
        }

        private static void SendWorkerJoinPacket(int tile, int skillLevel)
        {
            PKT_Site packet = new PKT_Site();
            packet._stepMode = SiteStepMode.WorkerJoin;
            packet._file = new SiteFile { Tile = tile };
            packet._workerSkillLevel = skillLevel;
            Network.ServerEndpoint.EnqueuePacket(PacketHeader.SiteManager, packet);
        }

        // KMH: Send worker leave request for a custom site
        public static void RequestWorkerLeave(int tile)
        {
            try
            {
                PKT_Site packet = new PKT_Site();
                packet._stepMode = SiteStepMode.WorkerLeave;
                packet._file = new SiteFile { Tile = tile };

                Network.ServerEndpoint.EnqueuePacket(PacketHeader.SiteManager, packet);
            }
            catch { }
        }

        // KMH: Request site upgrade (owner only)
        public static void RequestSiteUpgrade(int tile)
        {
            try
            {
                PKT_Site packet = new PKT_Site();
                packet._stepMode = SiteStepMode.Upgrade;
                packet._file = new SiteFile { Tile = tile };
                Network.ServerEndpoint.EnqueuePacket(PacketHeader.SiteManager, packet);
            }
            catch { }
        }

        // KMH: Request custom site info
        public static void RequestCustomSiteInfo(int tile)
        {
            try
            {
                PKT_Site packet = new PKT_Site();
                packet._stepMode = SiteStepMode.CustomInfo;
                packet._file = new SiteFile { Tile = tile };

                Network.ServerEndpoint.EnqueuePacket(PacketHeader.SiteManager, packet);
            }
            catch { }
        }

        // KMH: Handle custom site info/status response from server
        private static void ReceiveCustomSiteInfo(PKT_Site data)
        {
            // Close any waiting dialog
            try { if (DLG_Wait.Instance != null) DLG_Wait.Instance.Close(); } catch { }

            TryFinalizePendingCustomSiteBuild(data);

            string msg = data._statusMessage ?? "No response from server.";

            DLG_Base.PushNewDialog(new DLG_Message("Site Information", new string[] { msg }));
        }

        private static void BeginPendingStandardSiteBuild(int cost, Caravan caravan)
        {
            PendingStandardSiteBuild = true;
            PendingStandardSiteCost = cost;
            PendingStandardSiteCaravan = caravan;
        }

        private static void ClearPendingStandardSiteBuild()
        {
            PendingStandardSiteBuild = false;
            PendingStandardSiteCost = 0;
            PendingStandardSiteCaravan = null;
        }

        private static void FinalizePendingStandardSiteBuild()
        {
            try
            {
                if (!PendingStandardSiteBuild)
                    return;

                if (PendingStandardSiteCaravan != null && PendingStandardSiteCost > 0)
                {
                    bool hasEnough = RimworldManager.CheckIfHasEnoughItemInCaravan(
                        PendingStandardSiteCaravan,
                        ThingDefOf.Silver.defName,
                        PendingStandardSiteCost);

                    if (hasEnough)
                    {
                        RimworldManager.RemoveThingFromCaravan(
                            PendingStandardSiteCaravan,
                            DefDatabase<ThingDef>.GetNamed(ThingDefOf.Silver.defName),
                            PendingStandardSiteCost);
                    }
                }
            }
            finally
            {
                ClearPendingStandardSiteBuild();
            }
        }
    }
}