using GameClient.Dialogs.Default;
using GameClient.Managers;
using GameClient.PacketManagers;
using RimWorld;
using Shared.Files.Economy;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs.Economy
{
    /// <summary>
    /// Compose + post a new quest. The bounty (silver + optional items) is
    /// escrowed from the poster's treasury at server time.
    /// </summary>
    public class DLG_PostQuest : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(640f, 540f);

        private QuestKind _kind = QuestKind.DeliverItem;
        private QuestVisibility _visibility = QuestVisibility.Public;
        private string _title = "";
        private string _description = "";
        private string _itemDefName = "Steel";
        private int _itemQty = 50;
        private int _bountySilver = 500;

        public DLG_PostQuest()
        {
            Title = "Post Quest";
            closeOnCancel = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width, 28f), "Post a Quest");
            Text.Font = GameFont.Small;

            float y = 36f;
            Widgets.DrawLineHorizontal(0f, y, rect.width); y += 8f;

            // Kind selector
            Widgets.Label(new Rect(0f, y, 120f, 22f), "Type:");
            if (Widgets.ButtonText(new Rect(120f, y, 200f, 26f), _kind.ToString()))
            {
                List<FloatMenuOption> opts = new List<FloatMenuOption>
                {
                    new FloatMenuOption("Deliver Item (server-verified)", () => _kind = QuestKind.DeliverItem),
                    new FloatMenuOption("Bounty (free-form, you confirm)", () => _kind = QuestKind.Bounty),
                };
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            y += 34f;

            // Visibility selector — only shown when the player is in a guild;
            // solo posters always default to Public (no guild scope to honour).
            bool inGuild = TreasuryClientCache.HasSnapshot && TreasuryClientCache.IsGuildOwned;
            if (inGuild)
            {
                Widgets.Label(new Rect(0f, y, 120f, 22f), "Visibility:");
                if (Widgets.ButtonText(new Rect(120f, y, 200f, 26f), _visibility.ToString()))
                {
                    List<FloatMenuOption> visOpts = new List<FloatMenuOption>
                    {
                        new FloatMenuOption("Public — anyone can see + claim", () => _visibility = QuestVisibility.Public),
                        new FloatMenuOption("Guild Only — only your guild can see + claim", () => _visibility = QuestVisibility.GuildOnly)
                    };
                    Find.WindowStack.Add(new FloatMenu(visOpts));
                }
                y += 34f;
            }
            else
            {
                _visibility = QuestVisibility.Public;
            }

            // Title
            Widgets.Label(new Rect(0f, y, 120f, 22f), "Title:");
            _title = Widgets.TextField(new Rect(120f, y, rect.width - 120f, 26f), _title ?? "");
            y += 34f;

            // Description
            Widgets.Label(new Rect(0f, y, 120f, 22f), "Description:");
            _description = Widgets.TextArea(new Rect(120f, y, rect.width - 120f, 80f), _description ?? "");
            y += 88f;

            // KMH 2.7: Item picker — replaces the old "type the raw defName"
            // text input with a clickable button that opens the searchable
            // catalog picker. Players never need to know "Apparel_Tribalwear"
            // again — they search "tribalwear" in the picker and click.
            if (_kind == QuestKind.DeliverItem)
            {
                Widgets.Label(new Rect(0f, y, 120f, 22f), "Item:");
                ThingDef pickedDef = string.IsNullOrEmpty(_itemDefName)
                    ? null
                    : DefDatabase<ThingDef>.GetNamedSilentFail(_itemDefName);
                string itemBtnText = pickedDef != null
                    ? $"{pickedDef.label.CapitalizeFirst()}  ({_itemDefName})"
                    : "Choose an item…";
                if (Widgets.ButtonText(new Rect(120f, y, rect.width - 120f, 26f), itemBtnText))
                    OpenItemPicker();
                y += 34f;

                Widgets.Label(new Rect(0f, y, 120f, 22f), "Quantity:");
                string qtyStr = Widgets.TextField(new Rect(120f, y, 100f, 26f), _itemQty.ToString());
                if (int.TryParse(qtyStr, out int q) && q > 0) _itemQty = q;
                y += 34f;
            }

            // Bounty
            Widgets.DrawLineHorizontal(0f, y, rect.width); y += 6f;
            Widgets.Label(new Rect(0f, y, rect.width, 20f), "<b>Bounty</b>");
            y += 22f;

            Widgets.Label(new Rect(0f, y, 120f, 22f), "Silver:");
            string silverStr = Widgets.TextField(new Rect(120f, y, 140f, 26f), _bountySilver.ToString());
            if (int.TryParse(silverStr, out int s) && s >= 0) _bountySilver = s;

            // Treasury balance hint
            int treasurySilver = TreasuryClientCache.HasSnapshot ? TreasuryClientCache.SilverBalance : 0;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(280f, y + 4f, 320f, 22f),
                $"(Treasury: {treasurySilver}s — must hold the bounty)");
            GUI.color = Color.white;
            y += 34f;

            // Hints
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(0f, y, rect.width, 60f),
                "• The bounty silver is taken from your treasury when you post.\n" +
                "• Lifetime: 7 days. Unclaimed quests refund automatically on expiry.\n" +
                "• Open the Treasury dialog first if you need to deposit silver.");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // Buttons
            float btnY = rect.height - 38f;
            if (Widgets.ButtonText(new Rect(0f, btnY, 120f, 32f), "Cancel"))
                Close();
            if (Widgets.ButtonText(new Rect(rect.width - 120f, btnY, 120f, 32f), "Post"))
                Submit();
        }

        private void Submit()
        {
            if (string.IsNullOrWhiteSpace(_title)) return;

            QuestFile draft = new QuestFile
            {
                Kind = _kind,
                Visibility = _visibility,
                Title = _title,
                Description = _description,
                BountySilver = _bountySilver,
                TargetItemDefName = _kind == QuestKind.DeliverItem ? _itemDefName : string.Empty,
                TargetItemQty = _kind == QuestKind.DeliverItem ? _itemQty : 0
            };

            PM_Quest.Post(draft);
            Close();
        }

        // KMH 2.7: Lightweight wrapper around DLG_MarketItemPicker that ignores
        // the source/qty/price fields and just snaps the chosen defName into
        // the quest draft. Reuses the same catalog-search UX so the quest
        // post flow matches the marketplace listing flow.
        private void OpenItemPicker()
        {
            // KMH 26.5.20.1: catalogOnly = true → poster doesn't need to OWN
            // the item, they're just naming what someone else should deliver.
            // Previously the confirm button was gated on the poster having
            // the requested item in their own caravan/treasury, which made
            // posting "Deliver 100 plasteel" impossible if you had no plasteel.
            Find.WindowStack.Add(new DLG_MarketItemPicker(
                GameClient.Misc.SessionHandler.ChosenCaravan,
                title: "Pick item to deliver",
                confirmLabel: "Use this item",
                askForPrice: false,
                onConfirm: (entry, qty, _, _src) =>
                {
                    _itemDefName = entry.DefName;
                    if (qty > 0) _itemQty = qty;
                },
                catalogOnly: true));
        }
    }
}
