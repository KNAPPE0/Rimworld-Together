using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Shared;
using Verse;
using static Shared.CommonEnumerators;


namespace GameClient
{
    public static class SiteManager
    {
        public static SitePartDef[] siteDefs;

        public static string[] siteDefLabels;

        public static int[] siteRewardCount;

        public static ThingDef[] siteRewardDefNames;

        public static void SetSiteData(ServerGlobalData serverGlobalData)
        {
            siteRewardDefNames = new ThingDef[]
            {
                ThingDefOf.RawPotatoes,
                ThingDefOf.Steel,
                ThingDefOf.WoodLog,
                ThingDefOf.Silver,
                ThingDefOf.ComponentIndustrial,
                ThingDefOf.Chemfuel,
                ThingDefOf.MedicineHerbal,
                ThingDefOf.Cloth,
                ThingDefOf.MealSimple
            };

            siteRewardCount = new int[]
            {
                serverGlobalData.SiteValues.FarmlandRewardCount,
                serverGlobalData.SiteValues.QuarryRewardCount,
                serverGlobalData.SiteValues.SawmillRewardCount,
                serverGlobalData.SiteValues.BankRewardCount,
                serverGlobalData.SiteValues.LaboratoryRewardCount,
                serverGlobalData.SiteValues.RefineryRewardCount,
                serverGlobalData.SiteValues.HerbalWorkshopRewardCount,
                serverGlobalData.SiteValues.TextileFactoryRewardCount,
                serverGlobalData.SiteValues.FoodProcessorRewardCount
            };

            PersonalSiteManager.SetSiteData(serverGlobalData);
            FactionSiteManager.SetSiteData(serverGlobalData);
        }

        public static void SetSiteDefs()
        {
            List<SitePartDef> defs = new List<SitePartDef>();
            foreach (SitePartDef def in DefDatabase<SitePartDef>.AllDefs)
            {
                if (def.defName == "RTFarmland") defs.Add(def);
                else if (def.defName == "RTQuarry") defs.Add(def);
                else if (def.defName == "RTSawmill") defs.Add(def);
                else if (def.defName == "RTBank") defs.Add(def);
                else if (def.defName == "RTLaboratory") defs.Add(def);
                else if (def.defName == "RTRefinery") defs.Add(def);
                else if (def.defName == "RTHerbalWorkshop") defs.Add(def);
                else if (def.defName == "RTTexTileFactory") defs.Add(def);
                else if (def.defName == "RTFoodProcessor") defs.Add(def);
            }
            siteDefs = defs.ToArray();

            List<string> siteNames = new List<string>();
            foreach(SitePartDef def in siteDefs)
            {
                siteNames.Add(def.label);
            }
            siteDefLabels = siteNames.ToArray();
        }

        public static SitePartDef GetDefForNewSite(int siteTypeID, bool isFromFaction)
        {
            return siteDefs[siteTypeID];
        }

        public static void ParseSitePacket(Packet packet)
        {
            SiteData siteData = Serializer.ConvertBytesToObject<SiteData>(packet.Contents);

            switch(siteData.SiteStepMode)
            {
                case SiteStepMode.Accept:
                    OnSiteAccept();
                    break;

                case SiteStepMode.Build:
                    PlayerSiteManager.SpawnSingleSite(siteData);
                    break;

                case SiteStepMode.Destroy:
                    PlayerSiteManager.RemoveSingleSite(siteData);
                    break;

                case SiteStepMode.Info:
                    OnSimpleSiteOpen(siteData);
                    break;

                case SiteStepMode.Deposit:
                    //Nothing goes here
                    break;

                case SiteStepMode.Retrieve:
                    OnWorkerRetrieval(siteData);
                    break;

                case SiteStepMode.Reward:
                    ReceiveSitesRewards(siteData);
                    break;

                case SiteStepMode.WorkerError:
                    OnWorkerError();
                    break;
            }
        }

        private static void OnSiteAccept()
        {
            DialogManager.PopWaitDialog();
            DialogManager.PushNewDialog(new RT_Dialog_OK("The desired site has been built!"));

            SaveManager.ForceSave();
        }

