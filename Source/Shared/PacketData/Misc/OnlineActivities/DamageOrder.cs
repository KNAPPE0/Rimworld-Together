using System;

namespace Shared
{
    [Serializable]
    public class DamageOrder
    {
        // Index of the target to receive damage
        public int TargetIndex { get; set; } = 0; // Default to 0, set to the specific target's index

        // Damage Details
        public string DefName { get; set; } = string.Empty; // Damage type or effect definition name
        public string HitPartDefName { get; set; } = string.Empty; // Name of the body part that gets hit
        public float DamageAmount { get; set; } = 0f; // Amount of damage dealt
        public string WeaponDefName { get; set; } = string.Empty; // Name of the weapon causing the damage
        public float ArmorPenetration { get; set; } = 0f; // Armor penetration value of the attack
        public bool IgnoreArmor { get; set; } = false; // Whether to ignore armor when applying damage
    }
}