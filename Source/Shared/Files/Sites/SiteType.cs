namespace Shared.Files.Sites
{
    public class SiteType 
    {
        /// <summary>The SitePartDef defName this site type uses.</summary>
        public string DefName { get; set; } = string.Empty;

        /// <summary>Silver cost to build this site.</summary>
        public int Cost { get; set; } = -1;

        /// <summary>Human-readable description shown in the build dialog.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Minutes per production cycle. 0 or negative = use server default.</summary>
        public int CycleTimeMinutes { get; set; } = 0;

        /// <summary>Whether this is a player-created custom site type.</summary>
        public bool IsCustom { get; set; } = false;

        /// <summary>Username of the player who created this custom site (if IsCustom).</summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>Items produced each cycle.</summary>
        public SiteReward[] Rewards { get; set; } = null;
    }
}
