using GameServer.Managers;
using GameServer.Misc;
using Shared;
using Shared.Files;
using Shared.Files.Guilds;
using Shared.Files.Sites;
using System.Linq;
using System.Security.Policy;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets.Goodwills;
using static Shared.CommonEnumerators;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace GameServer.PacketManager
{
    public class PM_Goodwills : PM_Base
    {
        [HandlesPacket(PacketHeader.GoodWillManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_FactionGoodwill data = Serializer.ConvertBytesToObject<PKT_FactionGoodwill>(bytes);

            ChangeUserGoodwills(client, data);
        }

        public static void ChangeUserGoodwills(ServerClient client, PKT_FactionGoodwill data)
        {
            SettlementFile settlementFile = PM_Settlements.GetSettlementFileFromTile(data._tile);
            SiteFile siteFile = SiteManagerHelper.GetSiteFileFromTile(data._tile);

            if (settlementFile != null) data._username = settlementFile.Username;
            else data._username = siteFile.Username;

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
            SettlementFile[] settlements = PM_Settlements.GetAllSettlements().Where(fetch => fetch.Username != client.UserFile.Username).ToArray();
            SiteFile[] sites = SiteManagerHelper.GetAllSites().Where(fetch => fetch.Username != client.UserFile.Username).ToArray();

            PKT_FactionGoodwill factionGoodwillData = new PKT_FactionGoodwill();
            foreach (SettlementFile settlement in settlements)
            {
                PKT_SettlementGoodwill goodwill = new PKT_SettlementGoodwill();
                goodwill.Tile = settlement.Tile;
                goodwill.Goodwill = GetSettlementGoodwill(client, settlement);

                factionGoodwillData._settlements.Add(goodwill);
            }

            foreach (SiteFile site in sites)
            {
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