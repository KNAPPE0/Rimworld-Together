using System.Collections.Generic;
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

namespace GameClient.Dialogs
{
    public class DLG_SiteMenuInfo : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(520f, 320f);

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
            ConfigFile = PM_Sites.SiteValues.Where(f => f.DefName == thingChosen.defName).First();
            Instance = this;

            if (ConfigFile == null)
            {
                IsInvalid = true;
                return;
            }

            ThingDef cost = DefDatabase<ThingDef>.GetNamed(ThingDefOf.Silver.defName);
            if (cost != null) CostThing[cost] = ConfigFile.Cost;

            for (int i = 0; i < ConfigFile.Rewards.Length; i++)
            {
                ThingDef reward = DefDatabase<ThingDef>.GetNamedSilentFail(ConfigFile.Rewards[i].DefName);
                if (reward != null) RewardThing[reward] = ConfigFile.Rewards[i].Amount;
                else Printer.Warning($"{ConfigFile.Rewards[i].DefName} could not be found and won't be added to the list. Double check the def exists.");
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

            string desc = SitePartDef?.description ?? string.Empty;

            Text.Font = GameFont.Small;
            float descH = Text.CalcHeight(desc, rightInner.width - GenUI.ScrollBarWidth);
            float contentH = descH + 10f + 22f + (CostThing.Count * 20f) + 10f + 22f + (RewardThing.Count * 20f) + 6f;

            Rect viewRect = new Rect(0f, 0f, rightInner.width - GenUI.ScrollBarWidth, Mathf.Max(contentH, rightInner.height));

            Widgets.BeginScrollView(rightInner, ref ScrollPosition, viewRect);
            try
            {
                float cy = 0f;

                Widgets.Label(new Rect(0f, cy, viewRect.width, descH), desc);
                cy += descH + 8f;

                Widgets.Label(new Rect(0f, cy, viewRect.width, 22f), "Cost:");
                cy += 22f;

                foreach (var kv in CostThing)
                {
                    Widgets.Label(new Rect(0f, cy, viewRect.width, 20f), $"- {kv.Key.label} {kv.Value}");
                    cy += 20f;
                }

                cy += 8f;
                Widgets.Label(new Rect(0f, cy, viewRect.width, 22f), "Produces:");
                cy += 22f;

                foreach (var kv in RewardThing)
                {
                    Widgets.Label(new Rect(0f, cy, viewRect.width, 20f), $"- {kv.Key.label} {kv.Value}");
                    cy += 20f;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }

            Rect buildBtn = new Rect(rightColumn.x + 6f, inner.yMax - SmallButtonSize.y, rightColumn.width - 12f, SmallButtonSize.y);
            if (Widgets.ButtonText(buildBtn, "Build"))
            {
                PM_Sites.RequestSiteBuild(ConfigFile);
                DLG_SiteMenu.Instance.Close();
                DLG_SiteMenuInfo.Instance.Close();
            }
        }
    }
}