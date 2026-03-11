using GameClient.Dialogs;
using GameClient.Misc;
using GameClient.WorldObjects;
using RimWorld;
using RimWorld.Planet;
using Shared;
using System.Collections.Generic;
using System.Linq;
using Verse;
using static Shared.CommonEnumerators;
using static UnityEngine.GraphicsBuffer;
using TCPNetwork.Packets.Goodwills;
using Shared.Files.Sites;
using GameClient.Hooks.TCPNetwork;
using TCPNetwork;
using GameClient.Managers;
using TCPNetwork.Files.Client;


namespace GameClient.PacketManagers
{
    //Class that handles settlement and site player goodwills

    public class PM_Goodwills : PM_Base
    {
        [HandlesPacket(PacketHeader.GoodWillManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_FactionGoodwill data = Serializer.ConvertBytesToObject<PKT_FactionGoodwill>(bytes);

            ChangeStructureGoodwill(data);
            DLG_Wait.Instance.Close();
        }

        //Tries to request a goodwill change depending on the values given

        public static void TryRequestGoodwill(Goodwill type, GoodwillTarget target)
        {
            int tileToUse = 0;
            if (target == GoodwillTarget.Settlement) tileToUse = SessionHandler.ChosenSettlement.Tile;
            else if (target == GoodwillTarget.Site) tileToUse = SessionHandler.ChosenSite.Tile;

            Faction factionToUse = null;
            if (target == GoodwillTarget.Settlement) factionToUse = SessionHandler.ChosenSettlement.Faction;
            else if (target == GoodwillTarget.Site) factionToUse = SessionHandler.ChosenSite.Faction;

            if (type == Goodwill.Enemy)
            {
                if (factionToUse == SessionHandler.EnemyFaction)
                {
                    DLG_Message d1 = new DLG_Message("ERROR", new string[] { "Chosen settlement is already marked as enemy!" });
                    DLG_Base.PushNewDialog(d1);
                }
                else RequestChangeStructureGoodwill(tileToUse, Goodwill.Enemy);
            }

            else if (type == Goodwill.Neutral)
            {
                if (factionToUse == SessionHandler.NeutralFaction)
                {
                    DLG_Message d1 = new DLG_Message("ERROR", new string[] { "Chosen settlement is already marked as neutral!" });
                    DLG_Base.PushNewDialog(d1);
                }
                else RequestChangeStructureGoodwill(tileToUse, Goodwill.Neutral);
            }

            else if (type == Goodwill.Ally)
            {
                if (factionToUse == SessionHandler.AllyFaction)
                {
                    DLG_Message d1 = new DLG_Message("ERROR", new string[] { "Chosen settlement is already marked as ally!" });
                    DLG_Base.PushNewDialog(d1);
                }
                else RequestChangeStructureGoodwill(tileToUse, Goodwill.Ally);
            }
        }

        //Requests a structure goodwill change to the server

        public static void RequestChangeStructureGoodwill(int structureTile, Goodwill goodwill)
        {
            PKT_FactionGoodwill factionGoodwillData = new PKT_FactionGoodwill();
            factionGoodwillData._tile = structureTile;
            factionGoodwillData._goodwill = goodwill;

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.GoodWillManager, factionGoodwillData);

            DLG_Wait d1 = new DLG_Wait("Changing settlement goodwill");
            DLG_Base.PushNewDialog(d1);
        }

        //Changes a structure goodwill from a packet

        public static void ChangeStructureGoodwill(PKT_FactionGoodwill data)
        {
            ChangeSettlementGoodwills(data);
            ChangeSiteGoodwills(data);
        }

        private static void ChangeSettlementGoodwills(PKT_FactionGoodwill factionGoodwillData)
        {
            foreach (PKT_SettlementGoodwill _ in factionGoodwillData._settlements)
            {
                WO_Settlement settlement = (WO_Settlement)Find.WorldObjects.AllWorldObjects.First(fetch => fetch.Tile == _.Tile && fetch is WO_Settlement);
                if (settlement.Faction == Faction.OfPlayer) continue;
                else
                {
                    PM_Settlements.PlayerSettlements.Remove(settlement);
                    Find.WorldObjects.Remove(settlement);

                    WorldObjectDef def = DefDatabase<WorldObjectDef>.AllDefs.First(fetch => fetch.defName == "RTSettlement");
                    WO_Settlement newSettlement = (WO_Settlement)WorldObjectMaker.MakeWorldObject(def);
                    newSettlement.Tile = settlement.Tile;
                    newSettlement.Name = settlement.Name;
                    newSettlement.SetFaction(PlanetManagerHelper.GetPlayerFactionFromGoodwill(_.Goodwill));

                    PM_Settlements.PlayerSettlements.Add(newSettlement);
                    Find.WorldObjects.Add(newSettlement);
                }
            }
        }

        private static void ChangeSiteGoodwills(PKT_FactionGoodwill factionGoodwillData)
        {
            foreach (PKT_SiteGoodwill _ in factionGoodwillData._sites) 
            {
                WO_Site site = (WO_Site)Find.WorldObjects.AllWorldObjects.First(fetch => fetch.Tile == _.Tile && fetch is WO_Site);

                PM_Sites.RecalculateSiteGoodwill(site, _.Goodwill);
            }
        }
    }
}
