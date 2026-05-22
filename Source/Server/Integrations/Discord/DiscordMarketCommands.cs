using Discord;
using Discord.WebSocket;
using GameServer.Managers;
using Shared;
using Shared.Files.Economy;
using Shared.Misc;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TCPNetwork.Files.Client;

namespace GameServer.Integrations.Discord
{
    /// <summary>
    /// Discord-side marketplace commands. Lets a linked player browse and
    /// transact on the in-game marketplace from Discord.
    ///
    /// Commands (all start with "!" — see DiscordBridge wiring):
    ///   !market / !shop / !list / !listings           — paginated open listings
    ///   !market &lt;page&gt;                                — page N
    ///   !market mine                                  — only your own listings
    ///   !find &lt;name&gt;                                  — search listings by item label
    ///   !buy &lt;id&gt; [qty]                              — buy from listing; defaults qty=1
    ///   !sell / !post &lt;name|def&gt; &lt;qty&gt; &lt;price&gt;     — list from your treasury (item by friendly name OR defName)
    ///   !cancel &lt;id&gt;                                 — cancel your own listing (refund to treasury)
    ///   !treasury / !vault / !wallet                  — show your treasury balance + items
    ///   !quests / !questboard                         — list open quests
    ///   !leaderboard / !lb / !top [sort]              — top guilds
    ///
    /// !sell now accepts a friendly item name like "plasteel" or
    /// "melee weapon" — the server resolves to the right defName via
    /// <see cref="ItemLabelCache"/> (populated from connected clients on login).
    /// You no longer need to know the raw RimWorld defName.
    ///
    /// Linking is required for everything except !market browsing (which is
    /// public read-only).
    /// </summary>
    internal static class DiscordMarketCommands
    {
        private const int PageSize = 8;
        private const int CommandCooldownMs = 2_000;

        private static readonly ConcurrentDictionary<ulong, DateTime> LastCommandUtc = new ConcurrentDictionary<ulong, DateTime>();

        // -- entry point --

        /// <summary>
        /// Try to dispatch a command from a Discord user. Returns true if the
        /// content matched a market/economy command (so the caller doesn't
        /// also broadcast it as in-game chat).
        /// </summary>
        public static async Task<bool> TryDispatchAsync(SocketMessage raw, string content)
        {
            if (string.IsNullOrEmpty(content) || content[0] != '!') return false;

            // Per-user command cooldown.
            DateTime now = DateTime.UtcNow;
            DateTime last = LastCommandUtc.GetOrAdd(raw.Author.Id, DateTime.MinValue);
            if ((now - last).TotalMilliseconds < CommandCooldownMs)
                return true; // swallow without acting — prevents flood

            string[] parts = content.Substring(1).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;
            string cmd = parts[0].ToLowerInvariant();

            switch (cmd)
            {
                case "market":
                case "shop":
                case "listings":
                case "list":               // KMH: natural English — "show me the list"
                    LastCommandUtc[raw.Author.Id] = now;
                    await HandleMarketAsync(raw, parts);
                    return true;

                case "find":               // search listings by item label
                case "search":
                    LastCommandUtc[raw.Author.Id] = now;
                    await HandleFindAsync(raw, parts);
                    return true;

                case "items":              // discover what defNames the server knows
                case "catalog":
                    LastCommandUtc[raw.Author.Id] = now;
                    await HandleCatalogAsync(raw, parts);
                    return true;

                case "buy":
                    LastCommandUtc[raw.Author.Id] = now;
                    await HandleBuyAsync(raw, parts);
                    return true;

                case "sell":
                case "post":               // KMH: "post a listing" reads naturally for the sell flow
                    LastCommandUtc[raw.Author.Id] = now;
                    await HandleSellAsync(raw, parts);
                    return true;

                case "cancel":
                    LastCommandUtc[raw.Author.Id] = now;
                    await HandleCancelAsync(raw, parts);
                    return true;

                case "treasury":
                case "vault":
                case "wallet":
                    LastCommandUtc[raw.Author.Id] = now;
                    await HandleTreasuryAsync(raw);
                    return true;

                case "quests":
                case "questboard":
                    LastCommandUtc[raw.Author.Id] = now;
                    await HandleQuestsAsync(raw);
                    return true;

                case "leaderboard":
                case "lb":
                case "top":
                    LastCommandUtc[raw.Author.Id] = now;
                    await DiscordLeaderboardCommand.TryDispatchAsync(raw, parts);
                    return true;

                case "showcase":           // post/edit your sell showcase
                case "myshop":             // alias — "my shop"
                case "shopfront":          // alias — "set up my shopfront"
                    LastCommandUtc[raw.Author.Id] = now;
                    await HandleShowcaseAsync(raw, parts);
                    return true;

                case "history":            // last N treasury transactions
                case "txn":
                case "log":
                    LastCommandUtc[raw.Author.Id] = now;
                    await HandleHistoryAsync(raw, parts);
                    return true;

                case "compare":            // lowest 5 prices for an item
                case "price":
                case "prices":
                    LastCommandUtc[raw.Author.Id] = now;
                    await HandleCompareAsync(raw, parts);
                    return true;

                case "wtb":                // Want-To-Buy board (mirror of !showcase)
                case "want":
                case "wanted":
                    LastCommandUtc[raw.Author.Id] = now;
                    await HandleWtbAsync(raw, parts);
                    return true;

                default:
                    return false;
            }
        }

        // -- !showcase --

        /// <summary>
        /// Per-user marketplace showcase. Posts an embed of the
        /// player's current listings to the configured marketplace channel
        /// (text channel or forum). Subsequent calls EDIT the existing
        /// post so the channel doesn't fill up.
        ///
        /// Subcommands:
        ///   !showcase                      — post or refresh your showcase
        ///   !showcase update               — alias for the bare command
        ///   !showcase delete               — remove your showcase (and the
        ///                                    thread if it lives in a forum)
        ///   !showcase tagline &lt;text&gt;       — set the tagline shown on top
        ///   !showcase tagline clear        — remove the tagline
        /// </summary>
        private static async Task HandleShowcaseAsync(SocketMessage raw, string[] parts)
        {
            UserFile uf = await RequireLinkedAsync(raw);
            if (uf == null) return;

            string showcaseChannelStr = GameServer.Core.Master.ServerConfig?.DiscordMarketplaceForumChannelId;
            if (string.IsNullOrWhiteSpace(showcaseChannelStr) ||
                !ulong.TryParse(showcaseChannelStr.Trim(), out ulong showcaseChannelId) ||
                showcaseChannelId == 0)
            {
                await raw.Channel.SendMessageAsync(
                    "❌ Showcase channel isn't configured on this server. " +
                    "Ask an admin to set `DiscordMarketplaceForumChannelId` in `ServerConfig.json`.");
                return;
            }

            // Subcommand dispatch.
            string sub = parts.Length >= 2 ? parts[1].ToLowerInvariant() : string.Empty;

            if (sub == "delete" || sub == "remove" || sub == "clear")
            {
                await HandleShowcaseDeleteAsync(raw, uf);
                return;
            }
            if (sub == "tagline")
            {
                await HandleShowcaseTaglineAsync(raw, uf, parts);
                return;
            }
            // Anything else (including "update") falls through to the post-or-edit path.

            await PostOrEditShowcaseForUserAsync(raw, uf, showcaseChannelId);
        }

