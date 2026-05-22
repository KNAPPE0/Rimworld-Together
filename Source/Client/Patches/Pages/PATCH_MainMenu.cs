using GameClient.Dialogs;
using GameClient.Dialogs.Default;
using GameClient.Files;
using GameClient.Hooks.ServerBrowser;
using GameClient.Hooks.TCPNetwork;
using GameClient.Managers;
using GameClient.Misc;
using GameClient.PacketManagers;
using HarmonyLib;
using RimWorld;
using Shared;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using static GameClient.Hooks.TCPNetwork.ClientNetwork;

namespace GameClient.Patches.Pages
{
    [HarmonyPatchCategory("Start")]
    [HarmonyPatch(typeof(VersionControl), nameof(VersionControl.DrawInfoInCorner))]
    public static class VersionControl_DrawInfoInCorner_Patch
    {
        public static void Postfix()
        {
            string toDisplay = $"RimWorld Together V-{CommonValues.ExecutableVersion}";
            Vector2 size = Text.CalcSize(toDisplay);
            Rect rect = new Rect(10f, 73f, size.x, size.y);

            Text.Font = GameFont.Small;
            GUI.color = Color.white.ToTransparent(0.5f);
            Widgets.Label(rect, toDisplay);
            GUI.color = Color.white;
        }
    }

    [HarmonyPatchCategory("Start")]
    [HarmonyPatch(typeof(Verse.OptionListingUtility), nameof(Verse.OptionListingUtility.DrawOptionListing))]
    public static class MainMenuPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Rect rect, List<ListableOption> optList)
        {
            if (Current.ProgramState != ProgramState.Entry) return true;
            if (optList == null || optList.Count == 0) return true;
            if (optList.FirstOrDefault()?.GetType() != typeof(ListableOption)) return true;

            // "Host Local Server" entry — visible when
            // EITHER (a) a bundled server is found in the mod folder
            // (<ModRoot>/LocalServer/GameServer.exe), OR (b) a fallback
            // URL is configured in mod settings. Both sources must be
            // KMH-built (vanilla RWT speaks a different protocol — it
            // would immediate-disconnect on the first KMH packet).
            //
            // LocalServerHandler.IsAvailable wraps both checks. If
            // neither path exists, the menu entry stays hidden so the
            // player never sees a button that can't function.
            if (LocalServerHandler.IsAvailable)
            {
                optList.Insert(0, new ListableOption("Host Local Server", delegate
                {
                    if (SessionHandler.CurrentNetworkState != ClientNetworkState.Disconnected) return;
                    LocalServerHandler.ManageLocalServer();
                }));
            }

            optList.Insert(0, new ListableOption("Server Browser", delegate
            {
                if (SessionHandler.CurrentNetworkState != ClientNetworkState.Disconnected) return;
                if (!HarmonyHandler.CheckForModCollision()) return;
                if (!CheckIfLoginIsValid()) PM_Login.PromptCreateAccount();
                else ServerBrowserManager.TryConnect();
            }));

            optList.Insert(0, new ListableOption("Direct Connect", delegate
            {
                if (SessionHandler.CurrentNetworkState != ClientNetworkState.Disconnected) return;
                if (!HarmonyHandler.CheckForModCollision()) return;
                if (!CheckIfLoginIsValid()) PM_Login.PromptCreateAccount();
                else DLG_Base.PushNewDialog(new DLG_Login());
            }));

            if (EnforcementGuard.IsActive && EnforcementGuard.HasBackup)
            {
                int restoreIndex = Mathf.Min(2, optList.Count);
                optList.Insert(restoreIndex, new ListableOption("Restore Original Configs", delegate
                {
                    OptionsProfileSessionManager.RestorePersonalConfigsManual();
                }));
            }

            return true;
        }

        public static bool CheckIfLoginIsValid()
        {
            PersistentSettings settings = PersistentSettings.Load();
            if (!StringChecker.CheckIfStringValid(settings.UserSettings.Username)) return false;
            if (!StringChecker.CheckIfStringValid(settings.UserSettings.Password)) return false;
            return true;
        }
    }

    [HarmonyPatchCategory("Start")]
    [HarmonyPatch(typeof(MainMenuDrawer), "DoMainMenuControls")]
    public static class PatchButton
    {
        [HarmonyPrefix]
        public static bool DoPre(Rect rect)
        {
            if (Current.ProgramState == ProgramState.Entry)
            {
                Vector2 buttonSize = new Vector2(45f, 45f);
                Vector2 buttonLocation = new Vector2(rect.x - 50f, rect.y);

                if (Widgets.ButtonText(new Rect(buttonLocation.x, buttonLocation.y, buttonSize.x, buttonSize.y), ""))
                {
                    if (!HarmonyHandler.CheckForModCollision()) return true;
                    if (SessionHandler.CurrentNetworkState != ClientNetworkState.Disconnected) return true;
                    if (!MainMenuPatch.CheckIfLoginIsValid()) PM_Login.PromptCreateAccount();
                    else PM_Login.QuickConnectUser();
                }
            }

            return true;
        }

        [HarmonyPostfix]
        public static void DoPost(Rect rect)
        {
            if (Current.ProgramState == ProgramState.Entry)
            {
                Vector2 buttonSize = new Vector2(45f, 45f);
                Vector2 buttonLocation = new Vector2(rect.x - 50f, rect.y);

                if (Widgets.ButtonText(new Rect(buttonLocation.x, buttonLocation.y, buttonSize.x, buttonSize.y), "▶")) { }
            }
        }
    }
}