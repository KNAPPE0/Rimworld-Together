using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using static Shared.CommonEnumerators;

namespace GameClient
{
    public static class FactionManager
    {
        public static void ParseFactionPacket(Packet packet)
        {
            PlayerFactionData? factionManifest = Serializer.ConvertBytesToObject<PlayerFactionData>(packet.Contents);

            if (factionManifest == null)
            {
                Logger.Error("Failed to parse faction packet: Data is null.");
                return;
            }

            switch (factionManifest.manifestMode)
            {
                case FactionManifestMode.Create:
                    OnCreateFaction();
                    break;

                case FactionManifestMode.Delete:
                    OnDeleteFaction();
                    break;

                case FactionManifestMode.NameInUse:
                    OnFactionNameInUse();
                    break;

                case FactionManifestMode.NoPower:
                    OnFactionNoPower();
                    break;

                case FactionManifestMode.AddMember:
                    OnFactionGetInvited(factionManifest);
                    break;

                case FactionManifestMode.RemoveMember:
                    OnFactionGetKicked();
                    break;

                case FactionManifestMode.AdminProtection:
                    OnFactionAdminProtection();
                    break;

                case FactionManifestMode.MemberList:
                    OnFactionMemberList(factionManifest);
                    break;

                default:
                    Logger.Warning($"Unknown faction manifest mode: {factionManifest.manifestMode}");
                    break;
            }
        }

        public static void OnFactionOpen()
        {
            Action r3 = () =>
            {
                DialogManager.PushNewDialog(new RT_Dialog_Wait("Waiting for member list"));

                var playerFactionData = new PlayerFactionData
                {
                    manifestMode = FactionManifestMode.MemberList
                };

                var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.FactionPacket), playerFactionData);
                Network.Listener.EnqueuePacket(packet);
            };