        private static async Task PostOrEditShowcaseForUserAsync(SocketMessage raw, UserFile uf, ulong showcaseChannelId)
        {
            // Gather the user's current listings.
            Shared.Files.Economy.MarketplaceFile snap = MarketplaceManager.Snapshot();
            List<Shared.Files.Economy.MarketplaceListing> mine = new List<Shared.Files.Economy.MarketplaceListing>();
            if (snap?.Listings != null)
            {
                foreach (var l in snap.Listings)
                {
                    if (l != null && string.Equals(l.SellerUsername, uf.Username, StringComparison.OrdinalIgnoreCase))
                        mine.Add(l);
                }
            }

            Embed embed = DiscordAnnouncer.BuildShowcaseEmbed(
                uf.Username, uf.DiscordUsername, uf.DiscordShowcaseTagline, mine);

            ulong prevChannel = ParseUlongOrZero(uf.DiscordShowcaseChannelId);
            ulong prevMessage = ParseUlongOrZero(uf.DiscordShowcaseMessageId);

            // Thread title for forum-mode posts.
            string threadTitle = $"{uf.Username}'s Marketplace";

            var result = await DiscordBridge.PostOrEditShowcaseAsync(
                showcaseChannelId, threadTitle, embed, prevChannel, prevMessage);

            if (!result.Success)
            {
                await raw.Channel.SendMessageAsync($"❌ Couldn't post showcase: {result.PermalinkOrError}");
                return;
            }

            // Persist IDs + refresh ticks — next !showcase edits same post, sweep keeps it alive.
            uf.DiscordShowcaseChannelId = result.ChannelId.ToString();
            uf.DiscordShowcaseMessageId = result.MessageId.ToString();
            uf.DiscordShowcaseLastUpdatedUtcTicks = DateTime.UtcNow.Ticks;
            uf.SaveUserFile();

            string link = string.IsNullOrEmpty(result.PermalinkOrError) ? "" : $"\n{result.PermalinkOrError}";
            await raw.Channel.SendMessageAsync(
                $"✅ Showcase updated ({mine.Count} listing{(mine.Count == 1 ? "" : "s")}).{link}");
        }

        private static async Task HandleShowcaseDeleteAsync(SocketMessage raw, UserFile uf)
        {
            ulong prevChannel = ParseUlongOrZero(uf.DiscordShowcaseChannelId);
            ulong prevMessage = ParseUlongOrZero(uf.DiscordShowcaseMessageId);

            if (prevChannel == 0 || prevMessage == 0)
            {
                await raw.Channel.SendMessageAsync("ℹ️ You don't have an active showcase to delete.");
                return;
            }

            bool ok = await DiscordBridge.DeleteShowcaseAsync(prevChannel, prevMessage);

            uf.DiscordShowcaseChannelId = null;
            uf.DiscordShowcaseMessageId = null;
            uf.SaveUserFile();

            await raw.Channel.SendMessageAsync(ok ? "✅ Showcase removed." : "ℹ️ Couldn't find the message to delete — state cleared.");
        }

        private static async Task HandleShowcaseTaglineAsync(SocketMessage raw, UserFile uf, string[] parts)
        {
            if (parts.Length < 3)
            {
                await raw.Channel.SendMessageAsync(
                    "Usage: `!showcase tagline <text>`  or  `!showcase tagline clear`\n" +
                    "The tagline appears at the top of your showcase embed (e.g. \"DM me on Discord to negotiate\").");
                return;
            }

            string sub = parts[2].ToLowerInvariant();
            if (sub == "clear" || sub == "none" || sub == "off")
            {
                uf.DiscordShowcaseTagline = null;
                uf.SaveUserFile();
                await raw.Channel.SendMessageAsync("✅ Tagline cleared. Run `!showcase` to refresh your post.");
                return;
            }

            // Rejoin everything after "tagline" into one phrase, length-capped.
            // Use the same quote-aware tokeniser as !sell so quoted multi-word
            // taglines work.
            string[] tokens = ReparseQuoted(raw.Content?.Substring(1) ?? string.Empty);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 2; i < tokens.Length; i++)
            {
                if (i > 2) sb.Append(' ');
                sb.Append(tokens[i]);
            }
            string text = sb.ToString().Trim();
            if (text.Length == 0)
            {
                await raw.Channel.SendMessageAsync("❌ Tagline can't be empty.");
                return;
            }
            // Discord embed field cap is generous, but keep this tight to
            // avoid burying the listings.
            if (text.Length > 200) text = text.Substring(0, 200);

            uf.DiscordShowcaseTagline = text;
            uf.SaveUserFile();
            await raw.Channel.SendMessageAsync("✅ Tagline updated. Run `!showcase` to refresh your post.");
        }

        private static ulong ParseUlongOrZero(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            return ulong.TryParse(s.Trim(), out ulong v) ? v : 0;
        }

        // -- !wtb (Want To Buy) --

        private const int MaxWtbEntriesPerUser = 25;

        // !wtb [add|remove|list|clear|delete|tagline] — per-user Want-To-Buy board.
        // Falls back to sells showcase channel if no WTB channel is configured.
        private static async Task HandleWtbAsync(SocketMessage raw, string[] parts)
        {
            UserFile uf = await RequireLinkedAsync(raw);
            if (uf == null) return;

            if (uf.WantToBuyEntries == null) uf.WantToBuyEntries = new List<TCPNetwork.Files.Client.WantToBuyEntry>();

            string sub = parts.Length >= 2 ? parts[1].ToLowerInvariant() : string.Empty;

            switch (sub)
            {
                case "add": await HandleWtbAddAsync(raw, uf); return;
                case "remove":
                case "rm":
                case "del":
                    await HandleWtbRemoveAsync(raw, uf); return;
                case "list":
                case "show":
                case "mine":
                    await HandleWtbListAsync(raw, uf); return;
                case "clear":
                case "wipe":
                    await HandleWtbClearAsync(raw, uf); return;
                case "delete":
                    await HandleWtbDeleteAsync(raw, uf); return;
                case "tagline":
                    await HandleWtbTaglineAsync(raw, uf, parts); return;
                default:
                    // No subcommand → post/refresh the embed.
                    await PostOrEditWtbForUserAsync(raw, uf); return;
            }
        }

        private static async Task HandleWtbAddAsync(SocketMessage raw, UserFile uf)
        {
            // Expected: !wtb add <item-name...> <qty> <max-unit-price>
            string[] tokens = ReparseQuoted(raw.Content?.Substring(1) ?? string.Empty);
            // tokens[0]="wtb", tokens[1]="add", tokens[2..n-2]=item name, tokens[n-2]=qty, tokens[n-1]=price
            if (tokens.Length < 5
                || !int.TryParse(tokens[tokens.Length - 2], out int qty) || qty <= 0
                || !int.TryParse(tokens[tokens.Length - 1], out int unitPrice) || unitPrice <= 0)
            {
                await raw.Channel.SendMessageAsync(
                    "Usage: `!wtb add <item> <max-qty> <max-unit-price>`\n" +
                    "Examples:\n" +
                    "• `!wtb add plasteel 100 10`\n" +
                    "• `!wtb add \"power armor\" 1 800`");
                return;
            }
            if (qty > 10_000) qty = 10_000; // mirror MaxBuyQty for sanity.

            System.Text.StringBuilder nameSb = new System.Text.StringBuilder();
            for (int i = 2; i < tokens.Length - 2; i++)
            {
                if (i > 2) nameSb.Append(' ');
                nameSb.Append(tokens[i]);
            }
            string itemRaw = nameSb.ToString().Replace('_', ' ').Trim();

            string defName = ItemLabelCache.ResolveDefNameByQuery(itemRaw, out List<string> candidates);
            if (defName == null)
            {
                if (candidates != null && candidates.Count > 1)
                {
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    sb.Append("⚠️ `").Append(itemRaw).Append("` matches multiple items — be more specific:\n");
                    foreach (string c in candidates)
                        sb.Append("• **").Append(ItemLabelCache.LabelFor(c)).Append("** _(").Append(c).Append(")_\n");
                    await raw.Channel.SendMessageAsync(sb.ToString());
                    return;
                }
                // Cap synthetic defName — bounds UserFile.json growth per !wtb add.
                string raw1 = itemRaw.Replace(' ', '_');
                defName = raw1.Length > 64 ? raw1.Substring(0, 64) : raw1;
            }

            if (uf.WantToBuyEntries.Count >= MaxWtbEntriesPerUser)
            {
                await raw.Channel.SendMessageAsync(
                    $"❌ You already have the max **{MaxWtbEntriesPerUser}** WTB entries. Use `!wtb remove <item>` first.");
                return;
            }

            // De-dupe: if an entry for this defName already exists, update it.
            TCPNetwork.Files.Client.WantToBuyEntry existing = null;
            foreach (var e in uf.WantToBuyEntries)
            {
                if (e != null && string.Equals(e.ItemDefName, defName, StringComparison.OrdinalIgnoreCase))
                { existing = e; break; }
            }
            if (existing != null)
            {
                existing.MaxQty = qty;
                existing.MaxUnitPriceSilver = unitPrice;
                existing.AddedUtcTicks = DateTime.UtcNow.Ticks;
            }
            else
            {
                uf.WantToBuyEntries.Add(new TCPNetwork.Files.Client.WantToBuyEntry
                {
                    ItemDefName = defName,
                    MaxQty = qty,
                    MaxUnitPriceSilver = unitPrice,
                    AddedUtcTicks = DateTime.UtcNow.Ticks
                });
            }
            uf.SaveUserFile();

            string label = ItemLabelCache.LabelFor(defName);
            string verb = existing != null ? "Updated" : "Added";
            await raw.Channel.SendMessageAsync(
                $"✅ {verb} WTB: **{qty}× {label}** @ `≤{unitPrice}s`/ea. " +
                $"Run `!wtb` to refresh your published board.");
        }