        public static void OnSimpleSiteRequest()
        {
            DialogManager.PushNewDialog(new RT_Dialog_Wait("Waiting for site information"));

            SiteData siteData = new SiteData();
            siteData.Tile = ClientValues.chosenSite.Tile;
            siteData.SiteStepMode = SiteStepMode.Info;

            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.SitePacket), siteData);
            Network.Listener.EnqueuePacket(packet);
        }

        public static void OnSimpleSiteOpen(SiteData siteData)
        {
            DialogManager.PopWaitDialog();

            if (siteData.WorkerData == null)
            {
                RT_Dialog_YesNo d1 = new RT_Dialog_YesNo("There is no current worker on this site, send?", 
                    delegate { PrepareSendPawnScreen(); }, null);

                DialogManager.PushNewDialog(d1);
            }

            else
            {
                RT_Dialog_YesNo d1 = new RT_Dialog_YesNo("You have a worker on this site, retrieve?",
                    delegate { RequestWorkerRetrieval(siteData); }, null);

                DialogManager.PushNewDialog(d1);
            }
        }
        private static void RequestWorkerRetrieval(SiteData siteData)
        {
            DialogManager.PushNewDialog(new RT_Dialog_Wait("Waiting for site worker"));

            siteData.SiteStepMode = SiteStepMode.Retrieve;

            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.SitePacket), siteData);
            Network.Listener.EnqueuePacket(packet);
        }

        private static void OnWorkerRetrieval(SiteData siteData)
        {
            DialogManager.PopWaitDialog();

            Action r1 = delegate
            {
                Pawn pawnToRetrieve = HumanScribeManager.StringToHuman(Serializer.ConvertBytesToObject<HumanData>(siteData.WorkerData));

                RimworldManager.PlaceThingIntoCaravan(pawnToRetrieve, ClientValues.chosenCaravan);

                SaveManager.ForceSave();
            };

            DialogManager.PushNewDialog(new RT_Dialog_OK("Worker have been recovered", r1));
        }

        private static void PrepareSendPawnScreen()
        {
            List<Pawn> pawns = ClientValues.chosenCaravan.PawnsListForReading;
            List<string> pawnNames = new List<string>();
            foreach (Pawn pawn in pawns)
            {
                if (DeepScribeHelper.CheckIfThingIsHuman(pawn)) pawnNames.Add(pawn.Label);
            }

            RT_Dialog_ListingWithButton d1 = new RT_Dialog_ListingWithButton("Pawn Selection", "Select the pawn you wish to send", 
                pawnNames.ToArray(), SendPawnToSite);

            DialogManager.PushNewDialog(d1);
        }

        public static void SendPawnToSite()
        {
            List<Pawn> caravanPawns = ClientValues.chosenCaravan.PawnsListForReading;
            List<Pawn> caravanHumans = new List<Pawn>();
            foreach (Pawn pawn in caravanPawns)
            {
                if (DeepScribeHelper.CheckIfThingIsHuman(pawn)) caravanHumans.Add(pawn);
            }

            Pawn pawnToSend = caravanHumans[DialogManager.dialogButtonListingResultInt];
            ClientValues.chosenCaravan.RemovePawn(pawnToSend);

            SiteData siteData = new SiteData();
            siteData.Tile = ClientValues.chosenSite.Tile;
            siteData.SiteStepMode = SiteStepMode.Deposit;
            siteData.WorkerData = Serializer.ConvertObjectToBytes(HumanScribeManager.HumanToString(pawnToSend));

            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.SitePacket), siteData);
            Network.Listener.EnqueuePacket(packet);

            if (caravanHumans.Count == 1) ClientValues.chosenCaravan.Destroy();

            SaveManager.ForceSave();
        }

        public static void RequestDestroySite()
        {
            Action r1 = delegate
            {
                SiteData siteData = new SiteData();
                siteData.Tile = ClientValues.chosenSite.Tile;
                siteData.SiteStepMode = SiteStepMode.Destroy;

                Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.SitePacket), siteData);
                Network.Listener.EnqueuePacket(packet);
            };

            RT_Dialog_YesNo d1 = new RT_Dialog_YesNo("Are you sure you want to destroy this site?", r1, null);
            DialogManager.PushNewDialog(d1);
        }

        private static void OnWorkerError()
        {
            DialogManager.PushNewDialog(new RT_Dialog_Error("The site has a worker inside!"));
        }

        private static void ReceiveSitesRewards(SiteData siteData)
        {
            if (ClientValues.isReadyToPlay && !ClientValues.rejectSiteRewardsBool && RimworldManager.CheckIfPlayerHasMap())
            {
                Site[] sites = Find.WorldObjects.Sites.ToArray();
                List<Site> rewardedSites = new List<Site>();

                foreach (Site site in sites)
                {
                    if (siteData.SitesWithRewards.Contains(site.Tile)) rewardedSites.Add(site);
                }

                Thing[] rewards = GetSiteRewards(rewardedSites.ToArray());

                if (rewards.Count() > 0)
                {
                    TransferManager.GetTransferedItemsToSettlement(rewards, true, false, false);

                    RimworldManager.GenerateLetter("Site Rewards", "You have received site rewards!", LetterDefOf.PositiveEvent);
                }
            }
        }

        private static Thing[] GetSiteRewards(Site[] sites)
        {

            List<Thing> thingsToGet = new List<Thing>();
            foreach (Site site in sites)
            {
                for (int i = 0; i < siteDefs.Count(); i++)
                {
                    if (site.MainSitePartDef == siteDefs[i])
                    {
                        ThingData thingData = new ThingData();
                        thingData.DefName = siteRewardDefNames[i].defName;
                        thingData.Quantity = siteRewardCount[i];
                        thingData.Quality = "null";
                        thingData.Hitpoints = siteRewardDefNames[i].BaseMaxHitPoints;

                        if (siteRewardCount[i] > 0) thingsToGet.Add(ThingScribeManager.StringToItem(thingData));

                        break;
                    }
                }
            }

            return thingsToGet.ToArray();
        }
    }

    public static class PersonalSiteManager
    {
        public static int[] sitePrices;

        public static void SetSiteData(ServerGlobalData serverGlobalData)
        {
            sitePrices = new int[]
            {
                serverGlobalData.SiteValues.PersonalFarmlandCost,
                serverGlobalData.SiteValues.PersonalQuarryCost,
                serverGlobalData.SiteValues.PersonalSawmillCost,
                serverGlobalData.SiteValues.PersonalBankCost,
                serverGlobalData.SiteValues.PersonalLaboratoryCost,
                serverGlobalData.SiteValues.PersonalRefineryCost,
                serverGlobalData.SiteValues.PersonalHerbalWorkshopCost,
                serverGlobalData.SiteValues.PersonalTextileFactoryCost,
                serverGlobalData.SiteValues.PersonalFoodProcessorCost
            };
        }

        public static void PushConfirmSiteDialog()
        {
            RT_Dialog_YesNo d1 = new RT_Dialog_YesNo($"This site will cost you {sitePrices[DialogManager.selectedScrollButton]} " +
                $"silver, continue?", RequestSiteBuild, null);

            DialogManager.PushNewDialog(d1);
        }

        public static void RequestSiteBuild()
        {
            DialogManager.PopDialog(DialogManager.dialogScrollButtons);

            if (!RimworldManager.CheckIfHasEnoughSilverInCaravan(ClientValues.chosenCaravan, sitePrices[DialogManager.selectedScrollButton]))
            {
                DialogManager.PushNewDialog(new RT_Dialog_Error("You do not have enough silver!"));
            }

            else
            {
                RimworldManager.RemoveThingFromCaravan(ThingDefOf.Silver, sitePrices[DialogManager.selectedScrollButton], ClientValues.chosenCaravan);

                SiteData siteData = new SiteData();
                siteData.SiteStepMode = SiteStepMode.Build;
                siteData.Tile = ClientValues.chosenCaravan.Tile;
                siteData.Type = DialogManager.selectedScrollButton;
                siteData.IsFromFaction = false;

                Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.SitePacket), siteData);
                Network.Listener.EnqueuePacket(packet);

                DialogManager.PushNewDialog(new RT_Dialog_Wait("Waiting for building"));
            }
        }
    }

    public static class FactionSiteManager
    {
        public static int[] sitePrices;

        public static void SetSiteData(ServerGlobalData serverGlobalData)
        {
            sitePrices = new int[]
            {
                serverGlobalData.SiteValues.FactionFarmlandCost,
                serverGlobalData.SiteValues.FactionQuarryCost,
                serverGlobalData.SiteValues.FactionSawmillCost,
                serverGlobalData.SiteValues.FactionBankCost,
                serverGlobalData.SiteValues.FactionLaboratoryCost,
                serverGlobalData.SiteValues.FactionRefineryCost ,
                serverGlobalData.SiteValues.FactionHerbalWorkshopCost,
                serverGlobalData.SiteValues.FactionTextileFactoryCost,
                serverGlobalData.SiteValues.FactionFoodProcessorCost
            };
        }

        public static void PushConfirmSiteDialog()
        {
            RT_Dialog_YesNo d1 = new RT_Dialog_YesNo($"This site will cost you {sitePrices[DialogManager.selectedScrollButton]} " +
                $"silver, continue?", RequestSiteBuild, null);

            DialogManager.PushNewDialog(d1);
        }

        public static void RequestSiteBuild()
        {
            DialogManager.PopDialog(DialogManager.dialogScrollButtons);

            if (!RimworldManager.CheckIfHasEnoughSilverInCaravan(ClientValues.chosenCaravan, sitePrices[DialogManager.selectedScrollButton]))
            {
                DialogManager.PushNewDialog(new RT_Dialog_Error("You do not have enough silver!"));
            }

            else
            {
                RimworldManager.RemoveThingFromCaravan(ThingDefOf.Silver, sitePrices[DialogManager.selectedScrollButton], ClientValues.chosenCaravan);

                SiteData siteData = new SiteData();
                siteData.SiteStepMode = SiteStepMode.Build;
                siteData.Tile = ClientValues.chosenCaravan.Tile;
                siteData.Type = DialogManager.selectedScrollButton;
                siteData.IsFromFaction = true;

                Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.SitePacket), siteData);
                Network.Listener.EnqueuePacket(packet);

                DialogManager.PushNewDialog(new RT_Dialog_Wait("Waiting for building"));
            }
        }
    }
}
