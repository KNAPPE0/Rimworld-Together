using GameClient;
using RimWorld;
using RimWorld.Planet;
using Shared;
using System.Collections.Generic;
using System.Linq;
using Verse;
using static Shared.CommonEnumerators;

namespace GameClient
{
    public static class CaravanManager
    {
        //Variables

        public static WorldObjectDef onlineCaravanDef;
        public static List<CaravanDetails> activeCaravans = new List<CaravanDetails>();
        public static Dictionary<Caravan, int> activePlayerCaravans = new Dictionary<Caravan, int>();

        public static void ParsePacket(Packet packet)
        {
            CaravanData data = Serializer.ConvertBytesToObject<CaravanData>(packet.Contents);

            switch (data.StepMode)
            {
                case CaravanStepMode.Add:
                    AddCaravan(data.Details);
                    break;

                case CaravanStepMode.Remove:
                    RemoveCaravan(data.Details);
                    break;

                case CaravanStepMode.Move:
                    MoveCaravan(data.Details);
                    break;
            }
        }

        public static void AddCaravans(CaravanDetails[] Details)
        {
            if (Details == null) return;

            foreach (CaravanDetails caravan in Details)
            {
                AddCaravan(caravan);
            }
        }

        private static void AddCaravan(CaravanDetails Details)
        {
            activeCaravans.Add(Details);

            if (Details.Owner == ClientValues.Username)
            {
                Caravan toAdd = Find.WorldObjects.Caravans.FirstOrDefault(fetch => fetch.Faction == Faction.OfPlayer && 
                    !activePlayerCaravans.ContainsKey(fetch));

                if (toAdd == null) return;
                else activePlayerCaravans.Add(toAdd, Details.Id);
            }

            else
            {
                OnlineCaravan onlineCaravan = (OnlineCaravan)WorldObjectMaker.MakeWorldObject(onlineCaravanDef);
                onlineCaravan.Tile = Details.Tile;
                onlineCaravan.SetFaction(FactionValues.neutralPlayer);
                Find.World.worldObjects.Add(onlineCaravan);
            }
        }

        private static void RemoveCaravan(CaravanDetails Details)
        {
            CaravanDetails toRemove = CaravanManagerHelper.GetCaravanDetailsFromID(Details.Id);
            if (toRemove == null) return;
            else
            {
                activeCaravans.Remove(toRemove);

                if (Details.Owner == ClientValues.Username)
                {
                    foreach (KeyValuePair<Caravan, int> pair in activePlayerCaravans.ToArray())
                    {
                        if (pair.Value == Details.Id)
                        {
                            activePlayerCaravans.Remove(pair.Key);
                            break;
                        }
                    }
                }

                else
                {
                    WorldObject worldObject = Find.World.worldObjects.AllWorldObjects.First(fetch => fetch.Tile == Details.Tile 
                        && fetch.def == onlineCaravanDef);

                    Find.World.worldObjects.Remove(worldObject);
                }
            }
        }

        private static void MoveCaravan(CaravanDetails Details)
        {
            CaravanDetails toMove = CaravanManagerHelper.GetCaravanDetailsFromID(Details.Id);
            if (toMove == null) return;
            else
            {
                if (Details.Owner == ClientValues.Username) return;
                else
                {
                    RemoveCaravan(toMove);
                    AddCaravan(Details);
                }
            }
        }

        public static void RequestCaravanAdd(Caravan caravan)
        {
            CaravanData data = new CaravanData();
            data.StepMode = CaravanStepMode.Add;
            data.Details = new CaravanDetails();
            data.Details.Tile = caravan.Tile;
            data.Details.Owner = ClientValues.Username;

            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.CaravanPacket), data);
            Network.Listener.EnqueuePacket(packet);
        }

        public static void RequestCaravanRemove(Caravan caravan)
        {
            activePlayerCaravans.TryGetValue(caravan, out int caravanID);

            CaravanDetails Details = CaravanManagerHelper.GetCaravanDetailsFromID(caravanID);
            if (Details == null) return;
            else
            {
                CaravanData data = new CaravanData();
                data.StepMode = CaravanStepMode.Remove;
                data.Details = Details;

                Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.CaravanPacket), data);
                Network.Listener.EnqueuePacket(packet);
            }
        }

        public static void RequestCaravanMove(Caravan caravan)
        {
            activePlayerCaravans.TryGetValue(caravan, out int caravanID);

            CaravanDetails Details = CaravanManagerHelper.GetCaravanDetailsFromID(caravanID);
            if (Details == null) return;
            else
            {
                CaravanData data = new CaravanData();
                data.StepMode = CaravanStepMode.Move;
                data.Details = Details;

                Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.CaravanPacket), data);
                Network.Listener.EnqueuePacket(packet);
            }
        }

        public static void ClearAllCaravans()
        {            
            activeCaravans.Clear();
            activePlayerCaravans.Clear();

            foreach (WorldObject worldObject in Find.World.worldObjects.AllWorldObjects.ToArray())
            {
                if (worldObject.def == onlineCaravanDef)
                {
                    Find.World.worldObjects.Remove(worldObject);
                }
            }
        }

        public static void ModifyDetailsTile(Caravan caravan, int updatedTile)
        {
            activePlayerCaravans.TryGetValue(caravan, out int caravanID);

            foreach (CaravanDetails Details in activeCaravans)
            {
                if (Details.Id == caravanID)
                {
                    Details.Tile = updatedTile;
                    break;
                }
            }
        }
    }
}

public static class CaravanManagerHelper
{
    //Variables

    public static CaravanDetails[] tempCaravanDetails;

    public static void SetValues(ServerGlobalData serverGlobalData)
    {
        tempCaravanDetails = serverGlobalData.PlayerCaravans;
    }

    public static CaravanDetails GetCaravanDetailsFromID(int id)
    {
        return CaravanManager.activeCaravans.FirstOrDefault(fetch => fetch.Id == id);
    }

    public static void SetCaravanDefs()
    {
        CaravanManager.onlineCaravanDef = DefDatabase<WorldObjectDef>.AllDefs.First(fetch => fetch.defName == "RTCaravan");
    }
}
