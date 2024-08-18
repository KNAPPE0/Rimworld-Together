using System;
using static Shared.CommonEnumerators;

namespace Shared
{
    [Serializable]
    public class KillOrder
    {
        // Target Faction and Kill Target Index
        public OnlineActivityTargetFaction PawnFaction { get; set; } = OnlineActivityTargetFaction.None;
        public int KillTargetIndex { get; set; } = 0;
    }
}