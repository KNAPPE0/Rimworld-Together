using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Shared;
using Verse;
using static Shared.CommonEnumerators;

namespace GameClient
{
    // Class that handles settlement and site player goodwills
    public static class GoodwillManager
    {
        // Tries to request a goodwill change depending on the values given
        public static void TryRequestGoodwill(Goodwill type, GoodwillTarget target)
        {
            int tileToUse = 0;
            Faction? factionToUse = null;

            if (target == GoodwillTarget.Settlement)
            {
                if (ClientValues.chosenSettlement != null)
                {
                    tileToUse = ClientValues.chosenSettlement.Tile;
                    factionToUse = ClientValues.chosenSettlement.Faction;
                }
            }
            else if (target == GoodwillTarget.Site)
            {
                if (ClientValues.chosenSite != null)
                {
                    tileToUse = ClientValues.chosenSite.Tile;
                    factionToUse = ClientValues.chosenSite.Faction;
                }
            }

            if (factionToUse == null)
            {
                Logger.Error("Faction is null. Cannot request goodwill change.");
                return;
            }

            switch (type)
            {
                case Goodwill.Enemy:
                    if (factionToUse == FactionValues.enemyPlayer)
                    {
                        DialogManager.PushNewDialog(new RT_Dialog_Error("Chosen settlement is already marked as enemy!"));
                    }
                    else
                    {
                        RequestChangeStructureGoodwill(tileToUse, Goodwill.Enemy);
                    }
                    break;

                case Goodwill.Neutral:
                    if (factionToUse == FactionValues.neutralPlayer)
                    {
                        DialogManager.PushNewDialog(new RT_Dialog_Error("Chosen settlement is already marked as neutral!"));
                    }
                    else
                    {
                        RequestChangeStructureGoodwill(tileToUse, Goodwill.Neutral);
                    }
                    break;

                case Goodwill.Ally:
                    if (factionToUse == FactionValues.allyPlayer)
                    {
                        DialogManager.PushNewDialog(new RT_Dialog_Error("Chosen settlement is already marked as ally!"));
                    }
                    else
                    {
                        RequestChangeStructureGoodwill(tileToUse, Goodwill.Ally);
                    }
                    break;

                default:
                    Logger.Warning($"Unknown Goodwill type: {type}");
                    break;
            }
        }

        // Requests a structure goodwill change to the server
        public static void RequestChangeStructureGoodwill(int structureTile, Goodwill goodwill)
        {
            var factionGoodwillData = new FactionGoodwillData
            {
                Tile = structureTile,
                Goodwill = goodwill
            };

            var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.GoodwillPacket), factionGoodwillData);
            Network.Listener.EnqueuePacket(packet);

            DialogManager.PushNewDialog(new RT_Dialog_Wait("Changing settlement goodwill"));
        }

        // Changes a structure goodwill from a packet
        public static void ChangeStructureGoodwill(Packet packet)
        {
            var factionGoodwillData = Serializer.ConvertBytesToObject<FactionGoodwillData>(packet.Contents);
            if (factionGoodwillData == null)
            {
                Logger.Error("Failed to deserialize FactionGoodwillData from packet.");
                return;
            }

            ChangeSettlementGoodwills(factionGoodwillData);
            ChangeSiteGoodwills(factionGoodwillData);
        }

        // Changes a settlement goodwill from a request
        private static void ChangeSettlementGoodwills(FactionGoodwillData factionGoodwillData)
        {
            var settlementsToChange = factionGoodwillData.SettlementTiles
                .Select(tile => Find.WorldObjects.Settlements.Find(x => x.Tile == tile))
                .Where(settlement => settlement != null)
                .ToList();

            for (int i = 0; i < settlementsToChange.Count; i++)
            {
                var oldSettlement = settlementsToChange[i];
                if (oldSettlement == null) continue;

                PlayerSettlementManager.PlayerSettlements.Remove(oldSettlement);
                Find.WorldObjects.Remove(oldSettlement);

                var newSettlement = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
                newSettlement.Tile = oldSettlement.Tile;
                newSettlement.Name = oldSettlement.Name;
                newSettlement.SetFaction(PlanetManagerHelper.GetPlayerFactionFromGoodwill(factionGoodwillData.SettlementGoodwills[i]));

                PlayerSettlementManager.PlayerSettlements.Add(newSettlement);
                Find.WorldObjects.Add(newSettlement);
            }
        }

        // Changes a site goodwill from a request
        private static void ChangeSiteGoodwills(FactionGoodwillData factionGoodwillData)
        {
            var sitesToChange = factionGoodwillData.SiteTiles
                .Select(tile => Find.WorldObjects.Sites.Find(x => x.Tile == tile))
                .Where(site => site != null)
                .ToList();

            for (int i = 0; i < sitesToChange.Count; i++)
            {
                var oldSite = sitesToChange[i];
                if (oldSite == null) continue;

                PlayerSiteManager.playerSites.Remove(oldSite);
                Find.WorldObjects.Remove(oldSite);

                var newSite = SiteMaker.MakeSite(
                    sitePart: oldSite.MainSitePartDef,
                    tile: oldSite.Tile,
                    threatPoints: 1000,
                    faction: PlanetManagerHelper.GetPlayerFactionFromGoodwill(factionGoodwillData.SiteGoodwills[i]));

                PlayerSiteManager.playerSites.Add(newSite);
                Find.WorldObjects.Add(newSite);
            }
        }
    }
}