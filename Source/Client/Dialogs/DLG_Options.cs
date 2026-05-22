using GameClient.Dialogs.Default;
using GameClient.Managers;
using GameClient.Misc;
using GameClient.PacketManagers;
using Shared;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs
{
    public class DLG_Options : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(620f, 480f);

        public static DLG_Options Instance { get; private set; } = null;
        public static bool IsDialogOpen { get; set; } = false;

        public static bool AutorejectTransfersBool = false;
        public static bool AutorejectSiteRewardsBool = false;
        public static bool EnablePreviewFeatures = false;

        public enum SyncingMode { Fast, Complete }
        public static SyncingMode CurrentSyncingMode = SyncingMode.Fast;

        private Vector2 _scroll = Vector2.zero;

        public DLG_Options() 
        {
            Instance = this;
            closeOnCancel = true;
        }

        public override void PostOpen() { base.PostOpen(); IsDialogOpen = true; }
        public override void PostClose() { base.PostClose(); IsDialogOpen = false; }

        public override void DoWindowContents(Rect rect)
        {
            float w = rect.width;

            Rect scrollOuter = new Rect(0f, 0f, w, rect.height - 45f);
            Rect scrollView = new Rect(0f, 0f, w - 20f, 580f);

            Widgets.BeginScrollView(scrollOuter, ref _scroll, scrollView);

            float y = 0f;
            float cw = scrollView.width - 10f;
            float btnX = cw * 0.45f;
            float btnW = cw - btnX;

            // === TITLE ===
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, cw, 32f), "RimWorld Together");
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            Widgets.Label(new Rect(0f, y + 22f, cw, 14f), "KMH Edition v26.5.22.1");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 40f;

            // === GAMEPLAY ===
            // KMH 26.5.20.1: Gameplay toggles via DialogLayout.DrawTightCheckbox
            // so the ☐ marker sits flush against the label instead of floating
            // ~400 px to the right of "Reject all transfers" (previous behaviour
            // stretched the checkbox across the full content width).
            DrawSectionHeader(ref y, cw, "Gameplay");
            DialogLayout.DrawTightCheckbox(0f, y, "Reject all transfers", ref AutorejectTransfersBool); y += 26f;
            DialogLayout.DrawTightCheckbox(0f, y, "Reject all site rewards", ref AutorejectSiteRewardsBool); y += 26f;

            Widgets.Label(new Rect(0f, y, btnX, 28f), "Syncing mode");
            if (Widgets.ButtonText(new Rect(btnX, y, btnW, 28f), $"{CurrentSyncingMode}")) ShowSyncMenu();
            y += 34f;

            // === OPTIONS PROFILE ENFORCEMENT ===
            DrawSectionHeader(ref y, cw, "Server Config Enforcement");

            // Status
            string status = OptionsProfileSessionManager.GetStatusLine();
            bool enforced = (SessionHandler.CurrentModConfig?.IsEnforced ?? false) || OptionsProfileSessionManager.IsEnforcementActive();
            string enfText = enforced ? "<color=green>ENFORCED</color>" : "<color=grey>Not enforced</color>";

            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            Widgets.Label(new Rect(0f, y, cw, 20f), status); y += 22f;
            GUI.color = Color.white;
            Widgets.Label(new Rect(0f, y, cw, 20f), $"Server: {enfText}"); y += 24f;

            // Has backup?
            bool hasBackup = OptionsProfileSessionManager.HasBackup();
            string backupText = hasBackup ? "<color=green>Backup exists</color>" : "<color=grey>No backup</color>";
            Widgets.Label(new Rect(0f, y, cw, 20f), $"Personal configs: {backupText}"); y += 28f;

            // Buttons
            float btn3W = (cw - 12f) / 3f;
            float btnH = 30f;

            if (Widgets.ButtonText(new Rect(0f, y, btn3W, btnH), "Request Profile"))
            {
                DLG_Base.PushNewDialog(new DLG_YesNo(
                    "Download and apply the server's config profile?\n\nYour current configs will be backed up first.\nThe game will restart to apply changes.",
                    delegate { OptionsProfileSessionManager.RequestServerOptionsProfile(isManual: true); },
                    null, "Download & Apply", "Cancel"));
            }

            if (Widgets.ButtonText(new Rect(btn3W + 6f, y, btn3W, btnH), "Publish Config"))
            {
                if (!SessionHandler.IsAdmin)
                    DLG_Base.PushNewDialog(new DLG_Message("Error", new[] { "Only admins can publish config profiles." }));
                else
                    DLG_Base.PushNewDialog(new DLG_YesNo(
                        "Upload your current mod configs as the server's enforced profile?\n\nAll players will receive this when enforcement is enabled.",
                        delegate { OptionsProfileSessionManager.PublishCurrentConfigProfileToServer(); },
                        null, "Publish", "Cancel"));
            }

            if (Widgets.ButtonText(new Rect((btn3W + 6f) * 2f, y, btn3W, btnH), "Restore Original"))
            {
                if (!hasBackup)
                    DLG_Base.PushNewDialog(new DLG_Message("Profile", new[] { "No backup found. You're already using your original configs." }));
                else
                    DLG_Base.PushNewDialog(new DLG_YesNo(
                        "Restore your original mod configs?\n\nThe game will restart. You'll need to reconnect to any server.",
                        delegate { OptionsProfileSessionManager.RestorePersonalConfigsManual(); },
                        null, "Restore & Restart", "Cancel"));
            }
            y += btnH + 10f;

            // === DANGEROUS ===
            DrawSectionHeader(ref y, cw, "Dangerous", Color.red);

            GUI.color = Color.red;
            if (Widgets.ButtonText(new Rect(0f, y, cw, 30f), "Reset Account"))
            {
                GUI.color = Color.white;
                DLG_Base.PushNewDialog(new DLG_YesNo("Are you sure you want to delete your save?",
                    delegate { DLG_Base.PushNewDialog(new DLG_Wait()); PM_Saves.RequestResetSave(); },
                    null, "DELETE!", "No", Color.red));
            }
            GUI.color = Color.white;

            Widgets.EndScrollView();

            // Close button
            if (Widgets.ButtonText(new Rect((w - 180f) / 2f, rect.height - 38f, 180f, 33f), "Close"))
                Close();
        }

        private void DrawSectionHeader(ref float y, float width, string label, Color? color = null)
        {
            Widgets.DrawLineHorizontal(0f, y, width);
            y += 4f;
            if (color.HasValue) GUI.color = color.Value;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, y, width, 22f), $"<b>{label}</b>");
            if (color.HasValue) GUI.color = Color.white;
            y += 24f;
        }

        private void ShowSyncMenu()
        {
            List<FloatMenuOption> list = new List<FloatMenuOption>
            {
                new FloatMenuOption("Fast", delegate { CurrentSyncingMode = SyncingMode.Fast; }),
                new FloatMenuOption("Complete", delegate { CurrentSyncingMode = SyncingMode.Complete; })
            };
            Find.WindowStack.Add(new FloatMenu(list));
        }

        [OnSessionEnd]
        private static void CloseTab() { IsDialogOpen = false; }
    }
}
