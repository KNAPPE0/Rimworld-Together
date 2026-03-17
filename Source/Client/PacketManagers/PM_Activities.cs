using GameClient.Dialogs;
using GameClient.Misc;
using GameClient.Patches;
using GameClient.WorldObjects;
using TCPNetwork.Packets;
using RimWorld;
using RimWorld.Planet;
using Shared;
using System.Reflection;
using Verse;
using static Shared.CommonEnumerators;
using Shared.Files.Maps;
using GameClient.Hooks.TCPNetwork;
using TCPNetwork;
using GameClient.Managers;
using TCPNetwork.Files.Client;

namespace GameClient.PacketManagers
{
    public class PM_Activities : PM_Base
    {
        [HandlesPacket(PacketHeader.ActivityManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_Activity data = Serializer.ConvertBytesToObject<PKT_Activity>(bytes);

            switch (data._stepMode)
            {
                case ActivityStepMode.Request:
                    OnAccept(data);
                    break;

                case ActivityStepMode.Deny:
                    OnDeny();
                    break;
            }
        }

        public static void RequestActivity(ActivityType type, int targetTile)
        {
            if (!SessionHandler.CurrentActionValues.ActivityAction.IsEnabled)
            {
                DLG_Base.PushNewDialog(new DLG_Message("ERROR", new string[] { "This feature has been disabled in this server!" }));
                return;
            }

            SessionHandler.latestActivity = type;

            SendRequest(targetTile);
        }

        private static void SendRequest(int targetTile)
        {
            DLG_Base.PushNewDialog(new DLG_Wait("Waiting for map"));

            PKT_Activity data = new PKT_Activity();
            data._stepMode = ActivityStepMode.Request;
            data._targetTile = targetTile;

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.ActivityManager, data);
        }

        private static void OnAccept(PKT_Activity data) 
        {
            DLG_Wait.Instance.Close();

            PrepareMap(Serializer.ConvertBytesToObject<MapFile>(data._mapRawData)); 
        }

        private static void OnDeny()
        {
            DLG_Wait.Instance.Close();

            DLG_Base.PushNewDialog(new DLG_Message("ERROR", new string[] { "This map is currently unavailable!" }));
        }

        private static void PrepareMap(MapFile mapFile)
        {
            Map map = null;
            if (SessionHandler.latestActivity == ActivityType.Raid) map = MapSaveLoader.StringToMap(mapFile);
            else if (SessionHandler.latestActivity == ActivityType.Zoom) map = MapSaveLoader.StringToMap(mapFile);

            Faction faction;
            if (SessionHandler.latestActivity == ActivityType.Raid) faction = SessionHandler.EnemyFaction;
            else faction = SessionHandler.NeutralFaction;

            RimworldManager.SetMapFactions(map, faction);

            RimworldManager.SetMapLord(map, faction);

            if (SessionHandler.latestActivity == ActivityType.Raid)
            {
                CaravanEnterMapUtility.Enter(SessionHandler.ChosenCaravan, SessionHandler.ChosenSettlement.Map, 
                    CaravanEnterMode.Edge, CaravanDropInventoryMode.DoNotDrop, draftColonists: true);

                CameraJumper.TryJump(map.Center, map, CameraJumper.MovementMode.Pan);
            }

            else if (SessionHandler.latestActivity == ActivityType.Zoom)
            {
                CameraJumper.TryJump(map.Center, map, CameraJumper.MovementMode.Pan);
            }
        }
    }
}
