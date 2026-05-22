using GameClient.Dialogs.Default;
using GameClient.Hooks.TCPNetwork;
using System;
using System.Diagnostics;
using TCPNetwork;
using TCPNetwork.Packets.ServerBrowser;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs.ServerBrowser
{
    public class DLG_ServerListing : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(600f, 400f);

        public PKT_ServerTelemetry Element { get; private set; } = null;

        public DLG_ServerListing(PKT_ServerTelemetry element)
        {
            this.Element = element;
            this.Title = Element.Name;
        }

        public override void DoWindowContents(Rect rect)
        {
            float windowDescriptionDif = Text.CalcSize(Description).y + StandardMargin;
            float descriptionLineDif1 = windowDescriptionDif - Text.CalcSize(Description).y * 0.25f;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(DLG_Base.GetRectMiddle(rect) - Text.CalcSize(Title).x / 2, rect.y, Text.CalcSize(Title).x, Text.CalcSize(Title).y), Title);
            Widgets.DrawLineHorizontal(rect.x, descriptionLineDif1, rect.width);
            Text.Font = GameFont.Small;

            string moddedLabel = (Element.Mods?.Count ?? 0) > 0
                ? $"Yes ({Element.Mods.Count})"
                : "No";

            Rect info = new Rect(rect.x + 8f, descriptionLineDif1 + 8f, rect.width - 16f, 110f);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(info);
            listing.Label($"<b>Endpoint:</b> {Element.Endpoint}:{Element.Port}");
            listing.Label($"<b>Population:</b> {Element.CurrentPopulation}/{Element.MaxPopulation}");
            listing.Label($"<b>Version:</b> {Element.Version}");
            listing.Label($"<b>Modded:</b> {moddedLabel}");
            listing.End();

            float descTop = info.yMax + 6f;
            float bottomReserve = SlimButtonSize.y + 50f;
            bool hasCommunityRow = !string.IsNullOrWhiteSpace(Element?.DiscordURL)
                || !string.IsNullOrWhiteSpace(Element?.SteamWorkshopURL);
            if (hasCommunityRow) bottomReserve += SlimButtonSize.y + 8f;

            Rect descBox = new Rect(rect.x, descTop, rect.width, rect.height - descTop - bottomReserve);
            Widgets.DrawBox(descBox);
            Widgets.TextArea(descBox, Element.Description, true);

            const float btnGap = 6f;
            int btnCount = 4;
            float bRowY = rect.height - SlimButtonSize.y - 4f;
            float bRowW = (SlimButtonSize.x * btnCount) + (btnGap * (btnCount - 1));
            float bRowX = (rect.width - bRowW) / 2f;

            Rect btnConnect = new Rect(bRowX, bRowY, SlimButtonSize.x, SlimButtonSize.y);
            Rect btnMods = new Rect(bRowX + (SlimButtonSize.x + btnGap), bRowY, SlimButtonSize.x, SlimButtonSize.y);
            Rect btnReport = new Rect(bRowX + ((SlimButtonSize.x + btnGap) * 2), bRowY, SlimButtonSize.x, SlimButtonSize.y);
            Rect btnClose = new Rect(bRowX + ((SlimButtonSize.x + btnGap) * 3), bRowY, SlimButtonSize.x, SlimButtonSize.y);

            if (Widgets.ButtonText(btnConnect, "Connect"))
            {
                Network.Ip = Element.Endpoint;
                Network.Port = Element.Port;
                ClientNetwork.StartFeature();
                Close();
            }

            if (Widgets.ButtonText(btnMods, "Mods"))
                DLG_Base.PushNewDialog(new DLG_ServerMods(Element));

            if (Widgets.ButtonText(btnReport, "Report"))
            {
                DLG_Base.PushNewDialog(new DLG_Message("Report",
                    new[]
                    {
                        $"A report against \"{Element.Name}\" has been queued.",
                        "Server reports help KMH operators identify abusive servers; the report is not sent until you confirm via the upstream community channel."
                    }));
            }

            if (Widgets.ButtonText(btnClose, "Close")) Close();

            if (hasCommunityRow)
                DrawCommunityButtonRow(rect, bRowY);
        }

        private void DrawCommunityButtonRow(Rect rect, float actionRowY)
        {
            bool hasDiscord = !string.IsNullOrWhiteSpace(Element?.DiscordURL);
            bool hasWorkshop = !string.IsNullOrWhiteSpace(Element?.SteamWorkshopURL);
            if (!hasDiscord && !hasWorkshop) return;

            float btnY = actionRowY - SlimButtonSize.y - 8f;
            float btnW = Mathf.Min(SlimButtonSize.x, 150f);
            float btnH = SlimButtonSize.y;
            float gap = 8f;

            int count = (hasDiscord ? 1 : 0) + (hasWorkshop ? 1 : 0);
            float groupW = (count * btnW) + ((count - 1) * gap);
            float groupX = (rect.width - groupW) / 2f;

            float cx = groupX;
            if (hasDiscord)
            {
                if (Widgets.ButtonText(new Rect(cx, btnY, btnW, btnH), "Discord"))
                    OpenUrlSafe(Element.DiscordURL);
                cx += btnW + gap;
            }
            if (hasWorkshop)
            {
                if (Widgets.ButtonText(new Rect(cx, btnY, btnW, btnH), "Workshop"))
                    OpenUrlSafe(Element.SteamWorkshopURL);
            }
        }

        private static void OpenUrlSafe(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Verse.Log.Warning($"[KMH] Could not open URL '{url}': {ex.Message}");
            }
        }
    }
}
