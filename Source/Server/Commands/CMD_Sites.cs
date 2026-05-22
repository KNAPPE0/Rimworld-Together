using GameServer.Core;
using GameServer.PacketManager;
using Shared;
using Shared.Files.Sites;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GameServer.Commands
{
    /// <summary>
    /// KMH: Site listing command with filters + paging.
    ///
    /// Usage:
    ///   sites                       — all sites, page 1
    ///   sites &lt;page&gt;                — all sites, page N
    ///   sites custom [page]         — only custom sites
    ///   sites guild &lt;name&gt; [page]   — sites belonging to a guild
    ///   sites player &lt;name&gt; [page]  — sites owned by a player
    /// </summary>
    public class CMD_Sites : CMD_Base
    {
        private const int PageSize = 25;

        public CMD_Sites()
        {
            Prefix = "sites";
            Description = "List sites with filters: sites [page] | sites custom [page] | sites guild <name> [page] | sites player <name> [page]";
            ParameterCount = -1;
            Category = "Economy";
        }

        public override void Action()
        {
            string[] args = CMD_Base.CommandParameters ?? Array.Empty<string>();
            string filter = args.Length >= 1 ? args[0].ToLowerInvariant() : "";
            string filterValue = null;
            int page = 1;

            // Parse filter syntax.
            // - "sites" / "sites 2"
            // - "sites custom" / "sites custom 2"
            // - "sites guild <name> [page]"
            // - "sites player <name> [page]"
            if (filter == "guild" || filter == "player")
            {
                if (args.Length < 2)
                {
                    Printer.Warning($"Usage: sites {filter} <name> [page]");
                    return;
                }
                filterValue = args[1];
                if (args.Length >= 3 && int.TryParse(args[2], out int p) && p > 0) page = p;
            }
            else if (filter == "custom")
            {
                if (args.Length >= 2 && int.TryParse(args[1], out int p) && p > 0) page = p;
            }
            else
            {
                // Plain "sites" or "sites <page>"
                if (!string.IsNullOrEmpty(filter) && int.TryParse(filter, out int p) && p > 0)
                {
                    page = p;
                    filter = "";
                }
            }

            SiteFile[] allSites = SiteManagerHelper.GetAllSites();

            // Apply filter.
            IEnumerable<SiteFile> filtered = allSites;
            string headerLabel;
            switch (filter)
            {
                case "custom":
                    filtered = allSites.Where(s => s.Type?.IsCustom == true);
                    headerLabel = "Custom sites";
                    break;
                case "guild":
                    filtered = allSites.Where(s => string.Equals(s.GuildName, filterValue, StringComparison.OrdinalIgnoreCase));
                    headerLabel = $"Sites for guild '{filterValue}'";
                    break;
                case "player":
                    filtered = allSites.Where(s => string.Equals(s.Username, filterValue, StringComparison.OrdinalIgnoreCase));
                    headerLabel = $"Sites for player '{filterValue}'";
                    break;
                default:
                    headerLabel = "All sites";
                    break;
            }

            List<SiteFile> rows = filtered.OrderBy(s => s.Tile).ToList();
            int totalPages = Math.Max(1, (rows.Count + PageSize - 1) / PageSize);
            if (page > totalPages) page = totalPages;
            int skip = (page - 1) * PageSize;

            Printer.Title($"{headerLabel} — page {page}/{totalPages} ({rows.Count} total)");
            Printer.Title("----------------------------------------");

            if (rows.Count == 0)
            {
                Printer.Warning("(none)");
            }
            else
            {
                foreach (SiteFile site in rows.Skip(skip).Take(PageSize))
                {
                    string type = site.Type?.DefName ?? "unknown";
                    string custom = (site.Type?.IsCustom == true) ? " [CUSTOM]" : "";
                    string guild = string.IsNullOrEmpty(site.GuildName) ? "" : $" ({site.GuildName})";

                    if (site.Type?.IsCustom == true)
                    {
                        // Inline custom-site info to give admins context.
                        string customPath = Path.Combine(Master.SitesPath, $"{site.Tile}_custom.json");
                        string extra = "";
                        if (File.Exists(customPath))
                        {
                            try
                            {
                                CustomSiteData cd = Serializer.SerializeFromFile<CustomSiteData>(customPath);
                                int workers = cd.Workers?.Count ?? 0;
                                extra = $" | {cd.BaseAmountPerCycle}× {cd.ItemDefName} | {workers}/{cd.MaxWorkers} workers | {cd.AccessMode}";
                            }
                            catch { }
                        }
                        Printer.Warning($"Tile {site.Tile,-7} {site.Username,-16}{guild,-20}{custom}{extra}");
                    }
                    else
                    {
                        Printer.Warning($"Tile {site.Tile,-7} {site.Username,-16}{guild,-20} {type}");
                    }
                }
            }

            Printer.Title("----------------------------------------");
            if (totalPages > 1 && page < totalPages)
            {
                string nextArgs = filter switch
                {
                    "custom" => $"custom {page + 1}",
                    "guild" => $"guild {filterValue} {page + 1}",
                    "player" => $"player {filterValue} {page + 1}",
                    _ => $"{page + 1}"
                };
                Printer.Warning($"Next page: sites {nextArgs}");
            }
        }
    }
}
