using GameClient.Dialogs;
using GameClient.Hooks.TCPNetwork;
using GameClient.Managers;
using GameClient.Misc;
using RimWorld;
using RimWorld.Planet;
using Shared;
using Shared.Files;
using Shared.Files.Synchronous;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using Verse;
using Verse.Noise;
using static Shared.CommonEnumerators;

namespace GameClient.Hooks.Synchronous
{
    public class PM_Synchronous : PM_Base
    {
        private enum SynchronousSide { Host, Guest }

        [HandlesPacket(PacketHeader.SynchronousManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_Synchronous data = Serializer.ConvertBytesToObject<PKT_Synchronous>(bytes);

            switch (data._stepMode)
            {
                case PKT_Synchronous.StepMode.Ask:
                    OnAsk(data);
                    break;

                case PKT_Synchronous.StepMode.Accept:
                    OnAccept(data);
                    break;

                case PKT_Synchronous.StepMode.Reject:
                    OnReject(data);
                    break;

                case PKT_Synchronous.StepMode.Start:
                    StartSession(SynchronousSide.Host);
                    break;
            }
        }

        public static void Ask(int tile, PKT_Synchronous.Type type)
        {
            DLG_Base.PushNewDialog(new DLG_Wait());

            PKT_Synchronous data = new PKT_Synchronous();
            data._stepMode = PKT_Synchronous.StepMode.Ask;
            data._toTile = tile;
            data._type = type;
            data._party = GetPawnParty(SynchronousSide.Guest);

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.SynchronousManager, data);
        }

        private static void OnAsk(PKT_Synchronous data)
        {
            DLG_Wait.Instance.Close();

            Action actionYes = delegate
            {
                DLG_Base.PushNewDialog(new DLG_Wait());

                SetMap(SynchronousSide.Host, null);

                MapManager.SendMapToServer(SessionHandler.SynchronousMap);

                PKT_Synchronous _ = new PKT_Synchronous();
                _._stepMode = PKT_Synchronous.StepMode.Accept;
                _._type = data._type;
                _._fromTile = data._toTile;
                _._toTile = data._fromTile;
                _._party = GetPawnParty(SynchronousSide.Host);

                SpawnOtherPawns(SynchronousSide.Host, data);

                Network.ServerEndpoint.EnqueuePacket(PacketHeader.SynchronousManager, _);
            };

            Action actionNo = delegate
            {
                PKT_Synchronous _ = new PKT_Synchronous();
                _._stepMode = PKT_Synchronous.StepMode.Reject;
                _._fromTile = data._toTile;
                _._toTile = data._fromTile;

                Network.ServerEndpoint.EnqueuePacket(PacketHeader.SynchronousManager, _);
            };

            string description = $"Player '{data._username}' wants to interact, accept?";
            DLG_Base.PushNewDialog(new DLG_YesNo(description, actionYes, actionNo));
        }

        private static void OnAccept(PKT_Synchronous data)
        {
            DLG_Wait.Instance.Close();

            SetMap(SynchronousSide.Guest, data);

            EnterMap(SynchronousSide.Guest);

            SpawnOtherPawns(SynchronousSide.Guest, data);

            StartSession(SynchronousSide.Guest);
        }

        private static void OnReject(PKT_Synchronous data)
        {
            DLG_Wait.Instance.Close();

            string[] description = new string[] { "Interaction was rejected by the player!" };
            DLG_Base.PushNewDialog(new DLG_Message(null, description));
        }

        private static void StartSession(SynchronousSide side)
        {
            if (side == SynchronousSide.Host)
            {
                SessionHandler.IsSynchronousHost = true;
                MainThreadHandler.Instance.DoOnSynchronousStartMethods();
                DLG_Wait.Instance.Close();

                string[] description = new string[] { "Game will be unable to save while in synchronous!" };
                DLG_Base.PushNewDialog(new DLG_Message("MESSAGE", description));
            }

            else
            {
                PKT_Synchronous data = new PKT_Synchronous();
                data._stepMode = PKT_Synchronous.StepMode.Start;
                Network.ServerEndpoint.EnqueuePacket(PacketHeader.SynchronousManager, data);

                MainThreadHandler.Instance.DoOnSynchronousStartMethods();

                string[] description = new string[] { "Game will be unable to save while in synchronous!" };
                DLG_Base.PushNewDialog(new DLG_Message("MESSAGE", description));
            }
        }

        private static void EndSession()
        {
            MainThreadHandler.Instance.DoOnSynchronousEndMethods();
            SessionHandler.IsSynchronousHost = false;
        }

        private static void SetMap(SynchronousSide side, PKT_Synchronous data)
        {
            if (side == SynchronousSide.Host) SessionHandler.SynchronousMap = Find.AnyPlayerHomeMap;
            else
            {
                MapFile file = Serializer.ConvertBytesToObject<MapFile>(data._contents);
                SessionHandler.SynchronousMap = MapSaveLoader.StringToMap(file, true);
            }
        }

        private static void EnterMap(SynchronousSide side)
        {
            if (side == SynchronousSide.Host)
            {
                CameraJumper.TryJump(SessionHandler.SynchronousMap.Center, SessionHandler.SynchronousMap, CameraJumper.MovementMode.Pan);
            }

            else
            {
                CaravanEnterMapUtility.Enter(SessionHandler.ChosenCaravan, SessionHandler.SynchronousMap, CaravanEnterMode.Edge,
                    CaravanDropInventoryMode.DoNotDrop, draftColonists: false);

                CameraJumper.TryJump(SessionHandler.SynchronousMap.Center, SessionHandler.SynchronousMap, CameraJumper.MovementMode.Pan);
            }
        }

        private static void SpawnOtherPawns(SynchronousSide side, PKT_Synchronous data)
        {
            foreach (string str in data._party.Pawns)
            {
                Pawn pawn = ScribeManager.SerializeFromString<Pawn>(str, ScribeManager.SerializableType.Pawn, true);

                RimworldManager.PlaceThingIntoMap(pawn, SessionHandler.SynchronousMap, 
                    side == SynchronousSide.Host ? SessionHandler.SynchronousMap.Center : pawn.PositionHeld);

                if (data._type == PKT_Synchronous.Type.Visit) pawn.SetFactionDirect(SessionHandler.NeutralFaction);
                else pawn.SetFactionDirect(SessionHandler.EnemyFaction);
            }
        }

        private static PartyFile GetPawnParty(SynchronousSide side)
        {
            PartyFile file = new PartyFile();

            if (side == SynchronousSide.Host) file.Pawns = RimworldManager.GetMapPawnsIntoString(SessionHandler.SynchronousMap, true, true);
            else file.Pawns = RimworldManager.GetCaravanPawnsIntoString(SessionHandler.ChosenCaravan, true);

            return file;
        }
    }
}
