using Shared.Files.Economy;
using System.Collections.Generic;

namespace GameClient.Managers
{
    /// <summary>
    /// Last server snapshot of the quest board. Dialogs read here.
    /// </summary>
    public static class QuestClientCache
    {
        public static List<QuestFile> Quests { get; set; } = new List<QuestFile>();
        public static long LifetimeQuestsPosted { get; set; }
        public static long LifetimeQuestsCompleted { get; set; }
        public static long LifetimeBountySilverPaid { get; set; }
        public static bool HasSnapshot { get; set; }

        public static System.Action OnSnapshotUpdated;

        public static void Apply(TCPNetwork.Packets.PKT_Quest snap)
        {
            if (snap == null) return;
            Quests = snap.Board ?? new List<QuestFile>();
            LifetimeQuestsPosted = snap.LifetimeQuestsPosted;
            LifetimeQuestsCompleted = snap.LifetimeQuestsCompleted;
            LifetimeBountySilverPaid = snap.LifetimeBountySilverPaid;
            HasSnapshot = true;
            try { OnSnapshotUpdated?.Invoke(); } catch { }
        }
    }
}
