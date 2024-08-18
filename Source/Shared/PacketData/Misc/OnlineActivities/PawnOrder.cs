using System;
using static Shared.CommonEnumerators;

namespace Shared
{
    [Serializable]
    public class PawnOrder
    {
        // Basic Information
        public string DefName { get; set; } = "";
        public int PawnIndex { get; set; } = 0;

        // Targets
        public int TargetCount { get; set; } = 0;
        public int[] QueueTargetCounts { get; set; } = Array.Empty<int>();

        public string[] Targets { get; set; } = Array.Empty<string>();
        public int[] TargetIndexes { get; set; } = Array.Empty<int>();
        public ActionTargetType[] TargetTypes { get; set; } = Array.Empty<ActionTargetType>();
        public OnlineActivityTargetFaction[] TargetFactions { get; set; } = Array.Empty<OnlineActivityTargetFaction>();

        // Queue A
        public string[] QueueTargetsA { get; set; } = Array.Empty<string>();
        public int[] QueueTargetIndexesA { get; set; } = Array.Empty<int>();
        public ActionTargetType[] QueueTargetTypesA { get; set; } = Array.Empty<ActionTargetType>();
        public OnlineActivityTargetFaction[] QueueTargetFactionsA { get; set; } = Array.Empty<OnlineActivityTargetFaction>();

        // Queue B
        public string[] QueueTargetsB { get; set; } = Array.Empty<string>();
        public int[] QueueTargetIndexesB { get; set; } = Array.Empty<int>();
        public ActionTargetType[] QueueTargetTypesB { get; set; } = Array.Empty<ActionTargetType>();
        public OnlineActivityTargetFaction[] QueueTargetFactionsB { get; set; } = Array.Empty<OnlineActivityTargetFaction>();

        // Draft Status
        public bool IsDrafted { get; set; } = false;

        // Transform (Position and Rotation)
        public int[] UpdatedPosition { get; set; } = Array.Empty<int>();
        public int UpdatedRotation { get; set; } = 0;
    }
}