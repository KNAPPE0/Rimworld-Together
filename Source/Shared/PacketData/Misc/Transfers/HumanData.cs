using System;
using System.Collections.Generic;

namespace Shared
{
    [Serializable]
    public class HumanData
    {
        // Basic Information
        public string DefName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string BiologicalAge { get; set; } = string.Empty;
        public string ChronologicalAge { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string FactionDef { get; set; } = string.Empty;
        public string KindDef { get; set; } = string.Empty;

        // Appearance
        public string HairDefName { get; set; } = string.Empty;
        public string HairColor { get; set; } = string.Empty;
        public string HeadTypeDefName { get; set; } = string.Empty;
        public string SkinColor { get; set; } = string.Empty;
        public string BeardDefName { get; set; } = string.Empty;
        public string BodyTypeDefName { get; set; } = string.Empty;
        public string FaceTattooDefName { get; set; } = string.Empty;
        public string BodyTattooDefName { get; set; } = string.Empty;

        // Health Conditions (Hediffs)
        public List<string> HediffDefNames { get; set; } = new List<string>();
        public List<string> HediffPartDefNames { get; set; } = new List<string>();
        public List<float> HediffSeverities { get; set; } = new List<float>();
        public List<bool> HediffPermanents { get; set; } = new List<bool>();

        // Xenotypes
        public string XenotypeDefName { get; set; } = string.Empty;
        public string CustomXenotypeName { get; set; } = string.Empty;

        // Genes
        public List<string> XenogeneDefNames { get; set; } = new List<string>();
        public List<string> EndogeneDefNames { get; set; } = new List<string>();

        // Backstories
        public string ChildhoodStory { get; set; } = string.Empty;
        public string AdulthoodStory { get; set; } = string.Empty;

        // Skills
        public List<string> SkillDefNames { get; set; } = new List<string>();
        public List<int> SkillLevels { get; set; } = new List<int>();
        public List<string> Passions { get; set; } = new List<string>();

        // Traits
        public List<string> TraitDefNames { get; set; } = new List<string>();
        public List<int> TraitDegrees { get; set; } = new List<int>();

        // Apparel
        public List<ThingData> EquippedApparel { get; set; } = new List<ThingData>();
        public List<bool> ApparelWornByCorpse { get; set; } = new List<bool>();

        // Equipment
        public ThingData EquippedWeapon { get; set; } = new ThingData();
        public List<ThingData> InventoryItems { get; set; } = new List<ThingData>();

        // Transform (Position and Rotation)
        public string[] Position { get; set; } = Array.Empty<string>();
        public int Rotation { get; set; } = 0;

        // Miscellaneous
        public string FavoriteColor { get; set; } = string.Empty;
        public float GrowthPoints { get; set; } = 0f;
    }
}