        private static async Task HandleWtbRemoveAsync(SocketMessage raw, UserFile uf)
        {
            string[] tokens = ReparseQuoted(raw.Content?.Substring(1) ?? string.Empty);
            if (tokens.Length < 3)
            {
                await raw.Channel.SendMessageAsync("Usage: `!wtb remove <item>`");
                return;
            }
            System.Text.StringBuilder nameSb = new System.Text.StringBuilder();
            for (int i = 2; i < tokens.Length; i++)
            {
                if (i > 2) nameSb.Append(' ');
                nameSb.Append(tokens[i]);
            }
            string itemRaw = nameSb.ToString().Replace('_', ' ').Trim();
            string defName = ItemLabelCache.ResolveDefNameByQuery(itemRaw, out _);
            if (defName == null)
            {
                string raw3 = itemRaw.Replace(' ', '_');
                defName = raw3.Length > 64 ? raw3.Substring(0, 64) : raw3;
            }

            int removed = uf.WantToBuyEntries.RemoveAll(e =>
                e != null && string.Equals(e.ItemDefName, defName, StringComparison.OrdinalIgnoreCase));
            uf.SaveUserFile();

            if (removed == 0)
                await raw.Channel.SendMessageAsync($"ℹ️ No WTB for **{ItemLabelCache.LabelFor(defName)}** in your list.");
            else
                await raw.Channel.SendMessageAsync($"✅ Removed WTB for **{ItemLabelCache.LabelFor(defName)}**. Run `!wtb` to refresh your board.");
        }

        private static async Task HandleWtbListAsync(SocketMessage raw, UserFile uf)
        {
            if (uf.WantToBuyEntries.Count == 0)
            {
                await raw.Channel.SendMessageAsync("Your WTB list is empty. Use `!wtb add <item> <qty> <max-price>` to start.");
                return;
            }
            var eb = new EmbedBuilder()
                .WithTitle($"🛍️ {uf.Username}'s WTB List")
                .WithColor(new Color(186, 132, 246))
                .WithFooter($"{uf.WantToBuyEntries.Count} entries · {DiscordBridge.ServerTag}");
            foreach (var e in uf.WantToBuyEntries)
            {
                eb.AddField(
                    ItemLabelCache.LabelFor(e.ItemDefName),
                    $"Up to **{e.MaxQty}**× @ `≤{e.MaxUnitPriceSilver}s`/ea",
                    inline: true);
            }
            await raw.Channel.SendMessageAsync(embed: eb.Build());
        }

        private static async Task HandleWtbClearAsync(SocketMessage raw, UserFile uf)
        {
            int n = uf.WantToBuyEntries.Count;
            uf.WantToBuyEntries.Clear();
            uf.SaveUserFile();
            await raw.Channel.SendMessageAsync($"✅ Cleared {n} WTB entr{(n == 1 ? "y" : "ies")}. Run `!wtb` to refresh your board.");
        }

        private static async Task HandleWtbDeleteAsync(SocketMessage raw, UserFile uf)
        {
            ulong prevChannel = ParseUlongOrZero(uf.DiscordWtbChannelId);
            ulong prevMessage = ParseUlongOrZero(uf.DiscordWtbMessageId);
            if (prevChannel == 0 || prevMessage == 0)
            {
                await raw.Channel.SendMessageAsync("ℹ️ You don't have an active WTB board to delete.");
                return;
            }
            bool ok = await DiscordBridge.DeleteShowcaseAsync(prevChannel, prevMessage);
            uf.DiscordWtbChannelId = null;
            uf.DiscordWtbMessageId = null;
            uf.SaveUserFile();
            await raw.Channel.SendMessageAsync(ok ? "✅ WTB board removed." : "ℹ️ Couldn't find the message — state cleared.");
        }

        private static async Task HandleWtbTaglineAsync(SocketMessage raw, UserFile uf, string[] parts)
        {
            if (parts.Length < 3)
            {
                await raw.Channel.SendMessageAsync(
                    "Usage: `!wtb tagline <text>`  or  `!wtb tagline clear`");
                return;
            }
            string sub = parts[2].ToLowerInvariant();
            if (sub == "clear" || sub == "none" || sub == "off")
            {
                uf.DiscordWtbTagline = null;
                uf.SaveUserFile();
                await raw.Channel.SendMessageAsync("✅ WTB tagline cleared.");
                return;
            }
            string[] tokens = ReparseQuoted(raw.Content?.Substring(1) ?? string.Empty);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 2; i < tokens.Length; i++)
            {
                if (i > 2) sb.Append(' ');
                sb.Append(tokens[i]);
            }
            string text = sb.ToString().Trim();
            if (text.Length == 0) { await raw.Channel.SendMessageAsync("❌ Tagline can't be empty."); return; }
            if (text.Length > 200) text = text.Substring(0, 200);

