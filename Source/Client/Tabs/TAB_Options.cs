using GameClient.Core.Configs;
using GameClient.Dialogs;
using GameClient.Managers;
using GameClient.PacketManagers;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace GameClient.Tabs
{
    public class TAB_Options : MainTabWindow
    {
        public override Vector2 RequestedTabSize => new Vector2(200f, 195f);

        public TAB_Options()
        {
            layer = WindowLayer.GameUI;

            forcePause = false;
            draggable = false;
            focusWhenOpened = false;
            drawShadow = false;
            preventCameraMotion = false;
            drawInScreenshotMode = false;

            soundAppear = SoundDefOf.CommsWindow_Open;

            closeOnAccept = false;
            closeOnCancel = true;
        }

        public override void DoWindowContents(Rect rect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(rect);

            if (listingStandard.ButtonText("Mod Manager")) PM_Mods.OpenModManagerMenu();

            if (listingStandard.ButtonText("Event Manager")) PM_Events.OpenEventManagerMenu();

            if (listingStandard.ButtonText("Difficulty Manager")) DifficultyManager.OpenDifficultyManagerMenu();

            if (listingStandard.ButtonText("Save Uploader")) PM_Saves.OpenSaveUploaderMenu();

            GUI.color = Color.red;
            if (listingStandard.ButtonText("Reset Save")) ShowResetMenu();
            GUI.color = Color.white;

            listingStandard.End();
        }

        private void ShowResetMenu()
        {
            Action r1 = delegate
            {
                DLG_Base.PushNewDialog(new DLG_Wait("Waiting for server response"));
                PM_Saves.RequestResetSave();
            };

            DLG_YesNo d1 = new DLG_YesNo("Are you sure you want to delete your save?", r1, null, "DELETE!", "no", Color.red);
            DLG_Base.PushNewDialog(d1);
        }
    }
}
