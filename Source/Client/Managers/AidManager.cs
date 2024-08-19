using RimWorld;
using Shared;
using System;
using System.Linq;
using Verse;
using static Shared.CommonEnumerators;

namespace GameClient
{
    public static class AidManager
    {
        public static void ParsePacket(Packet packet)
        {
            if (packet?.Contents == null)
            {
                Logger.Error("Received null packet or packet contents in ParsePacket.");
                return;
            }

            AidData data = Serializer.ConvertBytesToObject<AidData>(packet.Contents);

            switch (data?.StepMode)
            {
                case AidStepMode.Send:
                    // Handle sending aid request (currently empty)
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

                default:
                    Logger.Warning($"Unknown AidStepMode received: {data?.StepMode}");
                    break;
            }
        }

        private static void ReceiveAidRequest(AidData data)
        {
            if (data == null)
            {
                Logger.Error("Received null AidData in ReceiveAidRequest.");
                return;
            }

            Action acceptAid = () => AcceptAid(data);
            Action rejectAid = () => RejectAid(data);

            DialogManager.PushNewDialog(new RT_Dialog_YesNo("You are receiving aid, accept?", acceptAid, rejectAid));
        }

        public static void SendAidRequest()
        {
            if (ClientValues.chosenSettlement == null)
            {
                Logger.Error("ChosenSettlement is null, cannot send aid request.");
                return;
            }

            var aidData = new AidData
            {
                StepMode = AidStepMode.Send,
                FromTile = Find.AnyPlayerHomeMap.Tile,
                ToTile = ClientValues.chosenSettlement.Tile
            };

            var allPawns = RimworldManager.GetAllSettlementPawns(Faction.OfPlayer, false);
            if (DialogManager.dialogButtonListingResultInt < 0 || DialogManager.dialogButtonListingResultInt >= allPawns.Length)
            {
                Logger.Error("Invalid pawn selection for aid request.");
                return;
            }

            Pawn selectedPawn = allPawns[DialogManager.dialogButtonListingResultInt];
            aidData.HumanData = HumanScribeManager.HumanToString(selectedPawn);

            RimworldManager.RemovePawnFromGame(selectedPawn);

            var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.AidPacket), aidData);
            Network.Listener.EnqueuePacket(packet);

            DialogManager.PushNewDialog(new RT_Dialog_Wait("Waiting for server response"));
        }
        private static void OnAidAccept()
        {
            DialogManager.PopWaitDialog();

            RimworldManager.GenerateLetter("Sent Aid",
                "You have sent aid towards a settlement! The owner will receive the news soon.",
                LetterDefOf.PositiveEvent);

            SaveManager.ForceSave();
        }

        private static void OnAidReject(AidData data)
        {
            if (data == null)
            {
                Logger.Error("Received null AidData in OnAidReject.");
                return;
            }

            DialogManager.PopWaitDialog();

            var map = Find.WorldObjects.SettlementAt(data.FromTile)?.Map;
            if (map == null)
            {
                Logger.Error("Failed to retrieve map for aid rejection.");
                return;
            }

            var pawn = HumanScribeManager.StringToHuman(data.HumanData);
            if (pawn == null)
            {
                Logger.Error("Failed to deserialize human data from aid rejection.");
                return;
            }

            RimworldManager.PlaceThingIntoMap(pawn, map, ThingPlaceMode.Near, true);

            DialogManager.PushNewDialog(new RT_Dialog_Error("Player is not currently available!"));
        }

        private static void AcceptAid(AidData data)
        {
            if (data == null)
            {
                Logger.Error("Received null AidData in AcceptAid.");
                return;
            }

            var map = Find.WorldObjects.SettlementAt(data.ToTile)?.Map;
            if (map == null)
            {
                Logger.Error("Failed to retrieve map for aid acceptance.");
                return;
            }

            var pawn = HumanScribeManager.StringToHuman(data.HumanData);
            if (pawn == null)
            {
                Logger.Error("Failed to deserialize human data from aid acceptance.");
                return;
            }

            RimworldManager.PlaceThingIntoMap(pawn, map, ThingPlaceMode.Near, true);

            data.StepMode = AidStepMode.Accept;
            var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.AidPacket), data);
            Network.Listener.EnqueuePacket(packet);

            RimworldManager.GenerateLetter("Received Aid",
                "You have received aid from a player! The pawn should come to help soon.",
                LetterDefOf.PositiveEvent);

            SaveManager.ForceSave();
        }

        private static void RejectAid(AidData data)
        {
            if (data == null)
            {
                Logger.Error("Received null AidData in RejectAid.");
                return;
            }

            data.StepMode = AidStepMode.Reject;
            var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.AidPacket), data);
            Network.Listener.EnqueuePacket(packet);
        }
    }
}