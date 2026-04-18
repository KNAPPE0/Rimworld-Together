using System.Collections.Generic;
using GameClient.Dialogs.Default;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using Shared;
using GameClient.Managers;
using GameClient.Misc;
using Shared.Files.Sites;
using Shared.Misc;
using GameClient.PacketManagers;

namespace GameClient.Dialogs.Sites
{
    public class DLG_SiteMenuInfo : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(560f, 400f);

        public SitePartDef SitePartDef { get; private set; }
        public SiteType ConfigFile { get; private set; }

        public Dictionary<ThingDef, int> CostThing { get; private set; } = new Dictionary<ThingDef, int>();
        public Dictionary<ThingDef, int> RewardThing { get; private set; } = new Dictionary<ThingDef, int>();

        private bool IsInvalid { get; set; }

        public static DLG_SiteMenuInfo Instance { get; private set; }

        public DLG_SiteMenuInfo(SitePartDef thingChosen)
        {
            SitePartDef = thingChosen;
            this.Title = thingChosen.label;
            ConfigFile = PM_Sites.SiteValues?.FirstOrDefault(f => f.DefName == thingChosen.defName);
            Instance = this;

            if (ConfigFile == null)
            {
                IsInvalid = true;
                return;
            }

            ThingDef cost = DefDatabase<ThingDef>.GetNamed(ThingDefOf.Silver.defName);
            if (cost != null) CostThing[cost] = ConfigFile.Cost;

            if (ConfigFile.Rewards != null)
            {
                for (int i = 0; i < ConfigFile.Rewards.Length; i++)
                {
                    ThingDef reward = DefDatabase<ThingDef>.GetNamedSilentFail(ConfigFile.Rewards[i].DefName);
                    if (reward != null) RewardThing[reward] = ConfigFile.Rewards[i].Amount;
                    else Printer.Warning($"{ConfigFile.Rewards[i].DefName} could not be found. Check the def exists.");
                }
            }

            closeOnAccept = false;
            closeOnCancel = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (IsInvalid)
            {
                DLG_Base.PushNewDialog(new DLG_Message("ERROR", new string[] { "Site could not be loaded because of invalid configuration" }));
                Close();
                return;
            }

            float y = DrawStandardHeader(inRect, drawTopBorder: true, drawBottomBorder: true, closeX: true);
            if (y < 0f) return;

            Rect outer = new Rect(0f, y, inRect.width, inRect.height - y).ContractedBy(ContentPad);
            Widgets.DrawMenuSection(outer);
            Rect inner = outer.ContractedBy(10f);

            float footerH = SmallButtonSize.y + 8f;
            Rect columns = new Rect(inner.x, inner.y, inner.width, inner.height - footerH);
            float colW = columns.width / 2f;

            Rect leftColumn = new Rect(columns.x, columns.y, colW, columns.height);
            Rect rightColumn = new Rect(columns.x + colW, columns.y, colW, columns.height);

            if (SitePartDef != null)
                Widgets.DrawTextureFitted(leftColumn.ContractedBy(6f), SitePartDef.ExpandingIconTexture, 1f);

            Rect rightInner = rightColumn.ContractedBy(6f);

            // Build content
            string desc = ConfigFile.Description;
            if (string.IsNullOrEmpty(desc))
                desc = SitePartDef?.description ?? string.Empty;

            Text.Font = GameFont.Small;
            float descH = Text.CalcHeight(desc, rightInner.width - GenUI.ScrollBarWidth);
            float contentH = descH + 10f + 22f + (CostThing.Count * 20f) + 10f + 22f + (RewardThing.Count * 20f) + 60f;

            Rect viewRect = new Rect(0f, 0f, rightInner.width - GenUI.ScrollBarWidth, Mathf.Max(contentH, rightInner.height));

            Widgets.BeginScrollView(rightInner, ref ScrollPosition, viewRect);
            try
            {
                float cy = 0f;

                // Description
                Widgets.Label(new Rect(0f, cy, viewRect.width, descH), desc);
                cy += descH + 8f;

                // Cost section
                GUI.color = new Color(1f, 0.85f, 0.4f);
                Widgets.Label(new Rect(0f, cy, viewRect.width, 22f), "<b>Cost:</b>");
                GUI.color = Color.white;
                cy += 22f;

                foreach (var kv in CostThing)
                {
                    Widgets.Label(new Rect(0f, cy, viewRect.width, 20f), $"  {kv.Key.label.CapitalizeFirst()} x{kv.Value}");
                    cy += 20f;
                }

                cy += 8f;

                // Cycle time
                int cycleMin = ConfigFile.CycleTimeMinutes > 0 ? ConfigFile.CycleTimeMinutes : 30;
                GUI.color = new Color(0.7f, 0.9f, 1f);
                Widgets.Label(new Rect(0f, cy, viewRect.width, 20f), $"<b>Cycle Time:</b> {cycleMin} minutes");
                GUI.color = Color.white;
                cy += 24f;

                // Rewards section
                GUI.color = new Color(0.5f, 1f, 0.5f);
                Widgets.Label(new Rect(0f, cy, viewRect.width, 22f), "<b>Produces per cycle:</b>");
                GUI.color = Color.white;
                cy += 22f;

                foreach (var kv in RewardThing)
                {
                    Widgets.Label(new Rect(0f, cy, viewRect.width, 20f), $"  {kv.Key.label.CapitalizeFirst()} x{kv.Value}");
                    cy += 20f;
                }

                // Estimated value
                cy += 8f;
                float totalValue = 0f;
                foreach (var kv in RewardThing)
                    totalValue += kv.Key.BaseMarketValue * kv.Value;

                double cyclesPerHour = 60.0 / cycleMin;
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                Widgets.Label(new Rect(0f, cy, viewRect.width, 18f), $"Value/cycle: ~${totalValue:F0} | Value/hour: ~${(totalValue * cyclesPerHour):F0}");
                cy += 18f;
                double roi = ConfigFile.Cost / (totalValue * cyclesPerHour);
                Widgets.Label(new Rect(0f, cy, viewRect.width, 18f), $"Break-even: ~{roi:F1} hours");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }
            finally
            {
                Widgets.EndScrollView();
            }

            Rect buildBtn = new Rect(rightColumn.x + 6f, inner.yMax - SmallButtonSize.y, rightColumn.width - 12f, SmallButtonSize.y);
            if (Widgets.ButtonText(buildBtn, $"Build ({ConfigFile.Cost} silver)"))
            {
                PM_Sites.RequestSiteBuild(ConfigFile);
                DLG_SiteMenu.Instance?.Close();
                Close();
            }
        }
    }
}
