using GameClient.Defs;
using GameClient.Dialogs;
using GameClient.Dialogs.Default;
using GameClient.Misc;
using GameClient.PacketManagers;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace GameClient.WorldObjects
{
    public class WO_Site : MapParent
    {
        private Material cachedMat;

        public override string Label => base.Label;

        public SitePartDef MainSitePartDef => MainSitePart.def;

        public List<RTSitePart> parts = new List<RTSitePart>();

        private RTSitePart MainSitePart { get { return parts[0]; } }

        public override Texture2D ExpandingIcon => MainSitePartDef.ExpandingIconTexture;

        public override Material Material
        {
            get
            {
                if (cachedMat == null)
                {
                    cachedMat = MaterialPool.MatFrom(color: (!MainSitePartDef.applyFactionColorToSiteTexture || base.Faction == null) ? 
                        Color.white : base.Faction.Color, texPath: MainSitePartDef.siteTexture, shader: ShaderDatabase.WorldOverlayTransparentLit, renderQueue: 3550);
                }
                return cachedMat;
            }
        }

        public void AddPart(RTSitePart part)
        {
            if (!part.def.forceMutators.NullOrEmpty())
            {
                foreach (TileMutatorDef forceMutator in part.def.forceMutators)
                    base.Tile.Tile.AddMutator(forceMutator);
            }
            parts.Add(part);
        }

        private bool IsOwnerOrGuild => Faction == Find.FactionManager.OfPlayer || Faction == SessionHandler.GuildFaction;

        public override IEnumerable<Gizmo> GetGizmos()
        {
            List<Gizmo> gizmos = new List<Gizmo>();

            // Info - always available
            gizmos.Add(new Command_Action
            {
                defaultLabel = "Site Info",
                defaultDesc = "View production details, workers, and efficiency",
                icon = ContentFinder<Texture2D>.Get("Commands/Worker"),
                action = delegate
                {
                    SessionHandler.ChosenSite = this;
                    PM_Sites.RequestCustomSiteInfo(Tile);
                }
            });

            // Owner/Guild actions
            if (IsOwnerOrGuild)
            {
                gizmos.Add(new Command_Action
                {
                    defaultLabel = "Upgrade",
                    defaultDesc = "Increase max worker capacity (owner only)",
                    icon = ContentFinder<Texture2D>.Get("Commands/Site"),
                    action = delegate
                    {
                        SessionHandler.ChosenSite = this;
                        PM_Sites.RequestSiteUpgrade(Tile);
                    }
                });

                gizmos.Add(new Command_Action
                {
                    defaultLabel = "Destroy",
                    defaultDesc = "Permanently destroy this site",
                    icon = ContentFinder<Texture2D>.Get("Commands/Site"),
                    action = delegate
                    {
                        if (SessionHandler.CurrentActionValues.SiteAction.IsEnabled)
                        {
                            SessionHandler.ChosenSite = this;
                            PM_Sites.RequestDestroySite();
                        }
                        else DLG_Base.PushNewDialog(new DLG_Message("Error", new string[] { "Sites are disabled on this server." }));
                    }
                });
            }

            return gizmos;
        }

        public override IEnumerable<Gizmo> GetCaravanGizmos(Caravan caravan)
        {
            List<Gizmo> gizmos = new List<Gizmo>();

            // Info
            gizmos.Add(new Command_Action
            {
                defaultLabel = "Site Info",
                defaultDesc = "View production details, workers, and efficiency",
                icon = ContentFinder<Texture2D>.Get("Commands/Worker"),
                action = delegate
                {
                    SessionHandler.ChosenCaravan = caravan;
                    SessionHandler.ChosenSite = this;
                    PM_Sites.RequestCustomSiteInfo(Tile);
                }
            });

            // Worker management - standard sites (owner/guild)
            if (IsOwnerOrGuild)
            {
                gizmos.Add(new Command_Action
                {
                    defaultLabel = "Assign Pawn",
                    defaultDesc = "Assign or retrieve a pawn worker at this site",
                    icon = ContentFinder<Texture2D>.Get("Commands/Worker"),
                    action = delegate
                    {
                        DLG_Base.PushNewDialog(new DLG_Wait());
                        SessionHandler.ChosenCaravan = caravan;
                        SessionHandler.ChosenSite = this;
                        PM_Sites.AskForInformation();
                    }
                });
            }

            // Join as worker (anyone can try - server validates access)
            gizmos.Add(new Command_Action
            {
                defaultLabel = "Join Site",
                defaultDesc = "Join as a worker (server checks access permissions)",
                icon = ContentFinder<Texture2D>.Get("Commands/Site"),
                action = delegate
                {
                    SessionHandler.ChosenCaravan = caravan;
                    SessionHandler.ChosenSite = this;
                    PM_Sites.RequestWorkerJoin(Tile);
                }
            });

            // Leave
            gizmos.Add(new Command_Action
            {
                defaultLabel = "Leave Site",
                defaultDesc = "Leave this site as a worker",
                icon = ContentFinder<Texture2D>.Get("Commands/Site"),
                action = delegate
                {
                    SessionHandler.ChosenSite = this;
                    PM_Sites.RequestWorkerLeave(Tile);
                }
            });

            return gizmos;
        }
    }
}
