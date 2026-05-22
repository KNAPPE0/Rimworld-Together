using GameServer.Core;
using Shared;
using Shared.Files.Configs.Mods;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameServer.Commands
{
    /// <summary>
    /// KMH: Mod list with filters + paging.
    ///
    /// Usage:
    ///   modlist                  — grouped overview (counts + first page)
    ///   modlist required [page]  — only required mods
    ///   modlist optional [page]  — only optional mods
    ///   modlist forbidden [page] — only forbidden mods
    ///   modlist all [page]       — flat list of every mod
    /// </summary>
    public class CMD_Modlist : CMD_Base
    {
        private const int PageSize = 25;

        public CMD_Modlist()
        {
            Prefix = "modlist";
            Description = "List server mods. modlist [required|optional|forbidden|all] [page]";
            ParameterCount = -1;
            Category = "Config";
        }

        public override void Action()
        {
            string[] args = CMD_Base.CommandParameters ?? Array.Empty<string>();
            string filter = args.Length >= 1 ? args[0].ToLowerInvariant() : "";
            int page = 1;
            if (args.Length >= 2 && int.TryParse(args[1], out int p) && p > 0) page = p;

            // Quick "modlist <page>" syntax (no filter, just a page number).
            if (!string.IsNullOrEmpty(filter) && int.TryParse(filter, out int alt) && alt > 0)
            {
                page = alt;
                filter = "";
            }

            List<ModConfig> required = Master.ModConfig.ModConfigs.Where(c => c.Type == ModConfigFile.ModType.Required).ToList();
            List<ModConfig> optional = Master.ModConfig.ModConfigs.Where(c => c.Type == ModConfigFile.ModType.Optional).ToList();
            List<ModConfig> forbidden = Master.ModConfig.ModConfigs.Where(c => c.Type == ModConfigFile.ModType.Forbidden).ToList();

            switch (filter)
            {
                case "required":
                    PrintGroup("Required Mods", required, page);
                    break;
                case "optional":
                    PrintGroup("Optional Mods", optional, page);
                    break;
                case "forbidden":
                    PrintGroup("Forbidden Mods", forbidden, page);
                    break;
                case "all":
                    {
                        // Flat list, retain group order.
                        List<ModConfig> all = required.Concat(optional).Concat(forbidden).ToList();
                        PrintGroup("All Mods", all, page);
                    }
                    break;
                default:
                    {
                        // Default: grouped overview.
                        Printer.Title($"Mod summary — {required.Count} required, {optional.Count} optional, {forbidden.Count} forbidden");
                        Printer.Title("----------------------------------------");
                        Printer.Warning($"Required: {required.Count}");
                        Printer.Warning($"Optional: {optional.Count}");
                        Printer.Warning($"Forbidden: {forbidden.Count}");
                        Printer.Title("----------------------------------------");
                        Printer.Warning("Use: modlist required | optional | forbidden | all  (with optional [page])");
                    }
                    break;
            }
        }

        private static void PrintGroup(string title, List<ModConfig> mods, int page)
        {
            int totalPages = mods.Count == 0 ? 1 : (mods.Count + PageSize - 1) / PageSize;
            if (page > totalPages) page = totalPages;
            int skip = (page - 1) * PageSize;

            Printer.Title($"{title} — page {page}/{totalPages} ({mods.Count} total)");
            Printer.Title("----------------------------------------");

            if (mods.Count == 0)
            {
                Printer.Warning("(none)");
            }
            else
            {
                int idx = skip;
                foreach (ModConfig c in mods.Skip(skip).Take(PageSize))
                {
                    idx++;
                    Printer.Warning($"{idx,3}. {c.FileName}");
                }
            }

            Printer.Title("----------------------------------------");
            if (page < totalPages)
                Printer.Warning($"Next page: modlist {title.Split(' ')[0].ToLowerInvariant()} {page + 1}");
        }
    }
}
