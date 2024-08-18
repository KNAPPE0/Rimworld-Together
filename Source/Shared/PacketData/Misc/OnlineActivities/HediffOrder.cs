using System;
using static Shared.CommonEnumerators;

namespace Shared
{
    [Serializable]
    public class HediffOrder
    {
        // Faction and Application Mode
        public CommonEnumerators.OnlineActivityTargetFaction PawnFaction { get; set; } = CommonEnumerators.OnlineActivityTargetFaction.None;
        public CommonEnumerators.OnlineActivityApplyMode ApplyMode { get; set; } = CommonEnumerators.OnlineActivityApplyMode.None;

        // Hediff (Health Condition) Details
        public int HediffTargetIndex { get; set; } = 0;   // Index of the target pawn or entity for the hediff
        public string HediffDefName { get; set; } = string.Empty;   // Definition name of the hediff
        public string HediffPartDefName { get; set; } = string.Empty;   // Body part affected by the hediff
        public string HediffWeaponDefName { get; set; } = string.Empty;   // Weapon responsible for the hediff, if any
        public float HediffSeverity { get; set; } = 0f;   // Severity level of the hediff
        public bool HediffPermanent { get; set; } = false;   // Indicates if the hediff is permanent
    }
}