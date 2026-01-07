using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using Shared;
using GameClient.Managers;
using Shared.Files.Sites;
using Shared.Misc;

namespace GameClient.Dialogs
{
    public class RT_Dialog_SiteMenu_Config : RT_Dialog_Base
    {
        public override Vector2 InitialSize => new Vector2(640f, 340f);

        public SitePartDef SitePartDef { get; private set; }
        public SiteType ConfigFile { get; private set; }

        public Dictionary<ThingDef, int> RewardThing { get; private set; } = new Dictionary<ThingDef, int>();

        private bool IsInvalid { get; set; }

        public static RT_Dialog_Base Instance { get; private set; } = null;

        public RT_Dialog_SiteMenu_Config(SitePartDef thingChosen)
        {
            Instance = this;
            SitePartDef = thingChosen;
            Title = thingChosen?.label ?? "Site";
            ConfigFile = SiteManager.SiteValues.Where(f => f.DefName == thingChosen.defName).FirstOrDefault();

            if (ConfigFile == null)
            {
                IsInvalid = true;
                return;
            }

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
                RT_Dialog_Base.PushNewDialog(new RT_Dialog_Message("Error", new string[] { "Site could not be loaded because of invalid configuration" }));
                Close();
                return;
            }

            float y = DrawStandardHeader(inRect, drawTopBorder: true, drawBottomBorder: true, closeX: true);
            if (y < 0f) return;

            Rect outer = new Rect(0f, y, inRect.width, inRect.height - y).ContractedBy(ContentPad);
            Widgets.DrawMenuSection(outer);

            Rect inner = outer.ContractedBy(10f);

            Rect columns = inner;
            float colW = columns.width / 2f;

            Rect leftColumn = new Rect(columns.x, columns.y, colW, columns.height);
            Rect rightColumn = new Rect(columns.x + colW, columns.y, colW, columns.height);

            if (SitePartDef != null)
                Widgets.DrawTextureFitted(leftColumn.ContractedBy(6f), SitePartDef.ExpandingIconTexture, 1f);

            Rect rightInner = rightColumn.ContractedBy(6f);

            string desc = SitePartDef?.description ?? string.Empty;

            Text.Font = GameFont.Small;
            float descH = Text.CalcHeight(desc, rightInner.width - GenUI.ScrollBarWidth);
            float contentH = descH + 10f + 22f + (RewardThing.Count * 28f) + 6f;

            Rect viewRect = new Rect(0f, 0f, rightInner.width - GenUI.ScrollBarWidth, Mathf.Max(contentH, rightInner.height));

            Widgets.BeginScrollView(rightInner, ref ScrollPosition, viewRect);
            try
            {
                float cy = 0f;

                Widgets.Label(new Rect(0f, cy, viewRect.width, descH), desc);
                cy += descH + 8f;

                Widgets.Label(new Rect(0f, cy, viewRect.width, 22f), "Produces:");
                cy += 22f;

                foreach (var kv in RewardThing)
                {
                    Rect row = new Rect(0f, cy, viewRect.width, 26f);

                    Rect labelRect = new Rect(row.x, row.y, row.width - 110f, row.height);
                    Widgets.Label(labelRect, $"- {kv.Key.label} {kv.Value}");

                    Rect btn = new Rect(row.xMax - 100f, row.y, 100f, row.height);
                    if (Widgets.ButtonText(btn, "Choose"))
                    {
                        SiteManager.RequestSiteChangeConfig(ConfigFile, kv.Key.defName);
                        RT_Dialog_SiteMenu.Instance?.Close();
                        RT_Dialog_SiteMenu_Config.Instance?.Close();
                        break;
                    }

                    cy += 28f;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }
    }
}