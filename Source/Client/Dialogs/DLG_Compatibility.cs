using GameClient.Core.Configs;
using GameClient.Dialogs.Default;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs
{
    public class DLG_Compatibility : DLG_Base
    {
        private List<string> Elements { get; set; } = new List<string>();

        public DLG_Compatibility(List<string> elements)
        {
            Title = "Potentially incompatible mods found";
            Description = "Continue anyway, or cancel and disable these mods?";
            Elements = elements;
        }

        public override void DoWindowContents(Rect rect)
        {
            float windowDescriptionDif = Text.CalcSize(Description).y + StandardMargin;
            float descriptionLineDif1 = windowDescriptionDif - Text.CalcSize(Description).y * 0.25f;
            float descriptionLineDif2 = windowDescriptionDif + Text.CalcSize(Description).y * 1.1f;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(DLG_Base.GetRectMiddle(rect) - Text.CalcSize(Title).x / 2, rect.y,
                Text.CalcSize(Title).x, Text.CalcSize(Title).y), Title);

            Widgets.DrawLineHorizontal(rect.x, descriptionLineDif1, rect.width);

            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(DLG_Base.GetRectMiddle(rect) - Text.CalcSize(Description).x / 2,
                windowDescriptionDif, Text.CalcSize(Description).x, Text.CalcSize(Description).y), Description);
            Text.Font = GameFont.Medium;

            Widgets.DrawLineHorizontal(rect.x, descriptionLineDif2, rect.width);

            FillMainRect(new Rect(0f, descriptionLineDif2 + 10f, rect.width, rect.height - SlimButtonSize.y - 85f));

            // Buttons size to their label so "Continue anyway" never gets clipped.
            // 100px (SlimButtonSize.x) was too narrow for the medium-font label.
            Text.Font = GameFont.Small;
            const string continueLabel = "Continue anyway";
            const string cancelLabel = "Cancel";
            float pad = 28f;
            float continueW = Mathf.Max(SlimButtonSize.x, Text.CalcSize(continueLabel).x + pad);
            float cancelW = Mathf.Max(SlimButtonSize.x, Text.CalcSize(cancelLabel).x + pad);
            float btnH = SlimButtonSize.y;
            float btnY = rect.height - btnH - 4f;
            float gap = 8f;
            float groupW = continueW + cancelW + gap;
            float groupX = (rect.width - groupW) / 2f;

            Rect continueRect = new Rect(groupX, btnY, continueW, btnH);
            Rect cancelRect = new Rect(groupX + continueW + gap, btnY, cancelW, btnH);

            if (Widgets.ButtonText(continueRect, continueLabel))
            {
                ModConfigGetter.BypassModCheckThisSession = true;
                DLG_Base.PushNewDialog(new DLG_Message("Mod check",
                    new[] { "Mod compatibility won't be re-checked until you restart RimWorld." }));
                Close();
            }

            if (Widgets.ButtonText(cancelRect, cancelLabel))
            {
                if (OnAccept != null) OnAccept.Invoke();
                Close();
            }
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

            for (int i = 0; i < count; i++)
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

        private void DrawCustomRow(Rect rect, string element, int index)
        {
            Text.Font = GameFont.Small;
            Rect fixedRect = new Rect(new Vector2(rect.x, rect.y + 5f), new Vector2(rect.width - 16f, rect.height - 5f));
            if (index % 2 == 0) Widgets.DrawHighlight(fixedRect);

            Widgets.Label(fixedRect, element);
        }
    }
}
