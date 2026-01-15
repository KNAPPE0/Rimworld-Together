using System.Linq;
using GameClient.Managers;
using Shared;
using UnityEngine;
using Verse;
using Shared.Misc;
using TCPNetwork.Packets.ServerBrowser;
using Reachability = TCPNetwork.Packets.ServerBrowser.Reachability;

namespace GameClient.Dialogs
{
    public class RT_Dialog_ServerListing : RT_Dialog_Base
    {
        public override Vector2 InitialSize => new Vector2(680f, 440f);

        public static RT_Dialog_Base Instance { get; private set; }

        private bool FailedToFetchServers { get; set; } = false;

        public RT_Dialog_ServerListing()
        {
            if (!GetServers()) FailedToFetchServers = true;

            Instance = this;
            Title = "Server Browser";
            Description = "This is a list of all publicly available servers!";

            closeOnAccept = false;
            closeOnCancel = true;
        }

        private bool GetServers()
        {
            ServerBrowserManager.GetAllServersAvailable();

            var servers = ServerBrowserManager.AllServers;

            if (servers == null || servers.Length == 0) return false;

            Printer.Warning($"Found {servers.Count()} servers in the server browser", CommonEnumerators.LogImportanceMode.Verbose);
            return true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (FailedToFetchServers)
            {
                Close();
                return;
            }

            float y = DrawStandardHeader(inRect);
            if (y < 0f) return;

            float footerH = DefaultButtonSize.y + FooterPad * 2f;

            Rect contentOuter = new Rect(0f, y, inRect.width, inRect.height - y - footerH).ContractedBy(ContentPad);

            Text.Font = GameFont.Small;
            float descH = Text.CalcHeight(Description ?? string.Empty, contentOuter.width);
            Rect descRect = new Rect(contentOuter.x, contentOuter.y, contentOuter.width, Mathf.Min(descH, 70f));
            Widgets.Label(descRect, Description ?? string.Empty);

            float listTop = descRect.yMax + 8f;
            Rect listOuter = new Rect(contentOuter.x, listTop, contentOuter.width, contentOuter.yMax - listTop);
            Widgets.DrawMenuSection(listOuter);

            Rect listInner = listOuter.ContractedBy(10f);
            FillMainRect(listInner);

            Rect footer = new Rect(0f, inRect.height - footerH, inRect.width, footerH);
            float closeW = Mathf.Min(DefaultButtonSize.x, inRect.width - (FooterPad * 2f));
            Rect closeBtn = new Rect((inRect.width - closeW) / 2f, footer.y + FooterPad, closeW, DefaultButtonSize.y);

            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(closeBtn, "Close"))
                Close();
        }

        private void FillMainRect(Rect mainRect)
        {
            var servers = ServerBrowserManager.AllServers
                .ToList()
                .OrderByDescending(x => x._currentPlayerCount)
                .Where(x => x.Reachability == Reachability.Reachable && x._version == CommonValues.ExecutableVersion)
                .ToArray();

            float rowH = 34f;
            float height = 6f + servers.Length * rowH;

            Rect viewRect = new Rect(0f, 0f, mainRect.width - GenUI.ScrollBarWidth, height);

            Widgets.BeginScrollView(mainRect, ref ScrollPosition, viewRect);
            try
            {
                float y = 0f;
                float yMin = ScrollPosition.y - rowH;
                float yMax = ScrollPosition.y + mainRect.height;

                for (int i = 0; i < servers.Length; i++)
                {
                    if (y > yMin && y < yMax)
                    {
                        Rect row = new Rect(0f, y, viewRect.width, rowH);
                        DrawCustomRow(row, servers[i], i);
                    }
                    y += rowH;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private void DrawCustomRow(Rect row, ServerInfo server, int index)
        {
            if (server == null) return;

            if (index % 2 == 0) Widgets.DrawAltRect(row);
            Widgets.DrawHighlightIfMouseover(row);

            Rect inner = row.ContractedBy(6f, 4f);

            float btnW = 90f;
            Rect btn = new Rect(inner.xMax - btnW, inner.y, btnW, inner.height);

            Rect labelRect = new Rect(inner.x, inner.y, inner.width - btnW - 8f, inner.height);

            Text.Font = GameFont.Small;
            Widgets.LabelEllipses(labelRect, $"{server._name}  |  {server._ip}  |  {server._currentPlayerCount}/{server._maximumPlayerCount}");

            if (Widgets.ButtonText(btn, "Select"))
                RT_Dialog_Base.PushNewDialog(new RT_Dialog_ServerListingInfo(server));
        }
    }
}