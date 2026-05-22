using Shared;
using System.Collections.Generic;
using System.Linq;
using TCPNetwork.Packets.ServerBrowser;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs.ServerBrowser
{
    public class DLG_ServerBrowser : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(600f, 400f);

        private List<PKT_ServerTelemetry> Elements { get; set; } = new List<PKT_ServerTelemetry>();
        private readonly string _clientVersion = CommonValues.ExecutableVersion ?? string.Empty;
        private int _matchedCount = 0;

        public DLG_ServerBrowser(List<PKT_ServerTelemetry> elements)
        {
            // Two-tier sort: servers running the client's exact version
            // come first (busiest first within that tier), then everything
            // else (busiest first). Players almost always want to join a
            // version-matched server, so they should appear at the top
            // even when an outdated-but-busier server exists.
            Elements = elements
                .OrderByDescending(e => IsVersionMatch(e))
                .ThenByDescending(e => e.CurrentPopulation)
                .ToList();

            _matchedCount = Elements.Count(IsVersionMatch);
            Title = $"Server Browser [{Elements.Count}]";
            Description = $"{_matchedCount} compatible · {Elements.Count - _matchedCount} on other versions";
        }

        // Match is exact on the version string (e.g. "26.5.22.1 (KMH)").
        // Keeps it simple — a player on KMH 2.7 should never see vanilla
        // RWT servers sorted as compatible even if the numeric part matches.
        private bool IsVersionMatch(PKT_ServerTelemetry e)
        {
            if (e == null) return false;
            return string.Equals(e.Version, _clientVersion, System.StringComparison.OrdinalIgnoreCase);
        }

        public override void DoWindowContents(Rect rect)
        {
            float windowDescriptionDif = Text.CalcSize(Description).y + StandardMargin;
            float descriptionLineDif1 = windowDescriptionDif - Text.CalcSize(Description).y * 0.25f;
            float descriptionLineDif2 = windowDescriptionDif + Text.CalcSize(Description).y * 1.1f;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(DLG_Base.GetRectMiddle(rect) - Text.CalcSize(Title).x / 2, rect.y, Text.CalcSize(Title).x, Text.CalcSize(Title).y), Title);
            Text.Font = GameFont.Small;

            Widgets.DrawLineHorizontal(rect.x, descriptionLineDif1, rect.width);
            Widgets.Label(new Rect(DLG_Base.GetRectMiddle(rect) - Text.CalcSize(Description).x / 2, windowDescriptionDif, Text.CalcSize(Description).x, Text.CalcSize(Description).y), Description);
            Text.Font = GameFont.Medium;
            Widgets.DrawLineHorizontal(rect.x, descriptionLineDif2, rect.width);

            FillMainRect(new Rect(0f, descriptionLineDif2 + 10f, rect.width, rect.height - SlimButtonSize.y - 85f));

            Text.Font = GameFont.Small;

            if (Widgets.ButtonText(DLG_Base.GetRectForLocation(rect, SlimButtonSize, RectLocation.BottomCenter), "Close")) { Close(); }
        }

        private void FillMainRect(Rect mainRect)
        {
            int count = Elements.Count;
            float height = 6f + count * 30f;
            Rect viewRect = new Rect(0f, 0f, mainRect.width - 16f, height);
            Widgets.BeginScrollView(mainRect, ref ScrollPosition, viewRect);
            float num = 0;
            float num2 = ScrollPosition.y - 30f;
            float num3 = ScrollPosition.y + mainRect.height;
            int num4 = 0;

            // Divider between version-matched and mismatched tiers.
            bool dividerDrawn = false;

            for (int i = 0; i < count; i++)
            {
                if (!dividerDrawn && i == _matchedCount && _matchedCount > 0 && _matchedCount < count)
                {
                    if (num > num2 && num < num3)
                    {
                        Rect divRect = new Rect(0f, num + 4f, viewRect.width, 22f);
                        GUI.color = new Color(1f, 0.85f, 0.4f, 0.85f);
                        Text.Anchor = TextAnchor.MiddleCenter;
                        Widgets.Label(divRect, $"— different version ({Elements.Count - _matchedCount}) —");
                        Text.Anchor = TextAnchor.UpperLeft;
                        GUI.color = Color.white;
                    }
                    num += 30f;
                    dividerDrawn = true;
                }

                if (num > num2 && num < num3)
                {
                    Rect rect = new Rect(0f, num, viewRect.width, 30f);
                    DrawCustomRow(rect, Elements[i], num4);
                }

                num += 30f;
                num4++;
            }

            Widgets.EndScrollView();
        }

        private void DrawCustomRow(Rect rect, PKT_ServerTelemetry element, int index)
        {
            Text.Font = GameFont.Small;
            Rect fixedRect = new Rect(new Vector2(rect.x, rect.y + 5f), new Vector2(rect.width - 16f, rect.height - 5f));
            if (index % 2 == 0) Widgets.DrawHighlight(fixedRect);

            // Version cell is colour-coded — green for match, soft red
            // for mismatch — so players see at-a-glance which servers
            // they can actually join.
            bool match = IsVersionMatch(element);
            string versionColored = match
                ? $"<color=#80ff80>[{element.Version}]</color>"
                : $"<color=#ff8080>[{element.Version}]</color>";

            string populationString = $"[{element.CurrentPopulation}/{element.MaxPopulation}]";
            Widgets.Label(fixedRect, $"{versionColored} - {populationString} - {element.Name}");

            if (Widgets.ButtonText(new Rect(new Vector2(rect.xMax - TinyButtonSize.x, rect.yMax - TinyButtonSize.y), TinyButtonSize), "Select"))
            {
                PKT_ServerTelemetry selectedServer = Elements[index];
                DLG_Base.PushNewDialog(new DLG_ServerListing(selectedServer));
            }
        }
    }
}
