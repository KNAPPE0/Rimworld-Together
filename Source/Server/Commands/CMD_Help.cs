using Shared;
using Shared.Misc;
using System.Collections.Generic;
using System.Linq;

namespace GameServer.Commands
{
    /// <summary>
    /// KMH: Categorised help.
    ///
    ///   <c>help</c>             — list categories + counts
    ///   <c>help &lt;category&gt;</c> — list commands in that category
    ///   <c>help all</c>         — full flat list (the legacy behaviour)
    /// </summary>
    public class CMD_Help : CMD_Base
    {
        public CMD_Help()
        {
            Prefix = "help";
            Description = "Shows command categories. Use 'help <category>' to drill in, or 'help all' for everything.";
            ParameterCount = -1;
            Category = "Server";
        }

        public override void Action()
        {
            string arg = (CommandParameters != null && CommandParameters.Length > 0)
                ? (CommandParameters[0] ?? string.Empty).Trim().ToLowerInvariant()
                : string.Empty;

            List<CMD_Base> all = Commands.OrderBy(c => c.Prefix).ToList();

            if (string.IsNullOrEmpty(arg))
            {
                PrintCategorySummary(all);
                return;
            }

            if (arg == "all" || arg == "*")
            {
                Printer.Title($"All commands ({all.Count})");
                Printer.Title("----------------------------------------");
                foreach (CMD_Base c in all)
                    Printer.Warning($"{c.Prefix.PadRight(22)} [{c.ResolvedCategory}]  {c.Description}");
                Printer.Title("----------------------------------------");
                return;
            }

            // Treat as category name. Match case-insensitively.
            List<CMD_Base> inCategory = all
                .Where(c => string.Equals(c.ResolvedCategory, arg, System.StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (inCategory.Count == 0)
            {
                Printer.Warning($"No category '{arg}'. Try: help");
                return;
            }

            Printer.Title($"{char.ToUpper(arg[0]) + arg.Substring(1)} commands ({inCategory.Count})");
            Printer.Title("----------------------------------------");
            foreach (CMD_Base c in inCategory)
                Printer.Warning($"{c.Prefix.PadRight(22)} {c.Description}");
            Printer.Title("----------------------------------------");
        }

        private static void PrintCategorySummary(List<CMD_Base> all)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            foreach (CMD_Base c in all)
            {
                string cat = c.ResolvedCategory;
                if (!counts.TryGetValue(cat, out int existing)) existing = 0;
                counts[cat] = existing + 1;
            }

            Printer.Title($"Help — {all.Count} commands across {counts.Count} categor{(counts.Count == 1 ? "y" : "ies")}");
            Printer.Title("----------------------------------------");
            foreach (var kv in counts.OrderBy(p => p.Key))
                Printer.Warning($"{kv.Key.PadRight(14)} {kv.Value,3}   try: help {kv.Key.ToLowerInvariant()}");
            Printer.Title("----------------------------------------");
            Printer.Warning("Tip: use 'help all' for the full flat list.");
        }
    }
}
