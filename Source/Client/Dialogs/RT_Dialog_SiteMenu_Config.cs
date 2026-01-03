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

namespace GameClient.Dialogs
{
    public class RT_Dialog_SiteMenu_Config : RT_Dialog_Base
    {
        public override Vector2 InitialSize => new Vector2(600f, 250f);

        public SitePartDef SitePartDef { get; private set; }
        public SiteType ConfigFile { get; private set; }

        public Dictionary<ThingDef, int> CostThing { get; private set; } = new Dictionary<ThingDef, int>();
        public Dictionary<ThingDef, int> RewardThing { get; private set; } = new Dictionary<ThingDef, int>();

        private bool IsInvalid { get; set; }

        public static RT_Dialog_Base Instance { get; private set; } = null;

        public RT_Dialog_SiteMenu_Config(SitePartDef thingChosen)
        {
            Instance = this;
            SitePartDef = thingChosen;
            Title = thingChosen.label;
            ConfigFile = SiteManager.SiteValues.Where(f => f.DefName == thingChosen.defName).FirstOrDefault();

            if (ConfigFile == null)
            {
                IsInvalid = true;
                return;
            }

            ThingDef cost = DefDatabase<ThingDef>.GetNamed(ThingDefOf.Silver.defName);
            if (cost != null) CostThing.Add(cost, ConfigFile.Cost);

            for (int i = 0; i < ConfigFile.Rewards.Length; i++)
            {
                ThingDef reward = DefDatabase<ThingDef>.GetNamedSilentFail(ConfigFile.Rewards[i].DefName);
                if (reward != null) RewardThing.Add(reward, ConfigFile.Rewards[i].Amount);
                else Printer.Warning($"{ConfigFile.Rewards[i].DefName} could not be found and won't be added to the list. Double check the def exists.");
            }
        }

        public override void DoWindowContents(Rect mainRect)
        {
            if (IsInvalid)
            {
                RT_Dialog_Base.PushNewDialog(new RT_Dialog_Message("ERROR", new string[] { "Site could not be loaded because of invalid configuration" }));
                Close();
                return;
            }

            Widgets.DrawLineHorizontal(mainRect.x, mainRect.y - 1, mainRect.width);
            Widgets.DrawLineHorizontal(mainRect.x, mainRect.yMax + 1, mainRect.width);

            if (Widgets.CloseButtonFor(mainRect)) { Close(); return; }

            float centeredX = mainRect.width / 2;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(centeredX - Text.CalcSize(Title).x / 2, mainRect.y, Text.CalcSize(Title).x, Text.CalcSize(Title).y), Title);

            Rect leftColumn = new Rect(mainRect.x, mainRect.y + 30f, mainRect.width / 2, mainRect.height - 20f);
            Widgets.DrawTextureFitted(leftColumn, SitePartDef.ExpandingIconTexture, 1f);

            Rect rightColumn = new Rect(mainRect.width / 2, mainRect.y + 30f, mainRect.width / 2, mainRect.height - 20f);

            float descH = Text.CalcHeight(SitePartDef.description, rightColumn.width - 16f);
            float contentH = descH + 20f + RewardThing.Count * 25f + 10f;

            Rect viewRect = new Rect(0f, 0f, rightColumn.width - 16f, contentH);

            Widgets.BeginScrollView(rightColumn, ref ScrollPosition, viewRect);
            try
            {
                Text.Font = GameFont.Small;
                float y = 0f;

                Widgets.Label(new Rect(0f, y, viewRect.width, descH), SitePartDef.description);
                y += descH + 6f;

                Widgets.Label(new Rect(0f, y, viewRect.width, 20f), "Produces:");
                y += 20f;

                foreach (ThingDef thing in RewardThing.Keys)
                {
                    Rect row = new Rect(0f, y, viewRect.width, 25f);
                    Widgets.Label(new Rect(row.x, row.y, row.width - 110f, row.height), $"- {thing.label} {RewardThing[thing]}");

                    Rect btn = new Rect(row.xMax - 100f, row.y, 100f, row.height);
                    if (Widgets.ButtonText(btn, "Choose"))
                    {
                        SiteManager.RequestSiteChangeConfig(ConfigFile, thing.defName);
                        RT_Dialog_SiteMenu.Instance.Close();
                        RT_Dialog_SiteMenu_Config.Instance.Close();
                    }

                    y += 25f;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }
    }
}