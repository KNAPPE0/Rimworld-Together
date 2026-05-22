using Discord;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameServer.Integrations.Discord
{
    /// <summary>
    /// Centralised, opinionated builder for command replies.
    ///
    /// Consistent rules for every command:
    ///   - Info style for normal output (NOT "Warning")
    ///   - Success / Error / Warning each get their own colour + icon
    ///   - Long lists paginate via <see cref="BuildPaged"/> instead of spamming
    ///   - Footer always carries server tag + page indicator + requester
    /// </summary>
    public static class DiscordResponseBuilder
    {
        public enum Style { Info, Success, Warning, Error, Admin }

        // Colour palette (consistent across all commands).
        private static readonly Color InfoColor = new Color(96, 153, 205);     // muted blue
        private static readonly Color SuccessColor = new Color(120, 200, 120); // green
        private static readonly Color WarningColor = new Color(240, 180, 60);  // amber
        private static readonly Color ErrorColor = new Color(220, 80, 80);     // red
        private static readonly Color AdminColor = new Color(186, 132, 246);   // purple

        public const int DefaultPageSize = 10;
        public const int MaxLinesPerEmbed = 25; // safe under Discord's 25-field cap

        /// <summary>
        /// KMH: Page size resolved from the server's <c>DiscordOutputMode</c>
        /// config (0 = Compact / 1 = Normal / 2 = Verbose). Use this in
        /// commands instead of hard-coding the page size.
        /// </summary>
        public static int PageSizeFromConfig
        {
            get
            {
                int mode = GameServer.Core.Master.ServerConfig?.DiscordOutputMode ?? 1;
                switch (mode)
                {
                    case 0: return 10;
                    case 2: return 50;
                    default: return 25;
                }
            }
        }

        // -- single-card embed --

        public static Embed Build(Style style, string title, string description = null, string footer = null,
            IEnumerable<KeyValuePair<string, string>> fields = null, string thumbnailUrl = null)
        {
            EmbedBuilder eb = new EmbedBuilder()
                .WithTitle($"{IconFor(style)} {title}")
                .WithColor(ColorFor(style))
                .WithTimestamp(DateTimeOffset.UtcNow);

            if (!string.IsNullOrWhiteSpace(description))
                eb.WithDescription(description);

            if (fields != null)
            {
                foreach (var kv in fields)
                {
                    if (string.IsNullOrEmpty(kv.Key)) continue;
                    eb.AddField(kv.Key, string.IsNullOrEmpty(kv.Value) ? "—" : kv.Value, inline: false);
                }
            }

            if (!string.IsNullOrEmpty(thumbnailUrl))
                eb.WithThumbnailUrl(thumbnailUrl);

            string fullFooter = ComposeFooter(footer);
            if (!string.IsNullOrEmpty(fullFooter))
                eb.WithFooter(fullFooter);

            return eb.Build();
        }

        // -- paginated list embed --

        /// <summary>
        /// Builds a paginated embed for a list of pre-formatted lines.
        /// One embed per call — caller posts the embed and tells the user how
        /// to ask for the next page.
        /// </summary>
        public static Embed BuildPaged(
            Style style,
            string title,
            IList<string> lines,
            int pageOneIndexed,
            int pageSize,
            string footerHint,
            string descriptionPrefix = null)
        {
            int totalLines = lines?.Count ?? 0;
            int pageSizeClamped = Math.Max(1, Math.Min(pageSize, MaxLinesPerEmbed));
            int totalPages = totalLines == 0 ? 1 : (totalLines + pageSizeClamped - 1) / pageSizeClamped;
            if (pageOneIndexed < 1) pageOneIndexed = 1;
            if (pageOneIndexed > totalPages) pageOneIndexed = totalPages;
            int skip = (pageOneIndexed - 1) * pageSizeClamped;

            EmbedBuilder eb = new EmbedBuilder()
                .WithTitle($"{IconFor(style)} {title}")
                .WithColor(ColorFor(style))
                .WithTimestamp(DateTimeOffset.UtcNow);

            StringBuilder body = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(descriptionPrefix))
                body.AppendLine(descriptionPrefix).AppendLine();

            if (totalLines == 0)
            {
                body.AppendLine("_No results._");
            }
            else
            {
                for (int i = skip; i < Math.Min(skip + pageSizeClamped, totalLines); i++)
                    body.AppendLine(lines[i]);
            }

            // Discord embed body cap is 4096; in practice we keep it well short.
            string text = body.ToString();
            if (text.Length > 3800)
                text = text.Substring(0, 3800) + "\n_(more — use the next page)_";
            eb.WithDescription(text);

            string footer = totalPages > 1
                ? $"Page {pageOneIndexed}/{totalPages}  ·  {totalLines} total"
                : $"{totalLines} entr{(totalLines == 1 ? "y" : "ies")}";
            if (!string.IsNullOrWhiteSpace(footerHint))
                footer += "  ·  " + footerHint;

            eb.WithFooter(ComposeFooter(footer));
            return eb.Build();
        }

        // -- helpers --

        private static string ComposeFooter(string extra)
        {
            string serverTag = GameServer.Core.Master.ServerConfig?.DiscordServerTag;
            if (string.IsNullOrWhiteSpace(serverTag))
                serverTag = $"S{GameServer.Core.Master.ServerConfig?.Port ?? 0}";

            if (string.IsNullOrEmpty(extra)) return serverTag;
            return $"{extra}  ·  {serverTag}";
        }

        public static Color ColorFor(Style style)
        {
            switch (style)
            {
                case Style.Success: return SuccessColor;
                case Style.Warning: return WarningColor;
                case Style.Error: return ErrorColor;
                case Style.Admin: return AdminColor;
                default: return InfoColor;
            }
        }

        public static string IconFor(Style style)
        {
            switch (style)
            {
                case Style.Success: return "✅";
                case Style.Warning: return "⚠";
                case Style.Error: return "❌";
                case Style.Admin: return "🛡";
                default: return "ℹ";
            }
        }
    }
}
