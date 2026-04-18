using GameServer.Core;
using GameServer.Hooks.TCPNetwork;
using GameServer.PacketManager;
using Shared;
using Shared.Files.Sites;
using Shared.Misc;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using System.IO;

namespace GameServer.Commands
{
    public class CMD_DeleteSite : CMD_Base
    {
        public CMD_DeleteSite()
        {
            Prefix = "deletesite";
            Description = "Deletes a site by tile number. Usage: deletesite <tile>";
            ParameterCount = 1;
        }

        public override void Action()
        {
            string tileStr = CMD_Base.CommandParameters[0];
            if (!int.TryParse(tileStr, out int tile))
            {
                Printer.Warning($"Invalid tile number: {tileStr}");
                return;
            }

            SiteFile site = SiteManagerHelper.GetSiteFileFromTile(tile);
            if (site == null)
            {
                Printer.Warning($"No site found at tile {tile}");
                return;
            }

            // Delete site file
            string sitePath = Path.Combine(Master.SitesPath, tile + Shared.CommonValues.DefaultSaveFormat);
            if (File.Exists(sitePath))
            {
                try { File.Delete(sitePath); }
                catch { Printer.Warning($"Failed to delete site file at {sitePath}"); return; }
            }

            // Delete custom data if exists
            string customPath = Path.Combine(Master.SitesPath, $"{tile}_custom.json");
            if (File.Exists(customPath))
            {
                try { File.Delete(customPath); } catch { }
            }

            // Notify all connected clients to remove the site
            PKT_Site destroyData = new PKT_Site();
            destroyData._stepMode = PKT_Site.SiteStepMode.Destroy;
            destroyData._file = site;

            foreach (ServerClient client in ServerNetwork.GetConnectedClients())
                client.Listener.EnqueuePacket(PacketHeader.SiteManager, destroyData);

            Printer.Title($"Deleted site at tile {tile} (owner: {site.Username}, type: {site.Type?.DefName})");
        }
    }
}
