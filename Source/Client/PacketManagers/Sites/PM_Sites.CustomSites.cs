using GameClient.Dialogs;
using GameClient.Dialogs.Default;
using GameClient.Managers;
using GameClient.Misc;
using RimWorld;
using RimWorld.Planet;
using Shared;
using Shared.Files.Sites;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using TCPNetwork;
using TCPNetwork.Packets;
using Verse;
using static Shared.Misc.Printer;

namespace GameClient.PacketManagers
{
    /// <summary>
    /// Custom-site request senders + pending-build payment tracking.
    ///
    /// Pending state is held here so that silver is only deducted from the
    /// caravan after the server confirms the build/upgrade. If the server
    /// rejects, no silver is removed.
    /// </summary>
    public partial class PM_Sites
    {
        // KMH: pending custom build payment is charged only after server approval
        private static bool PendingCustomSiteBuild { get; set; } = false;
        private static int PendingCustomSiteTile { get; set; } = -1;
        private static int PendingCustomSiteCost { get; set; } = 0;
        private static Caravan PendingCustomSiteCaravan { get; set; } = null;

        private static bool PendingStandardSiteBuild { get; set; } = false;
        private static int PendingStandardSiteCost { get; set; } = 0;
        private static Caravan PendingStandardSiteCaravan { get; set; } = null;

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

        // -- standard site pending build (silver deducted on server-confirmed Accept) --

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

        // -- custom site request senders --

        /// <summary>
        /// Join a custom site as a worker. The server no longer trusts a
        /// client-supplied skill value — workers start at level 0 and gain
        /// XP for every cycle served. <see cref="SiteFile.Tile"/> is the
        /// only payload needed.
        /// </summary>
        public static void RequestWorkerJoin(int tile)
        {
            try
            {
                PKT_Site packet = new PKT_Site();
                packet._stepMode = PKT_Site.SiteStepMode.WorkerJoin;
                packet._file = new SiteFile { Tile = tile };
                Network.ServerEndpoint.EnqueuePacket(PacketHeader.SiteManager, packet);
            }
            catch { }
        }

        public static void RequestWorkerLeave(int tile)
        {
            try
            {
                PKT_Site packet = new PKT_Site();
                packet._stepMode = PKT_Site.SiteStepMode.WorkerLeave;
                packet._file = new SiteFile { Tile = tile };

                Network.ServerEndpoint.EnqueuePacket(PacketHeader.SiteManager, packet);
            }
            catch { }
        }

        /// <summary>
        /// Update where this site's reward share goes for the current player —
        /// owner updates the site default, workers update their personal slot.
        /// </summary>
        public static void RequestSetDestination(int tile, Shared.Files.Economy.RewardDestination dest)
        {
            try
            {
                PKT_Site packet = new PKT_Site();
                packet._stepMode = PKT_Site.SiteStepMode.SetDestination;
                packet._file = new SiteFile { Tile = tile };
                packet._newRewardDestination = (int)dest;

                Network.ServerEndpoint.EnqueuePacket(PacketHeader.SiteManager, packet);
            }
            catch { }
        }

        public static void RequestCustomSiteInfo(int tile)
        {
            try
            {
                PKT_Site packet = new PKT_Site();
                packet._stepMode = PKT_Site.SiteStepMode.CustomInfo;
                packet._file = new SiteFile { Tile = tile };

                Network.ServerEndpoint.EnqueuePacket(PacketHeader.SiteManager, packet);
            }
            catch { }
        }

        private static void ReceiveCustomSiteInfo(PKT_Site data)
        {
            try { if (DLG_Wait.Instance != null) DLG_Wait.Instance.Close(); } catch { }

            TryFinalizePendingCustomSiteBuild(data);

            string msg = data._statusMessage ?? "No response from server.";

            DLG_Base.PushNewDialog(new DLG_Message("Site Information", new string[] { msg }));
        }
    }
}
