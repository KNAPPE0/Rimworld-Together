using GameServer.Core;
using GameServer.PacketManager;
using Shared;
using Shared.Files.Sites;
using Shared.Misc;
using System.IO;
using System.Linq;

namespace GameServer.Commands
{
    public class CMD_Sites : CMD_Base
    {
        public CMD_Sites()
        {
            Prefix = "sites";
            Description = "Lists all sites, or 'sites custom' for custom sites only";
            ParameterCount = -1;
        }

        public override void Action()
        {
            string arg = CMD_Base.CommandParameters != null && CMD_Base.CommandParameters.Length > 0
                ? CMD_Base.CommandParameters[0].ToLower() : "";

            SiteFile[] allSites = SiteManagerHelper.GetAllSites();

            if (arg == "custom")
            {
                SiteFile[] customSites = allSites.Where(s => s.Type?.IsCustom == true).ToArray();
                Printer.Title($"Custom sites: [{customSites.Length}]");
                Printer.Title("----------------------------------------");
                foreach (SiteFile site in customSites)
                {
                    string customPath = Path.Combine(Master.SitesPath, $"{site.Tile}_custom.json");
                    string extra = "";
                    if (File.Exists(customPath))
                    {
                        try
                        {
                            CustomSiteData cd = Serializer.SerializeFromFile<CustomSiteData>(customPath);
                            extra = $" | {cd.BaseAmountPerCycle}x {cd.ItemDefName} | Workers: {cd.Workers?.Count ?? 0}/{cd.MaxWorkers} | {cd.AccessMode}";
                        }
                        catch { }
                    }
                    Printer.Warning($"Tile {site.Tile} - {site.Username} ({site.GuildName}){extra}");
                }
                Printer.Title("----------------------------------------");
            }
            else
            {
                Printer.Title($"All sites: [{allSites.Length}]");
                Printer.Title("----------------------------------------");
                foreach (SiteFile site in allSites)
                {
                    string type = site.Type?.DefName ?? "unknown";
                    string custom = (site.Type?.IsCustom == true) ? " [CUSTOM]" : "";
                    Printer.Warning($"Tile {site.Tile} - {site.Username} ({site.GuildName}) - {type}{custom}");
                }
                Printer.Title("----------------------------------------");
            }
        }
    }
}
