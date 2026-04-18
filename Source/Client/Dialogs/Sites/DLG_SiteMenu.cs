using GameClient.Defs;
using Shared.Files.Sites;
using GameClient.Dialogs.Default;
using GameClient.Misc;
using GameClient.PacketManagers;
using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs.Sites
{
    public class DLG_SiteMenu : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(720f, 480f);

        private bool IsInConfigMode { get; set; }

        public static DLG_Base Instance { get; private set; } = null;

        public DLG_SiteMenu(bool configMode)
        {
            Instance = this;
            Title = "Choose a site";
            IsInConfigMode = configMode;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = DrawStandardHeader(inRect, drawTopBorder: true, drawBottomBorder: true, closeX: true);
            if (y < 0f) return;

            Rect listOuter = new Rect(0f, y, inRect.width, inRect.height - y).ContractedBy(ContentPad);

            Widgets.DrawMenuSection(listOuter);
            Rect mainRect = listOuter.ContractedBy(10f);

            float rowH = 56f;
            int count = RTSitePartDefs.Defs?.Length ?? 0;

            float height = 6f + count * rowH;
            Rect viewRect = new Rect(0f, 0f, mainRect.width - GenUI.ScrollBarWidth, height);

            Widgets.BeginScrollView(mainRect, ref ScrollPosition, viewRect);
            try
            {
                float cy = 0f;
                float yMin = ScrollPosition.y - rowH;
                float yMax = ScrollPosition.y + mainRect.height;

                for (int i = 0; i < count; i++)
                {
                    if (cy > yMin && cy < yMax)
                    {
                        Rect row = new Rect(0f, cy, viewRect.width, rowH);
                        DrawCustomRow(row, RTSitePartDefs.Defs[i], i);
                    }
                    cy += rowH;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }

            // KMH: Custom Site button (only in build mode, not config mode)
            if (!IsInConfigMode)
            {
                float btnH = 32f;
                float btnW = 200f;
                Rect customBtnRect = new Rect(
                    (inRect.width - btnW) / 2f,
                    inRect.height - btnH - 8f,
                    btnW, btnH);

                // KMH Custom Sites feature
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.5f, 0.7f, 0.9f);
                Widgets.Label(new Rect(customBtnRect.x, customBtnRect.y - 16f, btnW, 14f), "KMH Feature");
                GUI.color = new Color(0.4f, 0.8f, 1f);
                Text.Font = GameFont.Small;
                if (Widgets.ButtonText(customBtnRect, "Build Custom Site"))
                {
                    GUI.color = Color.white;
                    Close();
                    int tile = SessionHandler.ChosenCaravan?.Tile ?? -1;
                    if (tile >= 0)
                        Find.WindowStack.Add(new DLG_CustomSiteBuild(tile));
                    else
                        DLG_Base.PushNewDialog(new DLG_Message("Error", new string[] { "No caravan selected. Select a caravan on the world map first." }));
                }
                GUI.color = Color.white;
            }
        }

        private void DrawCustomRow(Rect row, SitePartDef thing, int index)
        {
            if (thing == null) return;

            if (index % 2 == 0) Widgets.DrawAltRect(row);
            Widgets.DrawHighlightIfMouseover(row);

            Rect inner = row.ContractedBy(6f, 4f);

            Rect iconRect = new Rect(inner.x, inner.y, 44f, 44f);
            Rect textRect = new Rect(iconRect.xMax + 10f, inner.y, inner.width - 44f - 10f, inner.height);

            Widgets.DrawTextureFitted(iconRect, thing.ExpandingIconTexture, 1f);

            // Show name + cost + description
            SiteType siteType = PM_Sites.SiteValues?.FirstOrDefault(f => f.DefName == thing.defName);
            string label = thing.label ?? thing.defName;
            string cost = siteType != null ? $" ({siteType.Cost} silver)" : "";
            string desc = siteType?.Description ?? thing.description ?? "";

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.Label(new Rect(textRect.x, textRect.y, textRect.width, 20f), $"<b>{label}</b>{cost}");
            
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.Label(new Rect(textRect.x, textRect.y + 20f, textRect.width, textRect.height - 20f), desc);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            if (Widgets.ButtonInvisible(row))
            {
                if (IsInConfigMode) Find.WindowStack.Add(new DLG_SiteMenuConfig(thing));
                else Find.WindowStack.Add(new DLG_SiteMenuInfo(thing));
            }
        }
    }
}