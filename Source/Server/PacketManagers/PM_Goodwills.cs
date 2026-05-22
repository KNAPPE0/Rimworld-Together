using GameServer.Managers;
using Shared;
using Shared.Files;
using Shared.Files.Guilds;
using Shared.Files.Sites;
using Shared.Misc;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets.Goodwills;
using static Shared.CommonEnumerators;

namespace GameServer.PacketManager
{
    public class PM_Goodwills : PM_Base
    {
        [HandlesPacket(PacketHeader.GoodWillManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_FactionGoodwill data = Serializer.ConvertBytesToObject<PKT_FactionGoodwill>(bytes);
            if (data == null) return;

            ChangeUserGoodwills(client, data);
        }

        public static void ChangeUserGoodwills(ServerClient client, PKT_FactionGoodwill data)
        {
            SettlementFile settlementFile = PM_Settlements.GetSettlementFileFromTile(data._tile);
            SiteFile siteFile = SiteManagerHelper.GetSiteFileFromTile(data._tile);

            // Server-authoritative username; orphan tile = forged packet.
            if (settlementFile != null) data._username = settlementFile.Username;
            else if (siteFile != null) data._username = siteFile.Username;
            else
            {
                Printer.Warning($"[Goodwill] {client?.UserFile?.Username} sent change for orphan tile {data._tile}. Ignored.");
                return;
            }

            GuildFile guild = GuildManagerH.GetFactionFromName(client.UserFile.GuildName);
            if (guild != null && GuildManagerH.CheckIfUserIsInFaction(guild, data._username))
            {
                ResponseShortcutManager.SendBreakPacket(client);
                return;
            }

            client.UserFile.UpdateGoodwill(data._username, data._goodwill);
            UpdateClientGoodwills(client);
        }

        public static void UpdateClientGoodwills(ServerClient client)
        {
            // Was allocating two intermediate arrays via
            // LINQ Where().ToArray() just to skip the requester's own
            // settlements/sites. Inline the filter into the existing
            // foreach so we walk each cached array exactly once with zero
            // intermediate allocation.
            string mine = client?.UserFile?.Username;
            PKT_FactionGoodwill factionGoodwillData = new PKT_FactionGoodwill();

            foreach (SettlementFile settlement in PM_Settlements.GetAllSettlements())
            {
                if (settlement == null || settlement.Username == mine) continue;
                PKT_SettlementGoodwill goodwill = new PKT_SettlementGoodwill();
                goodwill.Tile = settlement.Tile;
                goodwill.Goodwill = GetSettlementGoodwill(client, settlement);
                factionGoodwillData._settlements.Add(goodwill);
            }

            foreach (SiteFile site in SiteManagerHelper.GetAllSites())
            {
                if (site == null || site.Username == mine) continue;
                PKT_SiteGoodwill goodwill = new PKT_SiteGoodwill();
                goodwill.Tile = site.Tile;
                goodwill.Goodwill = GetSiteGoodwill(client, site);
                factionGoodwillData._sites.Add(goodwill);
            }

            client.Listener.EnqueuePacket(PacketHeader.GoodWillManager, factionGoodwillData);
        }

        public static Goodwill GetSettlementGoodwill(ServerClient client, SettlementFile settlement)
        {
            GuildFile guild = GuildManagerH.GetFactionFromName(client.UserFile.GuildName);

            if (client.UserFile.Username == settlement.Username) return Goodwill.Personal;
            else if (guild == null) return FindGoodwillFromUsername(client.UserFile, settlement.Username);
            else
            {
                if (GuildManagerH.GetAllFactionMembers(guild).FirstOrDefault(fetch => fetch.Username == settlement.Username) != null) return Goodwill.Guild;
                else return FindGoodwillFromUsername(client.UserFile, settlement.Username);
            }
        }

        public static Goodwill GetSiteGoodwill(ServerClient client, SiteFile site)
        {
            GuildFile guild = GuildManagerH.GetFactionFromName(client.UserFile.GuildName);

            if (client.UserFile.Username == site.Username) return Goodwill.Personal;
            else if (guild == null) return FindGoodwillFromUsername(client.UserFile, site.Username);
            else
            {
                if (GuildManagerH.GetAllFactionMembers(guild).FirstOrDefault(fetch => fetch.Username == site.Username) != null) return Goodwill.Guild;
                else return FindGoodwillFromUsername(client.UserFile, site.Username);
            }
        }

        public static Goodwill FindGoodwillFromUsername(UserFile file, string username)
        {
            if (file.Goodwills.Count == 0) return Goodwill.Neutral;
            else
            {
                PlayerGoodwill toFind = file.Goodwills.FirstOrDefault(fetch => fetch.Name == username);
                if (toFind == null) return Goodwill.Neutral;
                else if (toFind.Name == file.Username) return Goodwill.Personal;
                else return toFind.Goodwill;
            }
        }
    }
}