using GameClient.Dialogs;
using GameClient.Misc;
using RimWorld.Planet;
using Shared;
using Shared.Files.Maps;
using Shared.Files.Synchronous;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TCPNetwork.Packets;
using Verse;
using Verse.Noise;
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

                case SynchronousData.StepMode.Start:
                    StartSession();
                    break;
            }
        }

        public static void Ask(int tile)
        {
            RT_Dialog_Base.PushNewDialog(new RT_Dialog_Wait());

            PartyFile party = new PartyFile();
            party.Pawns = RimworldManager.GetCaravanPawnsIntoString(SessionHandler.ChosenCaravan, true);

            SynchronousData data = new SynchronousData();
            data._stepMode = SynchronousData.StepMode.Ask;
            data._toTile = tile;
            data._party = party;

            ClientNetwork.Instance.ClientListener.EnqueuePacket(PacketHeader.SynchronousManager, data);
        }

        private static void OnAsk(SynchronousData data)
        {
            RT_Dialog_Wait.Instance.Close();

            Action actionYes = delegate
            {
                RT_Dialog_Base.PushNewDialog(new RT_Dialog_Wait());

                Map map = Find.AnyPlayerHomeMap;
                MapManager.SendMapToServer(map);
                SessionHandler.SynchronousMap = map;

                PartyFile party = new PartyFile();
                party.Pawns = RimworldManager.GetMapPawnsIntoString(map, true);

                foreach (string str in data._party.Pawns)
                {
                    Pawn pawn = ScribeManager.SerializeFromString<Pawn>(str, ScribeManager.SerializableType.Pawn);
                    pawn.SetFactionDirect(SessionHandler.NeutralFaction);
                    RimworldManager.PlaceThingIntoMap(pawn, map);
                }

                SynchronousData _ = new SynchronousData();
                _._stepMode = SynchronousData.StepMode.Accept;
                _._fromTile = data._toTile;
                _._toTile = data._fromTile;
                _._party = party;

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

            Map map = MapSaveLoader.StringToMap(Serializer.ConvertBytesToObject<MapFile>(data._contents), true, true, false, false, false, false);
            CaravanEnterMapUtility.Enter(SessionHandler.ChosenCaravan, map, CaravanEnterMode.Edge, CaravanDropInventoryMode.DoNotDrop, draftColonists: false);
            CameraJumper.TryJump(map.Center, map, CameraJumper.MovementMode.Pan);

            foreach (string str in data._party.Pawns)
            {
                Pawn pawn = ScribeManager.SerializeFromString<Pawn>(str, ScribeManager.SerializableType.Pawn);
                pawn.SetFactionDirect(SessionHandler.NeutralFaction);
                RimworldManager.PlaceThingIntoMap(pawn, map);
            }

            SessionHandler.SynchronousMap = map;

            OnStart();
        }

        private static void OnReject(SynchronousData data)
        {
            RT_Dialog_Wait.Instance.Close();

            string[] description = new string[] { "Reject!" };
            RT_Dialog_Base.PushNewDialog(new RT_Dialog_Message(null, description));
        }

        private static void StartSession()
        {
            SessionHandler.IsSynchronousHost = true;
            MainThreadHandler.Instance.DoOnSynchronousStartMethods();

            RT_Dialog_Wait.Instance.Close();
        }

        private static void OnStart()
        {
            SynchronousData data = new SynchronousData();
            data._stepMode = SynchronousData.StepMode.Start;
            ClientNetwork.Instance.ClientListener.EnqueuePacket(PacketHeader.SynchronousManager, data);

            MainThreadHandler.Instance.DoOnSynchronousStartMethods();
        }

        private static void OnEnd()
        {
            MainThreadHandler.Instance.DoOnSynchronousEndMethods();
            SessionHandler.IsSynchronousHost = false;
        }
    }
}
