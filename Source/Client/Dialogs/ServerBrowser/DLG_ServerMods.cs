using GameClient.Hooks.TCPNetwork;
using Shared.Files.Configs.Mods;
using System;
using System.Linq;
using TCPNetwork;
using TCPNetwork.Packets.ServerBrowser;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs.ServerBrowser
{
    public class DLG_ServerMods : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(600f, 400f);

        public PKT_ServerTelemetry Element { get; private set; } = null;

        public DLG_ServerMods(PKT_ServerTelemetry element)
        {
            this.Element = element;
            // KMH 26.5.22.1: Show the mod count in the title — ported
            // from upstream RWT (Apr 2026 — "Server mod list now
            // displays mod count"). Saves the user from scrolling to
            // count rows.
            int count = element?.Mods?.Count ?? 0;
            this.Title = $"Server Mods [{count}]";
        }

        public override void DoWindowContents(Rect rect)
        {
            float windowDescriptionDif = Text.CalcSize(Description).y + StandardMargin;
            float descriptionLineDif1 = windowDescriptionDif - Text.CalcSize(Description).y * 0.25f;

            Text.Font = GameFont.Medium;

            Widgets.Label(new Rect(DLG_Base.GetRectMiddle(rect) - Text.CalcSize(Title).x / 2, rect.y, Text.CalcSize(Title).x, Text.CalcSize(Title).y), Title);

            Widgets.DrawLineHorizontal(rect.x, descriptionLineDif1, rect.width);

            Text.Font = GameFont.Small;

            FillMainRect(new Rect(rect.x, descriptionLineDif1 + 10f, rect.width, rect.height - SlimButtonSize.y - 50f));

            if (Widgets.ButtonText(DLG_Base.GetRectForLocation(rect, SlimButtonSize, RectLocation.BottomCenter), "Close")) Close();
        }

        private void FillMainRect(Rect mainRect)
        {
            float height = 6f + Element.Mods.Count() * 30f;
            Rect viewRect = new Rect(0f, 0f, mainRect.width - 16f, height);
            Widgets.BeginScrollView(mainRect, ref ScrollPosition, viewRect);
            float num = 0;
            float num2 = ScrollPosition.y - 30f;
            float num3 = ScrollPosition.y + mainRect.height;
            int num4 = 0;

            for (int i = 0; i < Element.Mods.Count(); i++)
            {
                if (num > num2 && num < num3)
                {
                    Rect rect = new Rect(0f, num, viewRect.width, 30f);
                    DrawCustomRow(rect, Element.Mods[i], num4);
                }

                num += 30f;
                num4++;
            }

            Widgets.EndScrollView();
        }

        private void DrawCustomRow(Rect rect, ModConfig element, int index)
        {
            Text.Font = GameFont.Small;
            Rect fixedRect = new Rect(new Vector2(rect.x, rect.y + 5f), new Vector2(rect.width - 16f, rect.height - 5f));
            if (index % 2 == 0) Widgets.DrawHighlight(fixedRect);

            Widgets.Label(fixedRect, $"{element.FileName}");
        }
    }
}
