using GameServer.Integrations.Discord;
using GameServer.Managers;
using Shared;
using Shared.Files.Economy;
using Shared.Misc;
using System.Linq;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;
using static Shared.Misc.Printer;

namespace GameServer.PacketManager
{
    /// <summary>
    /// Server-side quest board packet handler.
    /// Snapshots are broadcast back to all connected clients on every state change
    /// so quest boards stay in sync without polling.
    /// </summary>
    public class PM_Quest : PM_Base
    {
        [HandlesPacket(PacketHeader.QuestManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_Quest data = Serializer.ConvertBytesToObject<PKT_Quest>(bytes);
            if (data == null) return;

            switch (data.CurrentStep)
            {
                case PKT_Quest.StepMode.RequestBoard: SendSnapshot(client); break;
                case PKT_Quest.StepMode.Post:
                    if (!EconomyAccessGuard.IsAllowed(client))
                    {
                        Reply(client, Result(EconomyAccessGuard.DenyReason, 0));
                        return;
                    }
                    HandlePost(client, data);
                    break;
                case PKT_Quest.StepMode.Claim:
                    if (!EconomyAccessGuard.IsAllowed(client))
                    {
                        Reply(client, Result(EconomyAccessGuard.DenyReason, 0));
                        return;
                    }
                    HandleClaim(client, data);
                    break;
                case PKT_Quest.StepMode.Abandon: HandleAbandon(client, data); break;
                case PKT_Quest.StepMode.SubmitDelivery: HandleSubmitDelivery(client, data); break;
                case PKT_Quest.StepMode.ConfirmCompletion: HandleConfirmBounty(client, data); break;
                case PKT_Quest.StepMode.Cancel: HandleCancel(client, data); break;
            }
        }

        // -- snapshot fan-out --

        public static void SendSnapshot(ServerClient client)
        {
            QuestBoard b = QuestManager.Snapshot();
            string viewerGuild = client?.UserFile?.GuildName ?? string.Empty;
            string viewerUsername = client?.UserFile?.Username ?? string.Empty;

            Reply(client, new PKT_Quest
            {
                CurrentStep = PKT_Quest.StepMode.BoardSnapshot,
                Board = FilterForViewer(b.Quests, viewerUsername, viewerGuild),
                LifetimeQuestsPosted = b.LifetimeQuestsPosted,
                LifetimeQuestsCompleted = b.LifetimeQuestsCompleted,
                LifetimeBountySilverPaid = b.LifetimeBountySilverPaid
            });
        }

        public static void BroadcastSnapshot()
        {
            // KMH: Per-viewer filtered snapshots so guild-only quests stay
            // hidden from non-members. Cheap because the source list is small.
            QuestBoard b = QuestManager.Snapshot();

            foreach (ServerClient sc in GameServer.Hooks.TCPNetwork.ServerNetwork.GetConnectedClients())
            {
                if (sc?.UserFile == null) continue;
                PKT_Quest snap = new PKT_Quest
                {
                    CurrentStep = PKT_Quest.StepMode.BoardSnapshot,
                    Board = FilterForViewer(b.Quests, sc.UserFile.Username, sc.UserFile.GuildName ?? string.Empty),
                    LifetimeQuestsPosted = b.LifetimeQuestsPosted,
                    LifetimeQuestsCompleted = b.LifetimeQuestsCompleted,
                    LifetimeBountySilverPaid = b.LifetimeBountySilverPaid
                };
                sc.Listener.EnqueuePacket(PacketHeader.QuestManager, snap);
            }
        }

        private static System.Collections.Generic.List<QuestFile> FilterForViewer(
            System.Collections.Generic.List<QuestFile> quests, string viewerUsername, string viewerGuild)
        {
            if (quests == null || quests.Count == 0) return new System.Collections.Generic.List<QuestFile>();
            var result = new System.Collections.Generic.List<QuestFile>(quests.Count);
            foreach (QuestFile q in quests)
            {
                if (q == null) continue;
                // The poster always sees their own quest, regardless of visibility.
                if (string.Equals(q.PosterUsername, viewerUsername, System.StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(q);
                    continue;
                }
                if (q.IsVisibleTo(viewerUsername, viewerGuild))
                    result.Add(q);
            }
            return result;
        }

        // -- handlers --

        private static void HandlePost(ServerClient client, PKT_Quest data)
        {
            string username = client.UserFile?.Username;
            if (string.IsNullOrEmpty(username)) return;

            var (ok, note, quest) = QuestManager.PostQuest(username, data.Quest);
            Reply(client, Result(note, quest?.Id ?? 0));

            if (ok && quest != null)
            {
                DiscordAnnouncer.QuestPosted(quest);
                BroadcastSnapshot();
            }
        }

        private static void HandleClaim(ServerClient client, PKT_Quest data)
        {
            string username = client.UserFile?.Username;
            if (string.IsNullOrEmpty(username)) return;

            var (ok, note) = QuestManager.Claim(username, data.QuestId);
            Reply(client, Result(note, data.QuestId));
            if (ok) BroadcastSnapshot();
        }

        private static void HandleAbandon(ServerClient client, PKT_Quest data)
        {
            string username = client.UserFile?.Username;
            if (string.IsNullOrEmpty(username)) return;

            var (ok, note) = QuestManager.Abandon(username, data.QuestId);
            Reply(client, Result(note, data.QuestId));
            if (ok) BroadcastSnapshot();
        }

        private static void HandleSubmitDelivery(ServerClient client, PKT_Quest data)
        {
            string username = client.UserFile?.Username;
            if (string.IsNullOrEmpty(username)) return;

            var (ok, note) = QuestManager.SubmitDelivery(username, data.QuestId);
            Reply(client, Result(note, data.QuestId));
            if (ok)
            {
                Shared.Files.Economy.QuestFile q = QuestManager.Snapshot().Quests
                    .FirstOrDefault(x => x.Id == data.QuestId);
                if (q != null) DiscordAnnouncer.QuestCompleted(q, username);
                BroadcastSnapshot();
            }
        }

        private static void HandleConfirmBounty(ServerClient client, PKT_Quest data)
        {
            string username = client.UserFile?.Username;
            if (string.IsNullOrEmpty(username)) return;

            // Quest field can carry the completer username via Quest.ClaimedByUsername.
            string completerUsername = data.Quest?.ClaimedByUsername ?? string.Empty;
            var (ok, note) = QuestManager.ConfirmBountyCompletion(username, data.QuestId, completerUsername);
            Reply(client, Result(note, data.QuestId));
            if (ok)
            {
                Shared.Files.Economy.QuestFile q = QuestManager.Snapshot().Quests
                    .FirstOrDefault(x => x.Id == data.QuestId);
                if (q != null)
                    DiscordAnnouncer.QuestCompleted(q, !string.IsNullOrEmpty(completerUsername) ? completerUsername : "(unclaimed)");
                BroadcastSnapshot();
            }
        }

        private static void HandleCancel(ServerClient client, PKT_Quest data)
        {
            string username = client.UserFile?.Username;
            if (string.IsNullOrEmpty(username)) return;

            var (ok, note) = QuestManager.Cancel(username, data.QuestId);
            Reply(client, Result(note, data.QuestId));
            if (ok) BroadcastSnapshot();
        }

        // -- helpers --

        private static PKT_Quest Result(string note, long questId) =>
            new PKT_Quest { CurrentStep = PKT_Quest.StepMode.Result, Note = note, QuestId = questId };

        private static void Reply(ServerClient client, PKT_Quest packet)
        {
            client.Listener.EnqueuePacket(PacketHeader.QuestManager, packet);
        }
    }
}
