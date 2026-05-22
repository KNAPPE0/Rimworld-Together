using GameClient.Dialogs.Default;
using Shared;
using System;
using System.Diagnostics;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs
{
    // Two-step welcome flow: KMH credits, then official RWT credits.
    public class DLG_Welcome : DLG_Base
    {
        public enum Step { KMH, OfficialRWT }

        public override Vector2 InitialSize => new Vector2(560f, 380f);

        private readonly Step _step;

        public DLG_Welcome(Step step)
        {
            _step = step;
            if (step == Step.KMH)
            {
                Title = "Welcome to RimWorld Together (KMH Edition)";
                Description = "This is a customized edition of the official RimWorld Together mod with extra economy, Discord, leaderboard, and self-host features.";
            }
            else
            {
                Title = "Built on the official RimWorld Together";
                Description = "KMH wouldn't exist without the original RimWorld Together mod by Nova and Company. If you enjoy KMH, please show the official team some love.";
            }
        }

        public override void DoWindowContents(Rect rect)
        {
            float y = 0f;

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(0f, y, rect.width, 40f), Title);
            y += 44f;
            Widgets.DrawLineHorizontal(0f, y, rect.width);
            y += 12f;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            Rect descRect = new Rect(8f, y, rect.width - 16f, 70f);
            Widgets.Label(descRect, Description);
            y += 80f;

            float btnY = y;
            const float btnW = 200f;
            const float btnH = 38f;
            const float gap = 10f;

            if (_step == Step.KMH)
            {
                DrawLinkButton(rect, ref btnY, btnW, btnH, gap, "KMH Discord",              KMHProject.DiscordUrl);
                DrawLinkButton(rect, ref btnY, btnW, btnH, gap, "KMH GitHub",               KMHProject.GitHubUrl);
                DrawLinkButton(rect, ref btnY, btnW, btnH, gap, "KMH Steam Workshop",       KMHProject.SteamWorkshopUrl);
            }
            else
            {
                DrawLinkButton(rect, ref btnY, btnW, btnH, gap, "Official RWT Discord",     KMHProject.Official.DiscordUrl);
                DrawLinkButton(rect, ref btnY, btnW, btnH, gap, "Official RWT GitHub",      KMHProject.Official.GitHubUrl);
                DrawLinkButton(rect, ref btnY, btnW, btnH, gap, "Official Steam Workshop",  KMHProject.Official.SteamWorkshopUrl);
            }

            // Step 1 chains into Step 2; Step 2 closes the welcome flow.
            Rect okRect = new Rect((rect.width - 150f) / 2f, rect.height - 44f, 150f, 38f);
            if (Widgets.ButtonText(okRect, "OK"))
            {
                Close();
                if (_step == Step.KMH)
                {
                    PushNewDialog(new DLG_Welcome(Step.OfficialRWT));
                }
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static void DrawLinkButton(Rect rect, ref float y, float w, float h, float gap, string label, string url)
        {
            Rect btn = new Rect((rect.width - w) / 2f, y, w, h);
            if (Widgets.ButtonText(btn, label)) OpenUrl(url);
            y += h + gap;
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                Log.Warning($"[KMH] Could not open URL '{url}': {ex.Message}");
                PushNewDialog(new DLG_Message("Error",
                    new[] { "Could not open the URL in your browser.", url }));
            }
        }
    }
}