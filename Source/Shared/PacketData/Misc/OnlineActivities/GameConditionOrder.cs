using System;
using static Shared.CommonEnumerators;

namespace Shared
{
    [Serializable]
    public class GameConditionOrder
    {
        // Mode of Application for the Game Condition
        public OnlineActivityApplyMode ApplyMode { get; set; } = OnlineActivityApplyMode.None; // Default to 'None'

        // Game Condition Details
        public string ConditionDefName { get; set; } = string.Empty; // Definition name of the game condition
        public int Duration { get; set; } = 0; // Duration of the game condition in ticks or seconds
    }
}