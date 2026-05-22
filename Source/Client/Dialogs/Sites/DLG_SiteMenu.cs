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
        // KMH 26.5.20.1: Bumped 720x480 → 760x540 to accommodate the new
        // 80 px bottom strip (KMH Feature tag + Build Custom Site button)
        // without the site list feeling cramped.
        public override Vector2 InitialSize => new Vector2(760f, 540f);

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

            // KMH 26.5.20.1: Reserve space at the bottom for the
            // "Build Custom Site" button so it doesn't render OVER the
            // scroll list. Bumped from 56 → 80 px so the "KMH Feature"
            // tag above the button has clean vertical breathing room
            // (the old 56 px envelope left the tag clipping into the
            // MenuSection border).
            const float customButtonReserve = 80f;
            float listBottomReserve = IsInConfigMode ? 0f : customButtonReserve;

            Rect listOuter = new Rect(0f, y, inRect.width, inRect.height - y - listBottomReserve).ContractedBy(ContentPad);

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

            // KMH: Custom Site button (only in build mode, not config mode).
            // KMH 26.5.20.1: Centered "KMH Feature" tag + button at the
            // reserved 80 px bottom strip of the dialog. The tag is rendered
            // 28 px above the button (was 16) with a 20 px tall rect (was
            // 14) and 280 px wide rect (was 200) so the text never clips
            // against the menu section border above OR the button below.
            if (!IsInConfigMode)
            {
                const float btnH = 32f;
                const float btnW = 220f;
                Rect customBtnRect = new Rect(
                    (inRect.width - btnW) / 2f,
                    inRect.height - btnH - 12f,
                    btnW, btnH);

                const float tagW = 280f;
                const float tagH = 20f;
                Rect tagRect = new Rect(
                    (inRect.width - tagW) / 2f,
                    customBtnRect.y - tagH - 6f,
                    tagW, tagH);

                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(0.5f, 0.7f, 0.9f);
                Widgets.Label(tagRect, "✦ KMH Feature ✦");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;

                if (Widgets.ButtonText(customBtnRect, "Build Custom Site"))
                {
                    Close();
                    int tile = SessionHandler.ChosenCaravan?.Tile ?? -1;
                    if (tile >= 0)
                        Find.WindowStack.Add(new DLG_CustomSiteBuild(tile));
                    else
                        DLG_Base.PushNewDialog(new DLG_Message("Error", new string[] { "No caravan selected. Select a caravan on the world map first." }));
                }
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