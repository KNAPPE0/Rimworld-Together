using GameClient.Managers;
using Shared;
using Shared.Files.Economy;
using Shared.Misc;
using System.Collections.Generic;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;
using static Shared.Misc.Printer;

namespace GameClient.PacketManagers
{
    /// <summary>
    /// Client-side quest board packet handler.
    /// Snapshots refresh the cache; results print a verbose log line.
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
                case PKT_Quest.StepMode.BoardSnapshot:
                    QuestClientCache.Apply(data);
                    break;

                case PKT_Quest.StepMode.Result:
                    if (!string.IsNullOrEmpty(data.Note))
                        Printer.Message(data.Note, LogImportanceMode.Verbose);
                    break;
            }
        }

        // -- senders --

        public static void RequestBoard()
        {
            try
            {
                Network.ServerEndpoint.EnqueuePacket(PacketHeader.QuestManager,
                    new PKT_Quest { CurrentStep = PKT_Quest.StepMode.RequestBoard });
            }
            catch { }
        }

        public static void Post(QuestFile draft)
        {
            try
            {
                Network.ServerEndpoint.EnqueuePacket(PacketHeader.QuestManager,
                    new PKT_Quest { CurrentStep = PKT_Quest.StepMode.Post, Quest = draft });
            }
            catch { }
        }

        public static void Claim(long id)
        {
            try
            {
                Network.ServerEndpoint.EnqueuePacket(PacketHeader.QuestManager,
                    new PKT_Quest { CurrentStep = PKT_Quest.StepMode.Claim, QuestId = id });
            }
            catch { }
        }

        public static void Abandon(long id)
        {
            try
            {
                Network.ServerEndpoint.EnqueuePacket(PacketHeader.QuestManager,
                    new PKT_Quest { CurrentStep = PKT_Quest.StepMode.Abandon, QuestId = id });
            }
            catch { }
        }

        public static void SubmitDelivery(long id)
        {
            try
            {
                Network.ServerEndpoint.EnqueuePacket(PacketHeader.QuestManager,
                    new PKT_Quest { CurrentStep = PKT_Quest.StepMode.SubmitDelivery, QuestId = id });
            }
            catch { }
        }

        public static void ConfirmBountyCompletion(long id, string completerUsername)
        {
            try
            {
                Network.ServerEndpoint.EnqueuePacket(PacketHeader.QuestManager,
                    new PKT_Quest
                    {
                        CurrentStep = PKT_Quest.StepMode.ConfirmCompletion,
                        QuestId = id,
                        Quest = new QuestFile { ClaimedByUsername = completerUsername ?? string.Empty }
                    });
            }
            catch { }
        }

        public static void Cancel(long id)
        {
            try
            {
                Network.ServerEndpoint.EnqueuePacket(PacketHeader.QuestManager,
                    new PKT_Quest { CurrentStep = PKT_Quest.StepMode.Cancel, QuestId = id });
            }
            catch { }
        }
    }
}
