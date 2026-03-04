using GameClient.Dialogs;
using GameClient.Managers;
using GameClient.Misc;
using GameClient.Patches.Tabs;
using GameClient.WorldObjects;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using static Shared.CommonEnumerators;

namespace GameClient.Patches
{
    [HarmonyPatch(typeof(Settlement), nameof(Settlement.GetGizmos))]
    public static class Patch_Settlement_GetGizmos
    {
        [HarmonyPostfix]
        public static void DoPost(ref IEnumerable<Gizmo> __result, Settlement __instance)
        {
            List<Gizmo> gizmoList = __result.ToList();

            Command_Action command_PersonalFactionMenu = new Command_Action
            {
                defaultLabel = "Guild Menu",
                defaultDesc = "Access your guild menu",
                icon = ContentFinder<Texture2D>.Get("Commands/Guild"),
                action = delegate
                {
                    if (SessionHandler.CurrentActionValues.EnableFactions)
                    {
                        if (SessionHandler.HasFaction) GuildManager.OnFactionOpen();
                        else GuildManager.OnNoFactionOpen();
                    }
                    else RT_Dialog_Base.PushNewDialog(new RT_Dialog_Message("ERROR", new string[] { "This feature has been disabled in this server!" }));
                }
            };

            Command_Action command_Leaderboard = new Command_Action
            {
                defaultLabel = "Leaderboard",
                defaultDesc = "Access the server leaderboard",
                icon = ContentFinder<Texture2D>.Get("Commands/Leaderboard"),
                action = delegate
                {
                    if (SessionHandler.CurrentActionValues.EnableLeaderboard) LeaderboardManager.Ask();
                    else RT_Dialog_Base.PushNewDialog(new RT_Dialog_Message("ERROR", new string[] { "This feature has been disabled in this server!" }));
                }
            };

            Command_Action command_SiteConfigMenu = new Command_Action
            {
                defaultLabel = "Site settings",
                defaultDesc = "Configure the settings for your sites",
                icon = ContentFinder<Texture2D>.Get("Commands/Config"),
                action = delegate
                {
                    if (SessionHandler.CurrentActionValues.SiteAction.IsEnabled) RT_Dialog_Base.PushNewDialog(new RT_Dialog_SiteMenu(true));
                    else RT_Dialog_Base.PushNewDialog(new RT_Dialog_Message("ERROR", new string[] { "This feature has been disabled in this server!" }));
                }
            };

            if (__instance.Faction == Find.FactionManager.OfPlayer) gizmoList.Add(command_PersonalFactionMenu);
            gizmoList.Add(command_SiteConfigMenu);
            gizmoList.Add(command_Leaderboard);
            __result = gizmoList;
        }
    }

    [HarmonyPatch(typeof(Caravan), nameof(Caravan.GetGizmos))]
    public static class Patch_Caravan_GetGizmos
    {
        [HarmonyPostfix]
        public static void ModifyPost(ref IEnumerable<Gizmo> __result, Caravan __instance)
        {
            if (RimworldManager.CheckIfPlayerHasMap())
            {
                bool hasSomethingOnTop = Find.World.worldObjects.AllWorldObjects.FirstOrDefault(fetch => fetch.Tile == __instance.Tile
                     && fetch is not Caravan) != null;

                List<Gizmo> gizmoList = __result.ToList();

                Command_Action Command_BuildSite = new Command_Action
                {
                    defaultLabel = "Build a Site",
                    defaultDesc = "Build an utility site for your faction",
                    icon = ContentFinder<Texture2D>.Get("Commands/Site"),
                    action = delegate
                    {
                        SessionHandler.ChosenCaravan = __instance;

                        if (SessionHandler.CurrentActionValues.SiteAction.IsEnabled) RT_Dialog_Base.PushNewDialog(new RT_Dialog_SiteMenu(false));
                        else RT_Dialog_Base.PushNewDialog(new RT_Dialog_Message("ERROR", new string[] { "This feature has been disabled in this server!" }));
                    }
                };

                Command_Action Command_BuildRoad = new Command_Action
                {
                    defaultLabel = "Road Builder",
                    defaultDesc = "Build and destroy roads",
                    icon = ContentFinder<Texture2D>.Get("Commands/Road"),
                    action = delegate
                    {
                        SessionHandler.ChosenCaravan = __instance;

                        if (SessionHandler.CurrentActionValues.RoadsAction.IsEnabled)
                        {
                            List<PlanetTile> neighborTiles = new List<PlanetTile>();
                            Find.WorldGrid.GetTileNeighbors(SessionHandler.ChosenCaravan.Tile, neighborTiles);

                            SurfaceTile selectedTile = (SurfaceTile)Find.WorldGrid[__instance.Tile];
                            RoadManagerHelper.ShowRoadChooseDialog(neighborTiles.ToArray(), selectedTile.Roads != null);
                        }
                        else RT_Dialog_Base.PushNewDialog(new RT_Dialog_Message("ERROR", new string[] { "This feature has been disabled in this server!" }));
                    }
                };

                if (!hasSomethingOnTop) gizmoList.Add(Command_BuildSite);
                gizmoList.Add(Command_BuildRoad);
                __result = gizmoList;
            }
        }
    }

