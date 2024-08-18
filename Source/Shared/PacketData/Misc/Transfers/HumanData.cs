using System;
using System.Collections.Generic;

namespace Shared
{
    [Serializable]
    public class HumanData
    {
        // Basic Information
        public string DefName { get; set; } = "";
        public string Name { get; set; } = "";
        public string BiologicalAge { get; set; } = "";
        public string ChronologicalAge { get; set; } = "";
        public string Gender { get; set; } = "";
        public string FactionDef { get; set; } = "";
        public string KindDef { get; set; } = "";

        // Appearance
        public string HairDefName { get; set; } = "";
        public string HairColor { get; set; } = "";
        public string HeadTypeDefName { get; set; } = "";
        public string SkinColor { get; set; } = "";
        public string BeardDefName { get; set; } = "";
        public string BodyTypeDefName { get; set; } = "";
        public string FaceTattooDefName { get; set; } = "";
        public string BodyTattooDefName { get; set; } = "";

        // Health Conditions (Hediffs)
        public List<string> HediffDefNames { get; set; } = new List<string>();
        public List<string> HediffPartDefNames { get; set; } = new List<string>();
        public List<string> HediffSeverities { get; set; } = new List<string>();
        public List<bool> HediffPermanents { get; set; } = new List<bool>();

        // Xenotypes
        public string XenotypeDefName { get; set; } = "";
        public string CustomXenotypeName { get; set; } = "";

        // Genes
        public List<string> XenogeneDefNames { get; set; } = new List<string>();
        public List<string> EndogeneDefNames { get; set; } = new List<string>();

        // Backstories
        public string ChildhoodStory { get; set; } = "";
        public string AdulthoodStory { get; set; } = "";

        // Skills
        public List<string> SkillDefNames { get; set; } = new List<string>();
        public List<string> SkillLevels { get; set; } = new List<string>();
        public List<string> Passions { get; set; } = new List<string>();

        // Traits
        public List<string> TraitDefNames { get; set; } = new List<string>();
        public List<string> TraitDegrees { get; set; } = new List<string>();

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
        public string FavoriteColor { get; set; } = "";
        public float GrowthPoints { get; set; } = 0f;
    }
}