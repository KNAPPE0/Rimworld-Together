using GameClient.Dialogs;
using GameClient.Dialogs.Default;
using GameClient.Files;
using GameClient.PacketManagers;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Verse;

namespace GameClient.Core.Configs
{
    public class ModConfigSetter : Mod
    {
        private ModConfigGetter ModConfigs { get; set; }

        public ModConfigSetter(ModContentPack content) : base(content) { ModConfigs = GetSettings<ModConfigGetter>(); }

        public override string SettingsCategory() { return "RimWorld Together"; }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            listingStandard.Label("Debugging");
            if (listingStandard.ButtonTextLabeled("Verbosity mode", $"{ModConfigGetter.CurrentVerboseMode}")) ShowVerbosityMenu();

            listingStandard.GapLine();
            listingStandard.Label("Tweaks");
            if (listingStandard.ButtonTextLabeled("Change mod version [Windows only]", "Change")) { PM_Version.PromptChangeVersion(); }
            listingStandard.CheckboxLabeled("Bypass mod compatibility check", ref ModConfigGetter.BypassModCompatibilityCheck, "Bypass");

            // KMH 26.5.22.1: Self-host configuration. Two viable sources
            // (bundle wins if both are present):
            //   1. Bundle at <ModRoot>/LocalServer/<RID>/{exe|dll}  → auto-detected
            //   2. Optional download URL → opt-in fallback
            //
            // Either one makes the main-menu "Host Local Server" entry
            // appear; if both are absent the feature is hidden entirely.
            listingStandard.GapLine();
            listingStandard.Label("Self-host (Host Local Server)");

            // Platform indicator — players need to know what RID we're
            // looking for in the bundle (and what they'd need to ship if
            // they want to support their own platform).
            string platform = Misc.LocalServerHandler.CurrentRid ?? "<unsupported>";
            listingStandard.LabelDouble("Your platform", platform);

            // Bundle status — read-only indicator; users can't toggle this
            // since it's controlled by the mod packaging.
            string bundleStatus = Misc.LocalServerHandler.HasBundle
                ? $"<color=#80ff80>found for {platform}</color>"
                : "<color=grey>not bundled for your platform</color>";
            listingStandard.LabelDouble("Bundled server", bundleStatus);

            // Download URL — editable. Empty hides the feature unless a
            // bundle was found.
            string urlStatus = string.IsNullOrWhiteSpace(ModConfigGetter.LocalServerDownloadUrl)
                ? "<not set>"
                : ModConfigGetter.LocalServerDownloadUrl;
            if (listingStandard.ButtonTextLabeled("KMH server zip URL", urlStatus))
            {
                PromptLocalServerUrl();
            }

            // Live feature availability summary so the player knows what
            // the main-menu state actually is.
            string availability = Misc.LocalServerHandler.IsAvailable
                ? "<color=#80ff80>Main-menu entry is visible.</color>"
                : "<color=#ffc080>Main-menu entry is hidden — set a URL or bundle the server with the mod for your platform.</color>";
            GUI.color = Color.white;
            listingStandard.Label(availability);

            listingStandard.GapLine();
            listingStandard.Label("DANGEROUS");
            GUI.color = Color.red;
            if (listingStandard.ButtonTextLabeled("Reset account", "Reset")) { ShowResetAccountQuestion(); }
            GUI.color = Color.white;

            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }

        private void ShowVerbosityMenu()
        {
            List<FloatMenuOption> list = new List<FloatMenuOption>();

            List<Tuple<string, Printer.LogImportanceMode>> modes = new List<Tuple<string, Printer.LogImportanceMode>>()
            {
                Tuple.Create("None", Printer.LogImportanceMode.Normal),
                Tuple.Create("Verbose", Printer.LogImportanceMode.Verbose),
                Tuple.Create("Extreme", Printer.LogImportanceMode.Extreme),
                Tuple.Create("Ludicrous", Printer.LogImportanceMode.Ludicrous)
            };

            foreach (Tuple<string, Printer.LogImportanceMode> tuple in modes)
            {
                FloatMenuOption item = new FloatMenuOption(tuple.Item1, delegate
                {
                    ModConfigGetter.CurrentVerboseMode = tuple.Item2;
                });

                list.Add(item);
            }

            Find.WindowStack.Add(new FloatMenu(list));
        }

        /// <summary>
        /// KMH 26.5.22.1: Prompt the user for the KMH server zip URL,
        /// or clear it. Empty input deletes the setting (and hides the
        /// main-menu entry again).
        /// </summary>
        private void PromptLocalServerUrl()
        {
            DLG_Base.PushNewDialog(new DLG_Inputs(
                "Set KMH server zip URL",
                new string[] { "Direct .zip URL (blank to clear)" },
                new bool[] { false },
                delegate
                {
                    string entered = DLG_Inputs.DialogInputResults[0]?.Trim() ?? string.Empty;

                    if (string.IsNullOrEmpty(entered))
                    {
                        ModConfigGetter.LocalServerDownloadUrl = string.Empty;
                        DLG_Base.PushNewDialog(new DLG_Message("Self-host",
                            new[] { "URL cleared. The 'Host Local Server' menu entry will be hidden until you set a new URL." }));
                        return;
                    }

                    if (!Uri.TryCreate(entered, UriKind.Absolute, out Uri parsed)
                        || parsed.Scheme != Uri.UriSchemeHttps)
                    {
                        DLG_Base.PushNewDialog(new DLG_Message("Error",
                            new[] { "URL must be an absolute HTTPS .zip URL.", entered }));
                        return;
                    }

                    ModConfigGetter.LocalServerDownloadUrl = entered;
                    DLG_Base.PushNewDialog(new DLG_Message("Self-host",
                        new[] { "URL saved. The 'Host Local Server' main-menu entry is now available." }));
                }));
        }

        private void ShowResetAccountQuestion()
        {
            DLG_YesNo dialog = new DLG_YesNo("Are you sure you want to RESET your ACCOUNT?",
                delegate
                {
                    PersistentSettings settings = PersistentSettings.Load();
                    settings.UserSettings.Reset();
                    settings.Save();

                    DLG_Base.PushNewDialog(new DLG_Message("MESSAGE", new string[] { "Account has been reset" }));
                });

            DLG_Base.PushNewDialog(dialog);
        }
    }
}