            Action r2 = () =>
            {
                var playerFactionData = new PlayerFactionData
                {
                    manifestMode = FactionManifestMode.RemoveMember,
                    manifestDataInt = ClientValues.chosenSettlement?.Tile ?? 0
                };

                var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.FactionPacket), playerFactionData);
                Network.Listener.EnqueuePacket(packet);
            };

            Action r1 = () =>
            {
                DialogManager.PushNewDialog(new RT_Dialog_Wait("Waiting for faction deletion"));

                var playerFactionData = new PlayerFactionData
                {
                    manifestMode = FactionManifestMode.Delete
                };

                var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.FactionPacket), playerFactionData);
                Network.Listener.EnqueuePacket(packet);
            };

            var d3 = new RT_Dialog_YesNo("Are you sure you want to LEAVE your faction?", r2, null);
            var d2 = new RT_Dialog_YesNo("Are you sure you want to DELETE your faction?", r1, null);
            var d1 = new RT_Dialog_3Button("Faction Management", "Manage your faction from here",
                "Members", "Delete", "Leave",
                () => r3(),
                () => DialogManager.PushNewDialog(d2),
                () => DialogManager.PushNewDialog(d3),
                null);

            DialogManager.PushNewDialog(d1);
        }

        public static void OnNoFactionOpen()
        {
            Action r2 = () =>
            {
                if (string.IsNullOrWhiteSpace(DialogManager.dialog1ResultOne) || DialogManager.dialog1ResultOne.Length > 32)
                {
                    DialogManager.PushNewDialog(new RT_Dialog_Error("Faction name is invalid! Please try again!"));
                }
                else
                {
                    DialogManager.PushNewDialog(new RT_Dialog_Wait("Waiting for faction creation"));

                    var playerFactionData = new PlayerFactionData
                    {
                        manifestMode = FactionManifestMode.Create,
                        manifestDataString = DialogManager.dialog1ResultOne
                    };

                    var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.FactionPacket), playerFactionData);
                    Network.Listener.EnqueuePacket(packet);
                }
            };

            var d2 = new RT_Dialog_1Input("New Faction Name", "Input the name of your new faction", r2, null);
            Action r1 = () => DialogManager.PushNewDialog(d2);
            var d1 = new RT_Dialog_YesNo("You are not a member of any faction! Create one?", r1, null);

            DialogManager.PushNewDialog(d1);
        }

        public static void OnFactionOpenOnMember()
        {
            Action r1 = () =>
            {
                var playerFactionData = new PlayerFactionData
                {
                    manifestMode = FactionManifestMode.Promote,
                    manifestDataInt = ClientValues.chosenSettlement?.Tile ?? 0
                };

                var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.FactionPacket), playerFactionData);
                Network.Listener.EnqueuePacket(packet);
            };

            Action r2 = () =>
            {
                var playerFactionData = new PlayerFactionData
                {
                    manifestMode = FactionManifestMode.Demote,
                    manifestDataInt = ClientValues.chosenSettlement?.Tile ?? 0
                };

                var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.FactionPacket), playerFactionData);
                Network.Listener.EnqueuePacket(packet);
            };

            Action r3 = () =>
            {
                var playerFactionData = new PlayerFactionData
                {
                    manifestMode = FactionManifestMode.RemoveMember,
                    manifestDataInt = ClientValues.chosenSettlement?.Tile ?? 0
                };

                var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.FactionPacket), playerFactionData);
                Network.Listener.EnqueuePacket(packet);
            };

            var d5 = new RT_Dialog_YesNo("Are you sure you want to demote this player?",
                r2,
                () => DialogManager.PushNewDialog(DialogManager.previousDialog));

            var d4 = new RT_Dialog_YesNo("Are you sure you want to promote this player?",
                r1,
                () => DialogManager.PushNewDialog(DialogManager.previousDialog));

            var d3 = new RT_Dialog_YesNo("Are you sure you want to kick this player?",
                r3,
                () => DialogManager.PushNewDialog(DialogManager.previousDialog));

            var d2 = new RT_Dialog_2Button("Power Management Menu", "Choose what you want to manage",
                "Promote", "Demote",
                () => DialogManager.PushNewDialog(d4),
                () => DialogManager.PushNewDialog(d5),
                null);

            var d1 = new RT_Dialog_2Button("Management Menu", "Choose what you want to manage",
                "Powers", "Kick",
                () => DialogManager.PushNewDialog(d2),
                () => DialogManager.PushNewDialog(d3),
                null);

            DialogManager.PushNewDialog(d1);
        }

        public static void OnFactionOpenOnNonMember()
        {
            Action r1 = () =>
            {
                var playerFactionData = new PlayerFactionData
                {
                    manifestMode = FactionManifestMode.AddMember,
                    manifestDataInt = ClientValues.chosenSettlement?.Tile ?? 0
                };

                var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.FactionPacket), playerFactionData);
                Network.Listener.EnqueuePacket(packet);
            };

            var d1 = new RT_Dialog_YesNo("Do you want to invite this player to your faction?", r1, null);
            DialogManager.PushNewDialog(d1);
        }

        private static void OnCreateFaction()
        {
            ServerValues.hasFaction = true;

            string[] messages = new string[]
            {
                "Your faction has been created!",
                "You can now access its menu through the same button"
            };

            DialogManager.PopWaitDialog();
            var d1 = new RT_Dialog_OK_Loop(messages);
            DialogManager.PushNewDialog(d1);
        }

        private static void OnDeleteFaction()
        {
            ServerValues.hasFaction = false;

            if (!ClientValues.isInTransfer) DialogManager.PopWaitDialog();
            DialogManager.PushNewDialog(new RT_Dialog_Error("Your faction has been deleted!"));
        }

        private static void OnFactionNameInUse()
        {
            DialogManager.PopWaitDialog();
            DialogManager.PushNewDialog(new RT_Dialog_Error("That faction name is already in use!"));
        }

        private static void OnFactionNoPower()
        {
            DialogManager.PopWaitDialog();
            DialogManager.PushNewDialog(new RT_Dialog_Error("You don't have enough power for this action!"));
        }

        private static void OnFactionGetInvited(PlayerFactionData factionManifest)
        {
            Action r1 = () =>
            {
                ServerValues.hasFaction = true;

                factionManifest.manifestMode = FactionManifestMode.AcceptInvite;

                var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.FactionPacket), factionManifest);
                Network.Listener.EnqueuePacket(packet);
            };

            var d1 = new RT_Dialog_YesNo($"Invited to {factionManifest.manifestDataString}, accept?", r1, null);
            DialogManager.PushNewDialog(d1);
        }

        private static void OnFactionGetKicked()
        {
            ServerValues.hasFaction = false;

            DialogManager.PushNewDialog(new RT_Dialog_OK("You have been kicked from your faction!"));
        }

        private static void OnFactionAdminProtection()
        {
            DialogManager.PushNewDialog(new RT_Dialog_Error("You can't do this action as a faction admin!"));
        }

        private static void OnFactionMemberList(PlayerFactionData factionManifest)
        {
            DialogManager.PopWaitDialog();

            List<string> unraveledDatas = new List<string>();
            for (int i = 0; i < factionManifest.manifestComplexData.Count(); i++)
            {
                unraveledDatas.Add($"{factionManifest.manifestComplexData[i]} " +
                    $"- {(FactionRanks)int.Parse(factionManifest.manifestSecondaryComplexData[i])}");
            }

            RT_Dialog_Listing d1 = new RT_Dialog_Listing("Faction Members", 
                "All faction members are depicted here", unraveledDatas.ToArray());

            DialogManager.PushNewDialog(d1);
        }
    }
}
