using GameClient.Dialogs;
using GameClient.Misc;
using RimWorld.Planet;
using Shared;
using Shared.Files.Maps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TCPNetwork.Packets;
using Verse;
using static Shared.CommonEnumerators;

namespace GameClient.Managers
{
    public static class SynchronousManager
    {
        [HandlesPacket(PacketHeader.SynchronousManager)]
        private static void ParsePacket(byte[] bytes)
        {
            SynchronousData data = Serializer.ConvertBytesToObject<SynchronousData>(bytes);

            switch (data._stepMode)
            {
                case SynchronousData.StepMode.Ask:
                    OnAsk(data);
                    break;

                case SynchronousData.StepMode.Accept:
                    OnAccept(data);
                    break;

                case SynchronousData.StepMode.Reject:
                    OnReject(data);
                    break;
            }
        }

        public static void Ask(int tile)
        {
            RT_Dialog_Base.PushNewDialog(new RT_Dialog_Wait());

            SynchronousData data = new SynchronousData();
            data._stepMode = SynchronousData.StepMode.Ask;
            data._toTile = tile;

            ClientNetwork.Instance.ClientListener.EnqueuePacket(PacketHeader.SynchronousManager, data);
        }

        private static void OnAsk(SynchronousData data)
        {
            RT_Dialog_Wait.Instance.Close();

            Action actionYes = delegate
            {
                MapFile map = MapSaveLoader.MapToString(Finder.GetMapFromID(data._toTile), true, true, true, true, true, true);

                SynchronousData _ = new SynchronousData();
                _._stepMode = SynchronousData.StepMode.Accept;
                _._fromTile = data._toTile;
                _._toTile = data._fromTile;
                _._rawData = Serializer.ConvertObjectToBytes(map);

                ClientNetwork.Instance.ClientListener.EnqueuePacket(PacketHeader.SynchronousManager, _);
            };

            Action actionNo = delegate
            {
                SynchronousData _ = new SynchronousData();
                _._stepMode = SynchronousData.StepMode.Reject;
                _._fromTile = data._toTile;
                _._toTile = data._fromTile;

                ClientNetwork.Instance.ClientListener.EnqueuePacket(PacketHeader.SynchronousManager, _);
            };

            string description = $"Player {data._fromTile} wants to synchronize, accept?";
            RT_Dialog_Base.PushNewDialog(new RT_Dialog_YesNo(description, actionYes, actionNo));
        }

        private static void OnAccept(SynchronousData data)
        {
            RT_Dialog_Wait.Instance.Close();

            Map map = MapSaveLoader.StringToMap(Serializer.ConvertBytesToObject<MapFile>(data._rawData), true, true, true, true, true, true);
            CaravanEnterMapUtility.Enter(SessionHandler.ChosenCaravan, map, CaravanEnterMode.Edge, CaravanDropInventoryMode.DoNotDrop, draftColonists: true);
            CameraJumper.TryJump(map.Center, map, CameraJumper.MovementMode.Pan);
        }

        private static void OnReject(SynchronousData data)
        {
            RT_Dialog_Wait.Instance.Close();

            string[] description = new string[] { "Reject!" };
            RT_Dialog_Base.PushNewDialog(new RT_Dialog_Message(null, description));
        }
    }
}
