using GameClient.Dialogs;
using GameClient.Misc;
using RimWorld;
using Shared;
using System;
using Verse;
using static Shared.CommonEnumerators;
using TCPNetwork.Packets;
using GameClient.Hooks.TCPNetwork;
using TCPNetwork;
using GameClient.Managers;
using TCPNetwork.Files.Client;

namespace GameClient.PacketManagers
{
    public class PM_Aids : PM_Base
    {
        [HandlesPacket(PacketHeader.AidManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            AidData data = Serializer.ConvertBytesToObject<AidData>(bytes);

            switch (data._stepMode)
            {
                case AidStepMode.Send:
                    //Empty
                    break;

                case AidStepMode.Receive:
                    ReceiveAidRequest(data);
                    break;

                case AidStepMode.Accept:
                    OnAidAccept();
                    break;

                case AidStepMode.Reject:
                    OnAidReject(data);
                    break;
            }
        }

        private static void ReceiveAidRequest(AidData data)
        {
            Action toDoYes = delegate { AcceptAid(data); };
            Action toDoNo = delegate { RejectAid(data); };

            DLG_Base.PushNewDialog(new DLG_YesNo("You are receiving aid, accept?", toDoYes, toDoNo));
        }

        public static void SendAidRequest()
        {
            AidData aidData = new AidData();
            aidData._stepMode = AidStepMode.Send;
            aidData._fromTile = Find.AnyPlayerHomeMap.Tile;
            aidData._toTile = SessionHandler.ChosenSettlement.Tile;

            Pawn toGet = RimworldManager.GetAllSettlementsPawns(Faction.OfPlayer, false)[DLG_ListingWithButton.DialogButtonListingResultInt];
            aidData._humanData = ScribeManager.SerializeToString(toGet, ScribeManager.SerializableType.Thing);
            RimworldManager.RemovePawnFromGame(toGet);

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.AidManager, aidData);

            DLG_Base.PushNewDialog(new DLG_Wait("Waiting for server response"));
        }

        private static void OnAidAccept()
        {
            DLG_Wait.Instance.Close();

            RimworldManager.GenerateLetter("Sent aid",
                "You have sent aid towards a settlement! The owner will receive the news soon",
                LetterDefOf.PositiveEvent);

            PM_Saves.ForceSave();
        }

        private static void OnAidReject(AidData data)
        {
            DLG_Wait.Instance.Close();

            Map map = Find.World.worldObjects.SettlementAt(data._fromTile).Map;

            Pawn pawn = ScribeManager.SerializeFromString<Pawn>(data._humanData);
            pawn.SetFactionDirect(Faction.OfPlayer);

            RimworldManager.PlaceThingIntoMap(pawn, map, map.Center);

            DLG_Base.PushNewDialog(new DLG_Message("ERROR", new string[] { "Player is not currently available!" }));
        }

        private static void AcceptAid(AidData data)
        {
            Map map = Find.World.worldObjects.SettlementAt(data._toTile).Map;

            Pawn pawn = ScribeManager.SerializeFromString<Pawn>(data._humanData);
            pawn.SetFactionDirect(Faction.OfPlayer);

            RimworldManager.PlaceThingIntoMap(pawn, map, map.Center, true);

            data._stepMode = AidStepMode.Accept;
            Network.ServerEndpoint.EnqueuePacket(PacketHeader.AidManager, data);

            RimworldManager.GenerateLetter("Received aid",
                "You have received aid from a player! The pawn should come to help soon",
                LetterDefOf.PositiveEvent);

            PM_Saves.ForceSave();
        }

        private static void RejectAid(AidData data)
        {
            data._stepMode = AidStepMode.Reject;
            Network.ServerEndpoint.EnqueuePacket(PacketHeader.AidManager, data);
        }
    }
}
