using GameClient.Core.Configs;
using GameClient.Dialogs.Default;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs
{
    /// <summary>
    /// KMH 26.5.22.1: Mod-compatibility report dialog.
    ///
    /// <para>Listed when KMH's Harmony patches collide with another mod's
    /// patches at the same target methods. Two buttons:</para>
    /// <list type="bullet">
    ///   <item><b>Continue anyway</b> — flips
    ///   <see cref="ModConfigGetter.BypassModCheckThisSession"/> and
    ///   closes. Future <see cref="Misc.HarmonyHandler.CheckForModCollision"/>
    ///   calls in this RimWorld session will skip the scan. Resets on
    ///   restart.</item>
    ///   <item><b>Cancel</b> — just closes. The user is expected to
    ///   disable the offending mods or set the persistent bypass in mod
    ///   settings.</item>
    /// </list>
    ///
    /// <para>Ported from upstream RWT's "Continue/Cancel" UX (Apr 2026)
    /// with KMH styling — kept the two-line title+description header
    /// and the scrollable mod list KMH already had.</para>
    /// </summary>
    public class DLG_Compatibility : DLG_Base
    {
        private List<string> Elements { get; set; } = new List<string>();

        public DLG_Compatibility(List<string> elements)
        {
            // KMH 26.5.22.1: Softer phrasing — these aren't necessarily
            // "problematic", they're just touching the same Harmony
            // patch targets. False positives are common with QOL mods.
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

            // KMH 26.5.22.1: Two side-by-side buttons replacing the old
            // single "Close". GetRectForLocation positions to the bottom
            // half — we compute left/right halves manually so KMH doesn't
            // depend on upstream's FillLocation enum (which KMH doesn't
            // have).
            float btnW = Mathf.Min(SlimButtonSize.x, (rect.width - 24f) / 2f);
            float btnH = SlimButtonSize.y;
            float btnY = rect.height - btnH - 4f;
            float gap = 8f;
            float groupW = (btnW * 2f) + gap;
            float groupX = (rect.width - groupW) / 2f;

            Rect continueRect = new Rect(groupX, btnY, btnW, btnH);
            Rect cancelRect = new Rect(groupX + btnW + gap, btnY, btnW, btnH);

            if (Widgets.ButtonText(continueRect, "Continue anyway"))
            {
                ModConfigGetter.BypassModCheckThisSession = true;
                DLG_Base.PushNewDialog(new DLG_Message("Mod check",
                    new[] { "Mod compatibility won't be re-checked until you restart RimWorld." }));
                Close();
            }

            if (Widgets.ButtonText(cancelRect, "Cancel"))
            {
                if (OnAccept != null) OnAccept.Invoke();
                Close();
            }
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

        private void DrawCustomRow(Rect rect, string element, int index)
        {
            Text.Font = GameFont.Small;
            Rect fixedRect = new Rect(new Vector2(rect.x, rect.y + 5f), new Vector2(rect.width - 16f, rect.height - 5f));
            if (index % 2 == 0) Widgets.DrawHighlight(fixedRect);

            Widgets.Label(fixedRect, element);
        }
    }
}
