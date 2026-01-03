using System.Linq;
using GameClient.Managers;
using GameClient.Misc;
using Rimworld_Together_Master_Server.Data;
using Shared;
using UnityEngine;
using Verse;
using Reachability = Rimworld_Together_Master_Server.Data.Reachability;
using Shared.Misc;

namespace GameClient.Dialogs
{
    public class RT_Dialog_ServerListing : RT_Dialog_Base
    {
        public override Vector2 InitialSize => new Vector2(650f, 400f);

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

        public override void DoWindowContents(Rect rect)
        {
            if (FailedToFetchServers)
            {
                Close();
                return;
            }

            float centeredX = rect.width / 2;

            float descH = Text.CalcSize(Description).y;
            float windowDescriptionDif = descH + StandardMargin;
            float descriptionLineDif1 = windowDescriptionDif - descH * 0.25f;
            float descriptionLineDif2 = windowDescriptionDif + descH * 1.1f;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(centeredX - Text.CalcSize(Title).x / 2, rect.y, Text.CalcSize(Title).x, Text.CalcSize(Title).y), Title);

            Widgets.DrawLineHorizontal(rect.x, descriptionLineDif1, rect.width);

            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(centeredX - Text.CalcSize(Description).x / 2, windowDescriptionDif, Text.CalcSize(Description).x, descH), Description);

            Text.Font = GameFont.Medium;
            Widgets.DrawLineHorizontal(rect.x, descriptionLineDif2, rect.width);

            FillMainRect(new Rect(0f, descriptionLineDif2 + 10f, rect.width, rect.height - DefaultButtonSize.y - 85f));

            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(new Rect(new Vector2(centeredX - DefaultButtonSize.x / 2, rect.yMax - DefaultButtonSize.y), DefaultButtonSize), "Close"))
                Close();
        }

        private void FillMainRect(Rect mainRect)
        {
            var servers = ServerBrowserManager.AllServers
                .ToList()
                .OrderByDescending(x => x._currentPlayerCount)
                .Where(x => x.Reachability == Reachability.Reachable && x._version == CommonValues.ExecutableVersion)
                .ToArray();

            float rowH = 30f;
            float height = 6f + servers.Length * rowH;

            Rect viewRect = new Rect(0f, 0f, mainRect.width - 16f, height);

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

        private void DrawCustomRow(Rect rect, ServerInfo server, int index)
        {
            Text.Font = GameFont.Small;

            Rect fixedRect = new Rect(rect.x, rect.y + 5f, rect.width - 16f, rect.height - 5f);
            if (index % 2 == 0) Widgets.DrawHighlight(fixedRect);

            Widgets.Label(fixedRect, $"{server._name} - {server._ip} - {server._currentPlayerCount} / {server._maximumPlayerCount}");

            Rect btn = new Rect(rect.xMax - SmallerButtonSize.x - 5f, rect.y + (rect.height - TinyButtonSize.y) / 2f, SmallerButtonSize.x, TinyButtonSize.y);
            if (Widgets.ButtonText(btn, "Select"))
                RT_Dialog_Base.PushNewDialog(new RT_Dialog_ServerListingInfo(server));
        }
    }
}