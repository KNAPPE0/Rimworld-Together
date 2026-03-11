using GameClient.Dialogs;
using GameClient.Hooks.TCPNetwork;
using GameClient.Managers;
using GameClient.Misc;
using RimWorld;
using Shared;
using Shared.Files.Guilds;
using System;
using System.Collections.Generic;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using Verse;
using static Shared.CommonEnumerators;
using static Shared.Files.Guilds.GuildMember;

namespace GameClient.PacketManagers
{
    public class PM_Guilds : PM_Base
    {
        [HandlesPacket(PacketHeader.GuildManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_PlayerGuild data = Serializer.ConvertBytesToObject<PKT_PlayerGuild>(bytes);

            switch (data._stepMode)
            {
                case GuildStepMode.Create:
                    OnCreateFaction();
                    break;

                case GuildStepMode.Delete:
                    OnDeleteFaction();
                    break;

                case GuildStepMode.NameInUse:
                    OnFactionNameInUse();
                    break;

                case GuildStepMode.Invite:
                    OnFactionGetInvited(data);
                    break;

                case GuildStepMode.RemoveMember:
                    OnFactionGetKicked();
                    break;

                case GuildStepMode.AdminProtection:
                    OnFactionAdminProtection();
                    break;

                case GuildStepMode.MemberList:
                    OnFactionMemberList(data);
                    break;

                case GuildStepMode.Promote:
                    OnFactionPromote();
                    break;

                case GuildStepMode.Demote:
                    OnFactionDemote();
                    break;
            }
        }

        public static void OnFactionOpen()
        {
            Action r3 = delegate
            {
                DLG_Base.PushNewDialog(new DLG_Wait("Waiting for member list"));

                PKT_PlayerGuild playerFactionData = new PKT_PlayerGuild();
                playerFactionData._stepMode = GuildStepMode.MemberList;

                Network.ServerEndpoint.EnqueuePacket(PacketHeader.GuildManager, playerFactionData);
            };

            Action r2 = delegate
            {
                PKT_PlayerGuild playerFactionData = new PKT_PlayerGuild();
                playerFactionData._stepMode = GuildStepMode.RemoveMember;
                playerFactionData._dataInt = Find.AnyPlayerHomeMap.Tile;

                Network.ServerEndpoint.EnqueuePacket(PacketHeader.GuildManager, playerFactionData);
            };

            Action r1 = delegate
            {
                DLG_Base.PushNewDialog(new DLG_Wait("Waiting for guild deletion"));

                PKT_PlayerGuild playerFactionData = new PKT_PlayerGuild();
                playerFactionData._stepMode = GuildStepMode.Delete;

                Network.ServerEndpoint.EnqueuePacket(PacketHeader.GuildManager, playerFactionData);
            };

            DLG_YesNo d3 = new DLG_YesNo("Are you sure you want to LEAVE your guild?", r2, null);

            DLG_YesNo d2 = new DLG_YesNo("Are you sure you want to DELETE your guild?", r1, null);

            DLG_Buttons d1 = new DLG_Buttons("Guild Management", "Manage your guild from here",
                new string[] { "Members", "Delete", "Leave" },
                new Action[] { delegate { r3(); }, delegate { DLG_Base.PushNewDialog(d2); }, delegate { DLG_Base.PushNewDialog(d3); } },
                null);

            DLG_Base.PushNewDialog(d1);
        }

        public static void OnNoFactionOpen()
        {
            Action r2 = delegate
            {
                if (string.IsNullOrWhiteSpace(DLG_Inputs.DialogInputResults[0]) || DLG_Inputs.DialogInputResults[0].Length > 32)
                {
                    DLG_Base.PushNewDialog(new DLG_Message("ERROR", new string[] { "Guild name is invalid! Please try again!" }));
                }

                else
                {
                    DLG_Base.PushNewDialog(new DLG_Wait("Waiting for guild creation"));

                    PKT_PlayerGuild playerFactionData = new PKT_PlayerGuild();
                    playerFactionData._stepMode = GuildStepMode.Create;
                    playerFactionData._guild.Name = DLG_Inputs.DialogInputResults[0];

                    Network.ServerEndpoint.EnqueuePacket(PacketHeader.GuildManager, playerFactionData);
                }
            };
            DLG_Inputs d2 = new DLG_Inputs("New Guild Name", new string[] { "Input the name of your new guild" }, new bool[] { false }, r2);

            Action r1 = delegate { DLG_Base.PushNewDialog(d2); };
            DLG_YesNo d1 = new DLG_YesNo("You are not a member of any guild! Create one?", r1, null);

            DLG_Base.PushNewDialog(d1);
        }

        public static void OnFactionOpenOnMember()
        {
            Action r5 = delegate
            {
                PKT_PlayerGuild playerFactionData = new PKT_PlayerGuild();
                playerFactionData._stepMode = GuildStepMode.Demote;
                playerFactionData._dataInt = SessionHandler.ChosenSettlement.Tile;

                Network.ServerEndpoint.EnqueuePacket(PacketHeader.GuildManager, playerFactionData);
            };

            Action r4 = delegate
            {
                PKT_PlayerGuild playerFactionData = new PKT_PlayerGuild();
                playerFactionData._stepMode = GuildStepMode.Promote;
                playerFactionData._dataInt = SessionHandler.ChosenSettlement.Tile;

                Network.ServerEndpoint.EnqueuePacket(PacketHeader.GuildManager, playerFactionData);
            };

            Action r3 = delegate
            {
                PKT_PlayerGuild playerFactionData = new PKT_PlayerGuild();
                playerFactionData._stepMode = GuildStepMode.RemoveMember;
                playerFactionData._dataInt = SessionHandler.ChosenSettlement.Tile;

                Network.ServerEndpoint.EnqueuePacket(PacketHeader.GuildManager, playerFactionData);
            };

            DLG_YesNo d5 = new DLG_YesNo("Are you sure you want to demote this player?", r5);

            DLG_YesNo d4 = new DLG_YesNo("Are you sure you want to promote this player?", r4);

            DLG_YesNo d3 = new DLG_YesNo("Are you sure you want to kick this player?", r3);

            DLG_Buttons d2 = new DLG_Buttons("Power Management Menu", "Choose what you want to manage",
                new string[] { "Promote", "Demote" },
                new Action[] { delegate { DLG_Base.PushNewDialog(d4); }, delegate { DLG_Base.PushNewDialog(d5); } },
                delegate { DLG_Base.PushNewDialog(DLG_Base.PreviousDialog); });

            DLG_Buttons d1 = new DLG_Buttons("Management Menu", "Choose what you want to manage",
                new string[] { "Powers", "Kick" },
                new Action[] { delegate { DLG_Base.PushNewDialog(d2); }, delegate { DLG_Base.PushNewDialog(d3); } });

            DLG_Base.PushNewDialog(d1);
        }

        public static void OnFactionOpenOnNonMember()
        {
            Action r1 = delegate
            {
                PKT_PlayerGuild playerFactionData = new PKT_PlayerGuild();
                playerFactionData._stepMode = GuildStepMode.Invite;
                playerFactionData._dataInt = SessionHandler.ChosenSettlement.Tile;

                Network.ServerEndpoint.EnqueuePacket(PacketHeader.GuildManager, playerFactionData);
            };

            DLG_YesNo d1 = new DLG_YesNo("Do you want to invite this player to your guild?", r1, null);
            DLG_Base.PushNewDialog(d1);
        }

        private static void OnCreateFaction()
        {
            SessionHandler.HasFaction = true;

            string[] messages = new string[]
            {
                "Your guild has been created!",
                "You can now access its menu through the same button"
            };

            DLG_Wait.Instance.Close();
            DLG_Message d1 = new DLG_Message("MESSAGE", messages);
            DLG_Base.PushNewDialog(d1);
        }

        private static void OnDeleteFaction()
        {
            SessionHandler.HasFaction = false;

            if (!SessionHandler.IsInTransfer) DLG_Wait.Instance.Close();
            DLG_Base.PushNewDialog(new DLG_Message("MESSAGE", new string[] { "Your guild has been deleted!" }));
        }

        private static void OnFactionNameInUse()
        {
            DLG_Wait.Instance.Close();
            DLG_Base.PushNewDialog(new DLG_Message("ERROR", new string[] { "That guild name is already in use!" }));
        }

        private static void OnFactionGetInvited(PKT_PlayerGuild factionManifest)
        {
            Action r1 = delegate
            {
                SessionHandler.HasFaction = true;

                factionManifest._stepMode = GuildStepMode.AddMember;

                Network.ServerEndpoint.EnqueuePacket(PacketHeader.GuildManager, factionManifest);

                RimworldManager.GenerateLetter("Joined guild", "You have joined a guild!", LetterDefOf.PositiveEvent);
            };

            DLG_YesNo d1 = new DLG_YesNo($"Invited to {factionManifest._guild.Name}, accept?", r1, null);
            DLG_Base.PushNewDialog(d1);
        }

        private static void OnFactionGetKicked()
        {
            SessionHandler.HasFaction = false;

            DLG_Base.PushNewDialog(new DLG_Message("MESSAGE", new string[] { "You have been kicked from your guild!" }));
        }

        private static void OnFactionAdminProtection()
        {
            DLG_Base.PushNewDialog(new DLG_Message("ERROR", new string[] { "You can't do this action as a guild admin!" }));
        }

        private static void OnFactionPromote()
        {
            DLG_Base.PushNewDialog(new DLG_Message("MESSAGE", new string[] { "You have been promoted in your guild!" }));
        }

        private static void OnFactionDemote()
        {
            DLG_Base.PushNewDialog(new DLG_Message("MESSAGE", new string[] { "You have been demoted in your guild!" }));
        }

        private static void OnFactionMemberList(PKT_PlayerGuild factionManifest)
        {
            DLG_Wait.Instance.Close();

            List<string> toDisplay = new List<string>();

            for (int i = 0; i < factionManifest._guild.GuildMembers.Count; i++)
            {
                GuildMember member = factionManifest._guild.GuildMembers[i];

                toDisplay.Add($"{member.Username} - {(GuildRanks)member.Rank}");
            }

            DLG_Listing d1 = new DLG_Listing("Guild Members",
                "All guild members are depicted here", toDisplay.ToArray());

            DLG_Base.PushNewDialog(d1);
        }
    }
}
