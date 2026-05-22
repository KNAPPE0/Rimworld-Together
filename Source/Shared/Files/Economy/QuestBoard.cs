using System.Collections.Generic;

namespace Shared.Files.Economy
{
    /// <summary>
    /// Holds every quest the server knows about. One file, atomic writes.
    /// </summary>
    public class QuestBoard
    {
        public List<QuestFile> Quests { get; set; } = new List<QuestFile>();
        public long LifetimeQuestsPosted { get; set; }
        public long LifetimeQuestsCompleted { get; set; }
        public long LifetimeBountySilverPaid { get; set; }
    }
}