    [HarmonyPatch(typeof(TransportersArrivalAction_AttackSettlement), "GetFloatMenuOptions")]
    public static class PatchDropAttack
    {
        [HarmonyPostfix]
        public static void ModifyPost(ref IEnumerable<FloatMenuOption> __result, Settlement settlement)
        {
            if (SessionHandler.PlayerFactions.Contains(settlement.Faction))
            {
                List<FloatMenuOption> floatMenuList = __result.ToList();
                floatMenuList.Clear();
                __result = floatMenuList;
            }
        }
    }

    [HarmonyPatch(typeof(DestroyedSettlement), "GetGizmos")]
    public static class DestroyedSettlementPatch
    {
        [HarmonyPostfix]
        public static void DoPost(ref IEnumerable<Gizmo> __result)
        {
            List<Gizmo> gizmoList = __result.ToList();
            List<Gizmo> removeList = new List<Gizmo>();

            foreach (Command_Action action in gizmoList.ToList())
            {
                if (action.defaultLabel == "CommandSettle".Translate()) removeList.Add(action);
            }

            foreach (Gizmo gizmo in removeList) gizmoList.Remove(gizmo);

            __result = gizmoList;
        }
    }

    [HarmonyPatch(typeof(WorldInspectPane), "CurTabs", MethodType.Getter)]
    public static class Patch_WorldInspectPane_CurTabs
    {
        private static PlayersUI _playersTab;
        private static BasesUI _basesTab;
        private static SitesUI _sitesTab;

        private static void EnsureTabs()
        {
            _playersTab ??= new PlayersUI();
            _basesTab ??= new BasesUI();
            _sitesTab ??= new SitesUI();
        }

        [HarmonyPrefix]
        public static bool DoPre(WorldInspectPane __instance, ref IEnumerable<InspectTabBase> __result)
        {
            if (SessionHandler.CurrentNetworkState != ClientNetworkState.Connected)
                return true;

            if (Find.WorldSelector.NumSelectedObjects == 1)
            {
                __result = Find.WorldSelector.SingleSelectedObject.GetInspectTabs();
                return false;
            }

            if (Find.WorldSelector.NumSelectedObjects == 0 && Find.WorldSelector.SelectedTile.Valid)
            {
                EnsureTabs();

                IEnumerable<InspectTabBase> baseTabs = PlanetLayer.Selected?.Def?.Tabs ?? Enumerable.Empty<InspectTabBase>();

                __result = baseTabs
                    .Concat(new InspectTabBase[] { _playersTab, _basesTab, _sitesTab });

                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(WorldPawns), nameof(WorldPawns.PassToWorld))]
    public static class Patch_WorldPawns_PassToWorld
    {
        [HarmonyPrefix]
        public static bool DoPre(Pawn pawn)
        {
            if (!SessionHandler.PlayerFactions.Contains(pawn.Faction)) return true;
            else return false;
        }
    }
}