            uf.DiscordWtbTagline = text;
            uf.SaveUserFile();
            await raw.Channel.SendMessageAsync("✅ WTB tagline updated.");
        }

        private static async Task PostOrEditWtbForUserAsync(SocketMessage raw, UserFile uf)
        {
            // Channel resolution: dedicated WTB channel if configured, else
            // fall back to the sells showcase channel.
            string wtbChannelStr = GameServer.Core.Master.ServerConfig?.DiscordWtbForumChannelId;
            if (string.IsNullOrWhiteSpace(wtbChannelStr))
                wtbChannelStr = GameServer.Core.Master.ServerConfig?.DiscordMarketplaceForumChannelId;

            if (string.IsNullOrWhiteSpace(wtbChannelStr) ||
                !ulong.TryParse(wtbChannelStr.Trim(), out ulong wtbChannelId) ||
                wtbChannelId == 0)
            {
                await raw.Channel.SendMessageAsync(
                    "❌ No WTB channel is configured. Ask an admin to set " +
                    "`DiscordWtbForumChannelId` (or `DiscordMarketplaceForumChannelId`) in `ServerConfig.json`.");
                return;
            }

            Embed embed = DiscordAnnouncer.BuildWtbEmbed(
                uf.Username, uf.DiscordUsername, uf.DiscordWtbTagline, uf.WantToBuyEntries);

            ulong prevChannel = ParseUlongOrZero(uf.DiscordWtbChannelId);
            ulong prevMessage = ParseUlongOrZero(uf.DiscordWtbMessageId);
            string threadTitle = $"{uf.Username} is Buying";

            var result = await DiscordBridge.PostOrEditShowcaseAsync(
                wtbChannelId, threadTitle, embed, prevChannel, prevMessage);

            if (!result.Success)
            {
                await raw.Channel.SendMessageAsync($"❌ Couldn't post WTB board: {result.PermalinkOrError}");
                return;
            }

            uf.DiscordWtbChannelId = result.ChannelId.ToString();
            uf.DiscordWtbMessageId = result.MessageId.ToString();
            uf.DiscordWtbLastUpdatedUtcTicks = DateTime.UtcNow.Ticks;
            uf.SaveUserFile();

            string link = string.IsNullOrEmpty(result.PermalinkOrError) ? "" : $"\n{result.PermalinkOrError}";
            int n = uf.WantToBuyEntries?.Count ?? 0;
            await raw.Channel.SendMessageAsync(
                $"✅ WTB board updated ({n} entr{(n == 1 ? "y" : "ies")}).{link}");
        }

        // -- Discord buttons --

        // "Buy 1 / Buy 10 / Buy all" buttons. customId = "buy:<id>:<qty|all>".
        public static MessageComponent BuildBuyButtonsForListing(MarketplaceListing l)
        {
            if (l == null || l.RemainingQty <= 0) return null;
            var builder = new ComponentBuilder();
            // "Buy 1"
            builder.WithButton(label: "Buy 1",
                customId: $"buy:{l.Id}:1",
                style: ButtonStyle.Success,
                emote: new Emoji("🛒"));
            // "Buy 10" — only show if there are at least 10 left.
            if (l.RemainingQty >= 10)
                builder.WithButton(label: "Buy 10", customId: $"buy:{l.Id}:10", style: ButtonStyle.Primary);
            // "Buy all" — buys min(remaining, MaxBuyQty 10k).
            builder.WithButton(label: $"Buy all ({l.RemainingQty})",
                customId: $"buy:{l.Id}:all",
                style: ButtonStyle.Secondary);
            return builder.Build();
        }

        // 2s debounce per user+listing — Buy is stock-atomic, but parallel clicks
        // would still produce duplicate toasts + refunds. 2s leaves room for real burst-buys.
        private const int ButtonDebounceWindowMs = 2000;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> ButtonDebounce =
            new System.Collections.Concurrent.ConcurrentDictionary<string, long>();

        // [Buy] button handler. Ephemeral reply so channel doesn't get one receipt per click.
        public static async Task HandleBuyButtonAsync(SocketMessageComponent component, string customId)
        {
            // customId format: "buy:<listingId>:<qty-or-all>"
            string[] segs = customId.Split(':');
            if (segs.Length != 3 || !long.TryParse(segs[1], out long listingId))
            {
                await component.RespondAsync("❌ Malformed buy button.", ephemeral: true);
                return;
            }

            // -- Debounce rapid double-clicks. --
            ulong clickerId = component.User?.Id ?? 0;
            if (clickerId != 0)
            {
                string debounceKey = $"{clickerId}:{listingId}";
                long nowTicks = DateTime.UtcNow.Ticks;
                long sinceMs = ButtonDebounce.TryGetValue(debounceKey, out long lastTicks)
                    ? (nowTicks - lastTicks) / TimeSpan.TicksPerMillisecond
                    : long.MaxValue;
                if (sinceMs < ButtonDebounceWindowMs)
                {
                    await component.RespondAsync(
                        "⏳ Still processing your previous click — give it a moment.", ephemeral: true);
                    return;
                }
                ButtonDebounce[debounceKey] = nowTicks;

                // Best-effort cleanup so the dictionary doesn't grow without
                // bound. Cheap O(1) probe of one stale entry per call.
                if (ButtonDebounce.Count > 256)
                {
                    foreach (var kv in ButtonDebounce)
                    {
                        if ((nowTicks - kv.Value) / TimeSpan.TicksPerMillisecond > ButtonDebounceWindowMs * 5)
                            ButtonDebounce.TryRemove(kv.Key, out _);
                        break;
                    }
                }
            }

            // Resolve the linked user. Buttons require linking same as `!buy`.
            UserFile uf = null;
            string discordId = component.User?.Id.ToString();
            if (!string.IsNullOrEmpty(discordId))
            {
                foreach (UserFile candidate in UserManagerH.GetAllUserFiles())
                {
                    if (candidate != null && string.Equals(candidate.DiscordId, discordId, StringComparison.Ordinal))
                    { uf = candidate; break; }
                }
            }
            if (uf == null)
            {
                await component.RespondAsync(
                    "❌ Your Discord isn't linked to an in-game account.\n" +
                    "In-game, run `/link` to get a token, then `!link <token>` here.",
                    ephemeral: true);
                return;
            }

            // Find the listing for qty resolution + receipt.
            MarketplaceListing listing = MarketplaceManager.Snapshot().Listings
                .FirstOrDefault(l => l.Id == listingId);
            if (listing == null)
            {
                await component.RespondAsync($"❌ Listing #{listingId} no longer exists.", ephemeral: true);
                return;
            }
            if (string.Equals(listing.SellerUsername, uf.Username, StringComparison.OrdinalIgnoreCase))
            {
                await component.RespondAsync("❌ You can't buy your own listing.", ephemeral: true);
                return;
            }

            int qty;
            if (segs[2] == "all") qty = Math.Min(listing.RemainingQty, 10_000);
            else if (int.TryParse(segs[2], out int parsed) && parsed > 0) qty = Math.Min(parsed, 10_000);
            else { await component.RespondAsync("❌ Malformed quantity.", ephemeral: true); return; }

            // Defer immediately so Discord knows we're working (button
            // interactions must respond within 3 seconds).
            await component.DeferAsync(ephemeral: true);

            int boughtUnits = Math.Min(qty, listing.RemainingQty);
            long totalCostLong = (long)listing.UnitPriceSilver * boughtUnits;
            if (totalCostLong > int.MaxValue || totalCostLong < 0)
            {
                await component.FollowupAsync("❌ Purchase value too large. Try a smaller quantity.", ephemeral: true);
                return;
            }
            int totalCost = (int)totalCostLong;

            string treasuryKey = TreasuryManager.ResolveKeyForUsername(uf.Username);
            if (string.IsNullOrEmpty(treasuryKey))
            {
                await component.FollowupAsync("❌ Could not resolve your treasury.", ephemeral: true);
                return;
            }
            bool isGuild = !treasuryKey.StartsWith("_personal:", StringComparison.OrdinalIgnoreCase);
            TreasuryFile t = TreasuryManager.GetOrCreate(treasuryKey, isGuild);
            if (t.SilverBalance < totalCost)
            {
                await component.FollowupAsync(
                    $"❌ Treasury has `{t.SilverBalance}s`, need `{totalCost}s`.", ephemeral: true);
                return;
            }

            if (!TreasuryManager.TryWithdrawSilver(t, uf.Username, totalCost,
                    TreasuryTransaction.TxKind.Withdraw, $"market-buy-btn:{listingId}"))
            {
                await component.FollowupAsync("❌ Failed to reserve silver from your treasury.", ephemeral: true);
                return;
            }

            var (ok, note, units, costPaid, defName) = MarketplaceManager.Buy(uf.Username, listingId, qty);
            if (!ok)
            {
                TreasuryManager.DepositSilverForUser(uf.Username, totalCost,
                    TreasuryTransaction.TxKind.MarketplaceRefund, $"market-buy-btn-failed:{listingId}");
                await component.FollowupAsync($"❌ {note}\nSilver refunded.", ephemeral: true);
                return;
            }

            TreasuryManager.DepositItemForUser(uf.Username, defName, units,
                TreasuryTransaction.TxKind.Deposit, $"market-buy-btn:{listingId}");

            if (costPaid < totalCost)
            {
                int refund = totalCost - costPaid;
                TreasuryManager.DepositSilverForUser(uf.Username, refund,
                    TreasuryTransaction.TxKind.MarketplaceRefund, $"market-buy-btn-partial:{listingId}");
            }

            string boughtLabel = ItemLabelCache.LabelFor(defName);
            await component.FollowupAsync(
                $"✅ Bought **{units}× {boughtLabel}** for `{costPaid}s`. Items in your treasury.",
                ephemeral: true);
        }

        // -- !history --

        // Last N treasury transactions. Default 10, capped at 25 by embed limits.
        private static async Task HandleHistoryAsync(SocketMessage raw, string[] parts)
        {
            UserFile uf = await RequireLinkedAsync(raw);
            if (uf == null) return;

            int count = 10;
            if (parts.Length >= 2 && int.TryParse(parts[1], out int n) && n > 0)
                count = Math.Min(25, n);

            string treasuryKey = TreasuryManager.ResolveKeyForUsername(uf.Username);
            if (string.IsNullOrEmpty(treasuryKey))
            {
                await raw.Channel.SendMessageAsync("❌ Could not resolve your treasury.");
                return;
            }
            bool isGuild = !treasuryKey.StartsWith("_personal:", StringComparison.OrdinalIgnoreCase);
            TreasuryFile t = TreasuryManager.GetOrCreate(treasuryKey, isGuild);

            EmbedBuilder eb = new EmbedBuilder()
                .WithTitle($"📜 Recent Transactions — {uf.Username}")
                .WithColor(new Color(186, 132, 246))
                .WithFooter($"{DiscordBridge.ServerTag} · treasury: {(isGuild ? treasuryKey : "personal vault")}");

            if (t.RecentTransactions == null || t.RecentTransactions.Count == 0)
            {
                eb.WithDescription("_No transactions yet._");
                await raw.Channel.SendMessageAsync(embed: eb.Build());
                return;
            }

            // Show most recent first. RecentTransactions appends, so reverse.
            int total = t.RecentTransactions.Count;
            int take = Math.Min(count, total);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = total - 1; i >= total - take; i--)
            {
                var tx = t.RecentTransactions[i];
                DateTimeOffset when = DateTimeOffset.FromUnixTimeSeconds(
                    new DateTimeOffset(new DateTime(tx.UtcTicks, DateTimeKind.Utc)).ToUnixTimeSeconds());

                string what = string.IsNullOrEmpty(tx.ItemDefName)
                    ? $"`{tx.Amount}s`"
                    : $"`{tx.Amount}× {ItemLabelCache.LabelFor(tx.ItemDefName)}`";

                string kind = tx.Kind.ToString();
                string by = string.IsNullOrEmpty(tx.Username) ? "—" : tx.Username;

                sb.Append($"<t:{when.ToUnixTimeSeconds()}:R> · **{kind}** · {what}");
                if (!string.Equals(by, uf.Username, StringComparison.OrdinalIgnoreCase))
                    sb.Append($" · _by {by}_");
                if (!string.IsNullOrEmpty(tx.Note))
                    sb.Append($" · `{tx.Note}`");
                sb.AppendLine();
            }
            eb.WithDescription(sb.ToString());
            if (total > take)
                eb.WithFooter(eb.Footer.Text + $" · {total - take} older entries truncated");

            await raw.Channel.SendMessageAsync(embed: eb.Build());
        }

        // -- !compare --

        // 5 cheapest active listings for a fuzzy item name (same resolution as !sell).
        private static async Task HandleCompareAsync(SocketMessage raw, string[] parts)
        {
            if (parts.Length < 2)
            {
                await raw.Channel.SendMessageAsync(
                    "Usage: `!compare <item-name>` — shows the 5 cheapest active listings.\n" +
                    "Examples: `!compare plasteel`  ·  `!compare \"power armor\"`  ·  `!compare smokeleaf`");
                return;
            }

            string[] tokens = ReparseQuoted(raw.Content?.Substring(1) ?? string.Empty);
            System.Text.StringBuilder nameSb = new System.Text.StringBuilder();
            for (int i = 1; i < tokens.Length; i++)
            {
                if (i > 1) nameSb.Append(' ');
                nameSb.Append(tokens[i]);
            }
            string query = nameSb.ToString().Replace('_', ' ').Trim();
            if (string.IsNullOrEmpty(query)) return;

            string defName = ItemLabelCache.ResolveDefNameByQuery(query, out List<string> candidates);
            if (defName == null)
            {
                if (candidates != null && candidates.Count > 1)
                {
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    sb.Append("⚠️ `").Append(query).Append("` matches multiple items — be more specific:\n");
                    foreach (string c in candidates)
                        sb.Append("• **").Append(ItemLabelCache.LabelFor(c)).Append("** _(").Append(c).Append(")_\n");
                    await raw.Channel.SendMessageAsync(sb.ToString());
                    return;
                }
                // Fall through with raw query as defName (unloaded mods etc.)
                defName = query.Replace(' ', '_');
            }

            string label = ItemLabelCache.LabelFor(defName);

            MarketplaceFile snap = MarketplaceManager.Snapshot();
            List<MarketplaceListing> matches = new List<MarketplaceListing>();
            if (snap?.Listings != null)
            {
                foreach (var l in snap.Listings)
                {
                    if (l == null) continue;
                    if (string.Equals(l.ItemDefName, defName, StringComparison.OrdinalIgnoreCase))
                        matches.Add(l);
                }
            }

            EmbedBuilder eb = new EmbedBuilder()
                .WithTitle($"📊 Price Compare — {label}")
                .WithColor(new Color(255, 196, 97))
                .WithFooter($"{DiscordBridge.ServerTag}");

            if (matches.Count == 0)
            {
                eb.WithDescription($"_No active listings for **{label}**._");
                await raw.Channel.SendMessageAsync(embed: eb.Build());
                return;
            }

            matches.Sort((a, b) => a.UnitPriceSilver.CompareTo(b.UnitPriceSilver));
            int show = Math.Min(5, matches.Count);

            int minP = matches[0].UnitPriceSilver;
            int maxP = matches[matches.Count - 1].UnitPriceSilver;
            long sum = 0;
            foreach (var l in matches) sum += l.UnitPriceSilver;
            int avg = (int)(sum / matches.Count);

            eb.WithDescription(
                $"**{matches.Count}** active listing(s) · cheapest **{minP}s** · highest **{maxP}s** · avg **{avg}s**");

            for (int i = 0; i < show; i++)
            {
                var l = matches[i];
                eb.AddField(
                    $"#{l.Id} · `{l.UnitPriceSilver}s`/ea",
                    $"**{l.RemainingQty}**× · total `{l.UnitPriceSilver * l.RemainingQty}s` · by **{l.SellerUsername}** · `!buy {l.Id}`",
                    inline: false);
            }

            // Buttons only on the cheapest result — Discord's 25-button cap + ambiguous
            // attribution rules out per-listing rows. `!buy <id>` still covers the rest.
            MessageComponent components = matches.Count > 0
                ? BuildBuyButtonsForListing(matches[0])
                : null;

            await raw.Channel.SendMessageAsync(embed: eb.Build(), components: components);
        }

        // -- helpers --

        /// <summary>Resolve the linked in-game user from a Discord author, or null.</summary>
        private static UserFile ResolveLinkedUser(SocketMessage raw)
        {
            string id = raw.Author?.Id.ToString();
            if (string.IsNullOrEmpty(id)) return null;
            foreach (UserFile uf in UserManagerH.GetAllUserFiles())
            {
                if (string.Equals(uf?.DiscordId, id, StringComparison.Ordinal))
                    return uf;
            }
            return null;
        }

        private static async Task<UserFile> RequireLinkedAsync(SocketMessage raw)
        {
            UserFile uf = ResolveLinkedUser(raw);
            if (uf == null)
            {
                await raw.Channel.SendMessageAsync(
                    "❌ Your Discord isn't linked to an in-game account yet.\n" +
                    "In-game, run `/link` to get a token, then post `!link <token>` here.");
                return null;
            }
            return uf;
        }

        // -- !market --

        private static async Task HandleMarketAsync(SocketMessage raw, string[] parts)
        {
            MarketplaceFile snap = MarketplaceManager.Snapshot();
            List<MarketplaceListing> all = snap.Listings ?? new List<MarketplaceListing>();
            int totalListings = all.Count;

            string filterUser = null;
            int page = 1;
            bool myOnly = false;

            if (parts.Length >= 2)
            {
                if (parts[1].Equals("mine", StringComparison.OrdinalIgnoreCase))
                {
                    myOnly = true;
                    UserFile uf = ResolveLinkedUser(raw);
                    if (uf == null)
                    {
                        await raw.Channel.SendMessageAsync("❌ Link your account first to filter by your listings.");
                        return;
                    }
                    filterUser = uf.Username;
                }
                else if (int.TryParse(parts[1], out int p) && p > 0)
                {
                    page = p;
                }
            }

            IEnumerable<MarketplaceListing> visible = all.OrderBy(l => l.ItemDefName).ThenBy(l => l.UnitPriceSilver);
            if (filterUser != null)
                visible = visible.Where(l => string.Equals(l.SellerUsername, filterUser, StringComparison.OrdinalIgnoreCase));
            List<MarketplaceListing> rows = visible.ToList();

            int totalPages = Math.Max(1, (rows.Count + PageSize - 1) / PageSize);
            if (page > totalPages) page = totalPages;
            int skip = (page - 1) * PageSize;

            EmbedBuilder eb = new EmbedBuilder()
                .WithTitle(myOnly ? "🛒 Your Listings" : "🛒 Marketplace")
                .WithColor(new Color(255, 196, 97));

            string footer = totalPages > 1
                ? $"Page {page}/{totalPages}  ·  use `!market {Math.Min(totalPages, page + 1)}` for next  ·  {DiscordBridge.ServerTag}"
                : $"{DiscordBridge.ServerTag}";
            if (rows.Count == 0)
            {
                eb.WithDescription(myOnly ? "_You have no open listings._" : "_No open listings on the server right now._");
            }
            else
            {
                int shown = 0;
                foreach (MarketplaceListing l in rows.Skip(skip).Take(PageSize))
                {
                    string emoji = DiscordItemIconMap.EmojiFor(l.ItemDefName);
                    string label = ItemLabelCache.LabelFor(l.ItemDefName);
                    string time = FormatExpiry(l.ExpiresUtcTicks);
                    eb.AddField(
                        $"{emoji} #{l.Id} · {label}",
                        $"**{l.RemainingQty}**× @ `{l.UnitPriceSilver}s`/ea · total `{l.UnitPriceSilver * l.RemainingQty}s`\n" +
                        $"by **{l.SellerUsername}** · expires {time}",
                        inline: false);
                    shown++;
                }
                eb.WithDescription($"Showing {shown} of {rows.Count} listings.\n_Tip: `!buy <id> [qty]` to purchase, `!find <name>` to search._");
            }

            eb.WithFooter(footer);
            await raw.Channel.SendMessageAsync(embed: eb.Build());
        }

        // -- !buy --

        private static async Task HandleBuyAsync(SocketMessage raw, string[] parts)
        {
            UserFile uf = await RequireLinkedAsync(raw);
            if (uf == null) return;

            if (parts.Length < 2 || !long.TryParse(parts[1], out long listingId))
            {
                await raw.Channel.SendMessageAsync("Usage: `!buy <listing-id> [qty]`");
                return;
            }
            int qty = 1;
            if (parts.Length >= 3 && int.TryParse(parts[2], out int q) && q > 0) qty = q;
            // Same hard cap as the in-game Buy path. Without this,
            // `unitPrice × qty` (both ints) could overflow into negative —
            // exploitable for free silver / unintended treasury debits.
            // Anything legitimate is well under 10k units.
            const int MaxBuyQty = 10_000;
            if (qty > MaxBuyQty) qty = MaxBuyQty;

            // Find the listing for the receipt embed.
            MarketplaceListing listing = MarketplaceManager.Snapshot().Listings.FirstOrDefault(l => l.Id == listingId);
            if (listing == null)
            {
                await raw.Channel.SendMessageAsync($"❌ Listing #{listingId} not found.");
                return;
            }
            if (string.Equals(listing.SellerUsername, uf.Username, StringComparison.OrdinalIgnoreCase))
            {
                await raw.Channel.SendMessageAsync("❌ You can't buy your own listing.");
                return;
            }

            int boughtUnits = Math.Min(qty, listing.RemainingQty);
            // Compute in long to detect overflow safely, bail out
            // before any treasury mutation if the result wouldn't fit in int.
            long totalCostLong = (long)listing.UnitPriceSilver * boughtUnits;
            if (totalCostLong > int.MaxValue || totalCostLong < 0)
            {
                await raw.Channel.SendMessageAsync(
                    $"❌ Purchase value too large — try a smaller quantity (`!buy {listingId} <qty>`).");
                return;
            }
            int totalCost = (int)totalCostLong;

            // Buyer's silver is taken from their treasury (Discord buys are
            // always treasury-funded — they're not in the game world to spend caravan silver).
            string treasuryKey = TreasuryManager.ResolveKeyForUsername(uf.Username);
            if (string.IsNullOrEmpty(treasuryKey))
            {
                await raw.Channel.SendMessageAsync("❌ Could not resolve your treasury.");
                return;
            }
            bool isGuild = !treasuryKey.StartsWith("_personal:", StringComparison.OrdinalIgnoreCase);
            TreasuryFile t = TreasuryManager.GetOrCreate(treasuryKey, isGuild);

            if (t.SilverBalance < totalCost)
            {
                await raw.Channel.SendMessageAsync(
                    $"❌ Treasury has only `{t.SilverBalance}s`, need `{totalCost}s`.");
                return;
            }

            // Withdraw silver from buyer's treasury BEFORE the buy so we don't double-charge if Buy fails.
            if (!TreasuryManager.TryWithdrawSilver(t, uf.Username, totalCost,
                    TreasuryTransaction.TxKind.Withdraw, $"market-buy:{listingId}"))
            {
                await raw.Channel.SendMessageAsync("❌ Failed to reserve silver from your treasury.");
                return;
            }

            // Run the marketplace buy. Items are deposited into buyer's treasury automatically
            // because we're not on a caravan.
            var (ok, note, units, costPaid, defName) = MarketplaceManager.Buy(uf.Username, listingId, qty);
            if (!ok)
            {
                // Refund silver — the buy failed (race / sold out / etc).
                TreasuryManager.DepositSilverForUser(uf.Username, totalCost,
                    TreasuryTransaction.TxKind.MarketplaceRefund, $"market-buy-failed:{listingId}");
                await raw.Channel.SendMessageAsync($"❌ {note}\nSilver refunded to your treasury.");
                return;
            }

            // Buy passed. The seller's net silver was deposited inside Buy; we need to deposit the items into the buyer.
            TreasuryManager.DepositItemForUser(uf.Username, defName, units,
                TreasuryTransaction.TxKind.Deposit, $"market-buy:{listingId}");

            // If the actual cost differed (race made fewer units available), refund the difference.
            if (costPaid < totalCost)
            {
                int refund = totalCost - costPaid;
                TreasuryManager.DepositSilverForUser(uf.Username, refund,
                    TreasuryTransaction.TxKind.MarketplaceRefund, $"market-buy-partial:{listingId}");
            }

            string boughtLabel = ItemLabelCache.LabelFor(defName);
            EmbedBuilder eb = new EmbedBuilder()
                .WithTitle($"✅ Purchase Complete")
                .WithDescription($"You bought **{units}× {boughtLabel}** for `{costPaid}s`.")
                .AddField("Items delivered to", "Your treasury", true)
                .AddField("Treasury silver after", $"{t.SilverBalance}s (+ items)", true)
                .WithColor(new Color(140, 220, 140))
                .WithFooter($"Listing #{listingId} · {DiscordBridge.ServerTag}");

            string thumb = DiscordItemIconMap.TryGetIconUrl(defName);
            if (!string.IsNullOrEmpty(thumb)) eb.WithThumbnailUrl(thumb);

            await raw.Channel.SendMessageAsync(embed: eb.Build());
        }

        // -- !sell --

        /// <summary>
        /// Friendly !sell. The "item" argument may be:
        ///   • A defName  (exact, e.g. "Plasteel")
        ///   • A label    (case-insensitive, e.g. "plasteel", "knife", "smokeleaf")
        ///   • A multi-word label  (use quotes: <c>!sell "melee weapon" 1 500</c>
        ///                           OR underscore form: <c>!sell melee_weapon 1 500</c>).
        ///
        /// On ambiguity the user gets a list of candidate items so they
        /// can re-issue the command. On unknown item the player can
        /// fall back to the raw defName (works as before).
        /// </summary>
        private static async Task HandleSellAsync(SocketMessage raw, string[] parts)
        {
            UserFile uf = await RequireLinkedAsync(raw);
            if (uf == null) return;

            // rejoin "quoted" multi-word arguments before parsing.
            string[] tokens = ReparseQuoted(raw.Content?.Substring(1) ?? string.Empty);
            if (tokens.Length < 4)
            {
                await SendSellUsageAsync(raw);
                return;
            }

            // Last two tokens must be qty + price; everything between argv[1]
            // and them is the (possibly multi-word) item name.
            if (!int.TryParse(tokens[tokens.Length - 2], out int qty) || qty <= 0
                || !int.TryParse(tokens[tokens.Length - 1], out int unitPrice) || unitPrice <= 0)
            {
                await SendSellUsageAsync(raw);
                return;
            }

            System.Text.StringBuilder nameSb = new System.Text.StringBuilder();
            for (int i = 1; i < tokens.Length - 2; i++)
            {
                if (i > 1) nameSb.Append(' ');
                nameSb.Append(tokens[i]);
            }
            string itemRaw = nameSb.ToString().Replace('_', ' ').Trim();

            // Resolve the user-facing string to a defName via the cache.
            string defName = ItemLabelCache.ResolveDefNameByQuery(itemRaw, out List<string> candidates);
            if (defName == null)
            {
                if (candidates != null && candidates.Count > 1)
                {
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    sb.Append("⚠️ `").Append(itemRaw).Append("` matches multiple items — be more specific:\n");
                    foreach (string cand in candidates)
                        sb.Append("• **").Append(ItemLabelCache.LabelFor(cand)).Append("** _(").Append(cand).Append(")_\n");
                    await raw.Channel.SendMessageAsync(sb.ToString());
                    return;
                }

                // Last resort: assume the raw input is already a defName.
                // The treasury withdraw will fail cleanly if it isn't.
                string raw2 = itemRaw.Replace(' ', '_');
                defName = raw2.Length > 64 ? raw2.Substring(0, 64) : raw2;
            }

            string label = ItemLabelCache.LabelFor(defName);

            // Pull from seller's treasury (write permission required).
            string treasuryKey = TreasuryManager.ResolveKeyForUsername(uf.Username);
            if (string.IsNullOrEmpty(treasuryKey))
            {
                await raw.Channel.SendMessageAsync("❌ Could not resolve your treasury.");
                return;
            }
            bool isGuild = !treasuryKey.StartsWith("_personal:", StringComparison.OrdinalIgnoreCase);
            TreasuryFile t = TreasuryManager.GetOrCreate(treasuryKey, isGuild);

            if (!TreasuryManager.UsernameCanAccessTreasury(uf.Username, t, needsWithdrawPermission: true))
            {
                await raw.Channel.SendMessageAsync("❌ You don't have withdraw permission on this treasury (Officer or higher required for guild treasuries).");
                return;
            }

            int taken = TreasuryManager.TryWithdrawItem(t, uf.Username, defName, qty,
                TreasuryTransaction.TxKind.Withdraw, "discord-list");
            if (taken < qty)
            {
                if (taken > 0)
                    TreasuryManager.DepositItemForUser(uf.Username, defName, taken,
                        TreasuryTransaction.TxKind.MarketplaceRefund, "abort-list");
                await raw.Channel.SendMessageAsync($"❌ Treasury only has `{taken}× {label}`, need `{qty}`.");
                return;
            }

            var (ok, note, listing) = MarketplaceManager.CreateListing(uf.Username, defName, qty, unitPrice, isAutoListing: false);
            if (!ok)
            {
                TreasuryManager.DepositItemForUser(uf.Username, defName, qty,
                    TreasuryTransaction.TxKind.MarketplaceRefund, "list-create-failed");
                await raw.Channel.SendMessageAsync($"❌ {note}");
                return;
            }

            EmbedBuilder eb = new EmbedBuilder()
                .WithTitle("📝 Listing Posted")
                .WithDescription($"**{qty}× {label}** listed at `{unitPrice}s`/ea\nTotal asking: `{unitPrice * qty}s`")
                .AddField("Listing ID", $"#{listing.Id}", true)
                .AddField("Expires", FormatExpiry(listing.ExpiresUtcTicks), true)
                .WithColor(new Color(255, 196, 97))
                .WithFooter($"{DiscordBridge.ServerTag} · use `!cancel {listing.Id}` to refund");

            string thumb = DiscordItemIconMap.TryGetIconUrl(defName);
            if (!string.IsNullOrEmpty(thumb)) eb.WithThumbnailUrl(thumb);

            await raw.Channel.SendMessageAsync(embed: eb.Build());
        }

        private static async Task SendSellUsageAsync(SocketMessage raw)
        {
            EmbedBuilder eb = new EmbedBuilder()
                .WithTitle("📝 How to sell")
                .WithColor(new Color(255, 196, 97))
                .WithDescription(
                    "**Usage:** `!sell <item> <qty> <unit-price>`\n" +
                    "Item can be either a friendly name or the raw defName.\n\n" +
                    "**Examples:**\n" +
                    "• `!sell plasteel 50 12`     — 50× Plasteel @ 12s each\n" +
                    "• `!sell smokeleaf 100 5`   — 100× Smokeleaf joints\n" +
                    "• `!sell \"melee weapon\" 1 200` — quote multi-word names\n" +
                    "• `!sell Steel 200 2`         — exact defName works too\n\n" +
                    "**Tips:**\n" +
                    "• `!find <name>` — search live listings\n" +
                    "• `!items <name>` — see what defNames the server knows\n" +
                    "• `!treasury` — confirm what you actually own to sell")
                .WithFooter($"{DiscordBridge.ServerTag}");
            await raw.Channel.SendMessageAsync(embed: eb.Build());
        }

        // -- !find <query> --

        private static async Task HandleFindAsync(SocketMessage raw, string[] parts)
        {
            if (parts.Length < 2)
            {
                await raw.Channel.SendMessageAsync("Usage: `!find <name>` — searches active listings by item label.");
                return;
            }
            string query = string.Join(" ", parts, 1, parts.Length - 1).Replace('_', ' ').Trim().ToLowerInvariant();
            if (query.Length == 0) return;

            MarketplaceFile snap = MarketplaceManager.Snapshot();
            List<MarketplaceListing> all = snap.Listings ?? new List<MarketplaceListing>();

            List<MarketplaceListing> matches = new List<MarketplaceListing>();
            foreach (MarketplaceListing l in all)
            {
                string label = ItemLabelCache.LabelFor(l.ItemDefName).ToLowerInvariant();
                if (label.Contains(query) || l.ItemDefName.ToLowerInvariant().Contains(query))
                    matches.Add(l);
            }

            if (matches.Count == 0)
            {
                await raw.Channel.SendMessageAsync($"_No active listings match **{query}**._");
                return;
            }

            EmbedBuilder eb = new EmbedBuilder()
                .WithTitle($"🔎 Listings matching \"{query}\"")
                .WithColor(new Color(255, 196, 97))
                .WithFooter($"{matches.Count} match · `!buy <id> [qty]` to purchase · {DiscordBridge.ServerTag}");

            int max = Math.Min(matches.Count, PageSize);
            matches.Sort((a, b) => a.UnitPriceSilver.CompareTo(b.UnitPriceSilver));
            for (int i = 0; i < max; i++)
            {
                var l = matches[i];
                string emoji = DiscordItemIconMap.EmojiFor(l.ItemDefName);
                string label = ItemLabelCache.LabelFor(l.ItemDefName);
                string time = FormatExpiry(l.ExpiresUtcTicks);
                eb.AddField($"{emoji} #{l.Id} · {label}",
                    $"**{l.RemainingQty}**× @ `{l.UnitPriceSilver}s`/ea · total `{l.UnitPriceSilver * l.RemainingQty}s`\n" +
                    $"by **{l.SellerUsername}** · expires {time}",
                    inline: false);
            }

            await raw.Channel.SendMessageAsync(embed: eb.Build());
        }

        // -- !items / !catalog [query] --

        /// <summary>
        /// Discoverability helper: shows what defNames the server has been
        /// told about. Useful for players who want to know what string to
        /// type into !sell.
        /// </summary>
        private static async Task HandleCatalogAsync(SocketMessage raw, string[] parts)
        {
            string query = parts.Length >= 2
                ? string.Join(" ", parts, 1, parts.Length - 1).Replace('_', ' ').Trim()
                : null;

            if (string.IsNullOrEmpty(query))
            {
                await raw.Channel.SendMessageAsync(
                    $"📚 Server knows **{ItemLabelCache.Count}** item labels.\n" +
                    "Use `!items <name>` to search them — e.g. `!items wool`.");
                return;
            }

            string defName = ItemLabelCache.ResolveDefNameByQuery(query, out List<string> candidates);
            if (defName != null)
            {
                await raw.Channel.SendMessageAsync(
                    $"✅ **{ItemLabelCache.LabelFor(defName)}** → `{defName}`\n" +
                    $"To sell: `!sell {defName} <qty> <price>`");
                return;
            }
            if (candidates == null || candidates.Count == 0)
            {
                await raw.Channel.SendMessageAsync($"_No items match **{query}**._");
                return;
            }
            System.Text.StringBuilder sb2 = new System.Text.StringBuilder();
            sb2.Append($"🔎 **{candidates.Count}** items match **{query}**:\n");
            foreach (string c in candidates)
                sb2.Append("• **").Append(ItemLabelCache.LabelFor(c)).Append("** — `").Append(c).Append("`\n");
            await raw.Channel.SendMessageAsync(sb2.ToString());
        }

        /// <summary>
        /// Mini argv parser that respects double-quoted multi-word arguments,
        /// so `!sell "melee weapon" 1 200` parses to ["sell","melee weapon","1","200"].
        /// </summary>
        private static string[] ReparseQuoted(string content)
        {
            List<string> tokens = new List<string>();
            System.Text.StringBuilder cur = new System.Text.StringBuilder();
            bool inQuotes = false;
            foreach (char c in content)
            {
                if (c == '"') { inQuotes = !inQuotes; continue; }
                if (!inQuotes && (c == ' ' || c == '\t'))
                {
                    if (cur.Length > 0) { tokens.Add(cur.ToString()); cur.Length = 0; }
                }
                else cur.Append(c);
            }
            if (cur.Length > 0) tokens.Add(cur.ToString());
            return tokens.ToArray();
        }

        // -- !cancel --

        private static async Task HandleCancelAsync(SocketMessage raw, string[] parts)
        {
            UserFile uf = await RequireLinkedAsync(raw);
            if (uf == null) return;
            if (parts.Length < 2 || !long.TryParse(parts[1], out long id))
            {
                await raw.Channel.SendMessageAsync("Usage: `!cancel <listing-id>`");
                return;
            }

            var (ok, note) = MarketplaceManager.CancelListing(uf.Username, id);
            await raw.Channel.SendMessageAsync(ok ? $"✅ {note}" : $"❌ {note}");
        }

        // -- !treasury --

        private static async Task HandleTreasuryAsync(SocketMessage raw)
        {
            UserFile uf = await RequireLinkedAsync(raw);
            if (uf == null) return;

            string treasuryKey = TreasuryManager.ResolveKeyForUsername(uf.Username);
            if (string.IsNullOrEmpty(treasuryKey))
            {
                await raw.Channel.SendMessageAsync("❌ Could not resolve your treasury.");
                return;
            }
            bool isGuild = !treasuryKey.StartsWith("_personal:", StringComparison.OrdinalIgnoreCase);
            TreasuryFile t = TreasuryManager.GetOrCreate(treasuryKey, isGuild);

            EmbedBuilder eb = new EmbedBuilder()
                .WithTitle(isGuild ? $"🏰 {treasuryKey} Treasury" : "🏦 Personal Vault")
                .WithColor(new Color(255, 220, 100))
                .AddField("Silver", $"`{t.SilverBalance}s`", true)
                .AddField("Lifetime in", $"`{t.LifetimeSilverIn:N0}s`", true)
                .AddField("Lifetime out", $"`{t.LifetimeSilverOut:N0}s`", true);

            if (t.Items != null && t.Items.Count > 0)
            {
                // Cap at 24 lines (Discord embed field limits) — show top stacks first.
                var tops = t.Items.OrderByDescending(kv => kv.Value).Take(24);
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                foreach (var kv in tops)
                {
                    sb.Append(DiscordItemIconMap.EmojiFor(kv.Key));
                    sb.Append(' ').Append(ItemLabelCache.LabelFor(kv.Key)).Append(" × ").Append(kv.Value).Append('\n');
                }
                eb.AddField("Items", sb.ToString().TrimEnd(), inline: false);
            }
            else
            {
                eb.AddField("Items", "_(empty)_", inline: false);
            }

            eb.WithFooter($"User: {uf.Username} · {DiscordBridge.ServerTag}");
            await raw.Channel.SendMessageAsync(embed: eb.Build());
        }

        // -- !quests --

        private static async Task HandleQuestsAsync(SocketMessage raw)
        {
            QuestBoard b = QuestManager.Snapshot();
            List<QuestFile> openOrClaimed = (b.Quests ?? new List<QuestFile>())
                .Where(q => q.State == QuestState.Open || q.State == QuestState.Claimed)
                .OrderByDescending(q => q.PostedUtcTicks)
                .Take(10)
                .ToList();

            EmbedBuilder eb = new EmbedBuilder()
                .WithTitle("📜 Quest Board")
                .WithColor(new Color(186, 132, 246))
                .WithFooter($"{DiscordBridge.ServerTag}");

            if (openOrClaimed.Count == 0)
            {
                eb.WithDescription("_No open quests right now._");
            }
            else
            {
                foreach (QuestFile q in openOrClaimed)
                {
                    string kindEmoji = q.Kind == QuestKind.DeliverItem ? "📦" : "⚔";
                    string state = q.State == QuestState.Open ? "OPEN" : $"CLAIMED by {q.ClaimedByUsername}";
                    string detail = q.Kind == QuestKind.DeliverItem
                        ? $"Deliver **{q.TargetItemQty}× {ItemLabelCache.LabelFor(q.TargetItemDefName)}**"
                        : (q.Description.Length > 120 ? q.Description.Substring(0, 120) + "…" : q.Description);
                    eb.AddField(
                        $"{kindEmoji} #{q.Id} · {q.Title}",
                        $"{detail}\nBounty: `{q.BountySilver}s` · by **{q.PosterUsername}** · {state}",
                        inline: false);
                }
            }

            await raw.Channel.SendMessageAsync(embed: eb.Build());
        }

        // -- text helpers --

        private static string FormatExpiry(long ticks)
        {
            long now = DateTime.UtcNow.Ticks;
            long diff = ticks - now;
            if (diff <= 0) return "expired";
            TimeSpan ts = new TimeSpan(diff);
            if (ts.TotalDays >= 1.0) return $"in {(int)ts.TotalDays}d {ts.Hours}h";
            if (ts.TotalHours >= 1.0) return $"in {(int)ts.TotalHours}h {ts.Minutes}m";
            return $"in {(int)ts.TotalMinutes}m";
        }
    }
}
