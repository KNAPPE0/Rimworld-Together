using GameClient.Dialogs;
using GameClient.Dialogs.Default;
using GameClient.Managers;
using GameClient.Misc;
using HarmonyLib;
using RimWorld;
using System;
using TCPNetwork;
using UnityEngine;
using Verse;

namespace GameClient.Patches.Pages
{
    [HarmonyPatch(typeof(Page_SelectStoryteller), nameof(Page_SelectStoryteller.PreOpen))]
    public static class Patch_Page_SelectStoryteller_PreOpen
    {
        [HarmonyPrefix]
        public static bool DoPre(ref DifficultyDef ___difficulty, ref Difficulty ___difficultyValues)
        {
            Find.GameInitData.permadeathChosen = true;
            Find.GameInitData.permadeath = true;

            if (!SessionHandler.IsGeneratingFreshWorld)
            {
                // Only set default difficulty if it's NOT enforced by the server
                if (SessionHandler.CurrentDifficulty == null || !SessionHandler.CurrentDifficulty.IsEnforced)
                {
                    ___difficulty = DifficultyDefOf.Rough;
                    ___difficultyValues = new Difficulty(___difficulty);
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Page_SelectStoryteller), nameof(Page_SelectStoryteller.DoWindowContents))]
    public static class Patch_Page_SelectStoryteller_DoWindowContents
    {
        public static bool executedMessage;
        private static bool _difficultyNoticeShown = false;

        [HarmonyPrefix]
        public static bool DoPre(Rect rect, Page_SelectStoryteller __instance)
        {
            if (Widgets.ButtonText(DLG_Base.GetRectForLocation(rect, DLG_Base.SmallButtonSize, DLG_Base.RectLocation.BottomLeft), "") || KeyBindingDefOf.Cancel.KeyDownEvent)
            {
                __instance.Close();
                Network.ServerEndpoint.MarkForDisconnect();
            }

            if (SessionHandler.IsGeneratingFreshWorld) return true;

            bool storytellerEnforced = SessionHandler.CurrentStoryteller?.IsEnforced ?? false;
            bool difficultyEnforced = SessionHandler.CurrentDifficulty?.IsEnforced ?? false;
            bool scenarioEnforced = SessionHandler.CurrentScenario?.IsEnforced ?? false;

            // If storyteller is enforced, skip the whole page
            if (storytellerEnforced)
            {
                if (!executedMessage)
                {
                    executedMessage = true;
                    Action toDo = delegate
                    {
                        GameParameterManager.SetStoryteller(SessionHandler.CurrentStoryteller);
                        if (difficultyEnforced)
                            GameParameterManager.SetDifficulty(SessionHandler.CurrentDifficulty, true);
                        DLG_Base.PushNewDialog(__instance.next);
                        __instance.Close();
                        executedMessage = false;
                    };

                    string msg = "Storyteller is enforced by the server.";
                    if (difficultyEnforced) msg += "\nDifficulty is also enforced.";
                    DLG_Base.PushNewDialog(new DLG_Message("Server Enforcement", new string[] { msg }, toDo));
                }
                return true;
            }

            // If only difficulty is enforced, show one-time info (don't loop)
            if (difficultyEnforced && !_difficultyNoticeShown)
            {
                _difficultyNoticeShown = true;
                DLG_Base.PushNewDialog(new DLG_Message("Server Enforcement", 
                    new string[] { "Difficulty settings are enforced by the server. You may choose your storyteller freely." }));
            }

            return true;
        }

        [HarmonyPostfix]
        public static void DoPost(Rect rect)
        {
            Text.Font = GameFont.Small;

            if (Widgets.ButtonText(DLG_Base.GetRectForLocation(rect, DLG_Base.SmallButtonSize,
                DLG_Base.RectLocation.BottomLeft), "Disconnect")) { };
        }
    }

    [HarmonyPatch(typeof(Page_SelectStorytellerInGame), nameof(Page_SelectStorytellerInGame.PreClose))]
    public static class Patch_Page_SelectStorytellerInGame_PreClose
    {
        [HarmonyPrefix]
        public static bool DoPre()
        {
            if (SessionHandler.IsAdmin)
            {
                DLG_Base.PushNewDialog(new DLG_Message("Admin Override", 
                    new string[] { "Settings saved. Admin permissions allow overriding enforcements." }));
                return true;
            }

            bool storytellerEnforced = SessionHandler.CurrentStoryteller?.IsEnforced ?? false;
            bool difficultyEnforced = SessionHandler.CurrentDifficulty?.IsEnforced ?? false;

            if (!storytellerEnforced && !difficultyEnforced) return true;

            // Re-apply enforced settings
            Action toDo = delegate
            {
                if (storytellerEnforced)
                    GameParameterManager.SetStoryteller(SessionHandler.CurrentStoryteller);
                if (difficultyEnforced)
                    GameParameterManager.SetDifficulty(SessionHandler.CurrentDifficulty);
            };

            string enforcedItems = "";
            if (storytellerEnforced) enforcedItems += "Storyteller";
            if (storytellerEnforced && difficultyEnforced) enforcedItems += " and ";
            if (difficultyEnforced) enforcedItems += "Difficulty";

            DLG_Base.PushNewDialog(new DLG_Message("Server Enforcement", 
                new string[] { $"{enforcedItems} settings will be restored to server values." }, toDo));

            return false;
        }
    }
}
