using GameClient.Defs;
using GameClient.Dialogs;
using GameClient.Dialogs.Default;
using GameClient.Misc;
using GameClient.PacketManagers;
using RimWorld;
using RimWorld.Planet;
using Shared.Misc;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace GameClient.WorldObjects
{
    public class WO_Site : MapParent
    {
        private Material cachedMat;

        public override string Label => base.Label;

        private RTSitePart MainSitePart => parts != null && parts.Count > 0 ? parts[0] : null;

        public SitePartDef MainSitePartDef => MainSitePart?.def;

        public List<RTSitePart> parts = new List<RTSitePart>();

        public override Texture2D ExpandingIcon
            => MainSitePartDef?.ExpandingIconTexture ?? BaseContent.BadTex;

        public override Material Material
        {
            get
            {
                if (cachedMat == null)
                {
                    SitePartDef def = MainSitePartDef;
                    if (def == null)
                        return null;

                    cachedMat = MaterialPool.MatFrom(
                        color: (!def.applyFactionColorToSiteTexture || base.Faction == null)
                            ? Color.white
                            : base.Faction.Color,
                        texPath: def.siteTexture,
                        shader: ShaderDatabase.WorldOverlayTransparentLit,
                        renderQueue: 3550);
                }

                return cachedMat;
            }
        }

        public void AddPart(RTSitePart part)
        {
            if (part == null || part.def == null)
            {
                Printer.Warning("[RT] Tried to add a null site part to WO_Site. Spawn cancelled.");
                return;
            }

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
                        else
                        {
                            DLG_Base.PushNewDialog(new DLG_Message("Error", new string[] { "Sites are disabled on this server." }));
                        }
                    }
                });
            }

            return gizmos;
        }

        public override IEnumerable<Gizmo> GetCaravanGizmos(Caravan caravan)
        {
            List<Gizmo> gizmos = new List<Gizmo>();

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