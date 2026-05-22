using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TCPNetwork.Packets.ServerBrowser;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs.ServerBrowser
{
    public class DLG_ServerBrowser : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(600f, 400f);

        private List<PKT_ServerTelemetry> Elements { get; set; } = new List<PKT_ServerTelemetry>();

        public DLG_ServerBrowser(List<PKT_ServerTelemetry> elements)
        {
            // KMH 26.5.22.1: Show the count right in the title for fast
            // glance — ported from upstream's title rework (Apr 2026).
            this.Title = $"Server Browser [{elements.Count()}]";
            this.Description = "Servers ordered by population (busiest first)";
            this.Elements = elements.OrderByDescending(fetch => fetch.CurrentPopulation).ToList();
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
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(DLG_Base.GetRectMiddle(rect) - Text.CalcSize(Description).x / 2, windowDescriptionDif, Text.CalcSize(Description).x, Text.CalcSize(Description).y), Description);
            Text.Font = GameFont.Medium;
            Widgets.DrawLineHorizontal(rect.x, descriptionLineDif2, rect.width);

            FillMainRect(new Rect(0f, descriptionLineDif2 + 10f, rect.width, rect.height - SlimButtonSize.y - 85f));

            Text.Font = GameFont.Small;

            if (Widgets.ButtonText(DLG_Base.GetRectForLocation(rect, SlimButtonSize, RectLocation.BottomCenter), "Close")) { Close(); }
        }

        private void FillMainRect(Rect mainRect)
        {
            float height = 6f + Elements.Count() * 30f;
            Rect viewRect = new Rect(0f, 0f, mainRect.width - 16f, height);
            Widgets.BeginScrollView(mainRect, ref ScrollPosition, viewRect);
            float num = 0;
            float num2 = ScrollPosition.y - 30f;
            float num3 = ScrollPosition.y + mainRect.height;
            int num4 = 0;

            for (int i = 0; i < Elements.Count(); i++)
            {
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

            string versionString = $"[{element.Version}]";
            string populationString = $"[{element.CurrentPopulation}/{element.MaxPopulation}]";
            Widgets.Label(fixedRect, $"{versionString} - {populationString} - {element.Name}");
            if (Widgets.ButtonText(new Rect(new Vector2(rect.xMax - TinyButtonSize.x, rect.yMax - TinyButtonSize.y), TinyButtonSize), "Select"))
            {
                PKT_ServerTelemetry selectedServer = Elements[index];
                DLG_Base.PushNewDialog(new DLG_ServerListing(selectedServer));
            }
        }
    }
}
