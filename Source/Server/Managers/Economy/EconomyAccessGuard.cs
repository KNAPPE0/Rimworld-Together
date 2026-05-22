using GameServer.Core;
using GameServer.PacketManager;
using Shared.Files.Sites;
using TCPNetwork.Files.Client;

namespace GameServer.Managers
{
    /// <summary>
    /// KMH: Server-side gate for site-only economy access.
    ///
    /// When <see cref="Shared.Files.Actions.SiteAction.RequireSiteAccessForEconomy"/>
    /// is on, marketplace listing/buying and quest posting require the caller
    /// to either own a site or be an active worker at one. Admins bypass.
    ///
    /// This is intentionally a "has a site at all" check, not "is currently
    /// standing on a site tile" — the server has no real-time caravan position
    /// data, and we don't want to tie economy access to which dialog you opened.
    /// </summary>
    public static class EconomyAccessGuard
    {
        public static bool IsRequired => Master.ActionConfigs?.SiteAction?.RequireSiteAccessForEconomy ?? false;

        /// <summary>
        /// Returns true if the caller is allowed to use economy commands per
        /// the configured policy.
        /// </summary>
        public static bool IsAllowed(ServerClient client)
        {
            if (client?.UserFile == null) return false;
            if (client.UserFile.IsAdmin) return true;
            if (!IsRequired) return true;

            string username = client.UserFile.Username;
            if (string.IsNullOrEmpty(username)) return false;

            // Owns at least one site → ok.
            SiteFile[] owned = SiteManagerHelper.GetAllSitesFromUsername(username);
            if (owned != null && owned.Length > 0) return true;

            // Or is listed as a worker on any custom site.
            foreach (SiteFile site in SiteManagerHelper.GetAllSites())
            {
                if (site?.Type == null || !site.Type.IsCustom) continue;
                string customPath = System.IO.Path.Combine(Master.SitesPath, $"{site.Tile}_custom.json");
                CustomSiteData cd = SiteManagerHelper.LoadCustomSiteData(customPath);
                if (cd?.Workers != null && cd.Workers.Contains(username)) return true;
            }

            return false;
        }

        public static string DenyReason =>
            "This server requires you to own or work at a site before using marketplace/quests. Build a custom site or join an existing one first.";
    }
}
