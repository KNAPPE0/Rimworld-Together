namespace Shared.Files.Actions
{
    public class ActionsConfigFile : BaseFile
    {
        public static string SavePath { get; set; } = string.Empty;

        public bool EnableFactions { get; set; } = true;

        public bool EnableLeaderboard { get; set; } = true;

        public bool EnableTrading { get; set; } = true;

        public bool EnableCustomScenarios { get; set; } = true;

        public bool EnableNPCDestruction { get; set; } = false;

        public bool EnablePollutionSpread { get; set; } = true;

        // KMH 26.5.22.1: Optional per-player pollution-spread cooldown
        // (seconds). Ported from upstream's "Security checks for sites,
        // settlements, roads and pollution" (May 2026). Defaults to -1
        // = no cooldown (preserves old behaviour); set to a positive
        // value in ActionConfig.json to rate-limit pollution-spread
        // packet spam from any single client. Read by PM_Pollution via
        // PlayerCooldown.CheckIfCanPollute.
        public double PollutionCooldown { get; set; } = -1;

        // KMH 26.5.22.1: Optional per-player NPC-action cooldown (seconds)
        // for the NPC destruction toggle. Same semantics as
        // PollutionCooldown — -1 disables. Read by NPC packet handlers
        // when KMH adds them; harmless to set even if unused.
        public double NPCCooldown { get; set; } = -1;

        public ActivityAction ActivityAction { get; set; } = new ActivityAction();

        public EventAction EventAction { get; set; } = new EventAction();

        public AidAction AidAction { get; set; } = new AidAction();

        public RoadsAction RoadsAction { get; set; } = new RoadsAction();

        public SiteAction SiteAction { get; set; } = new SiteAction();
    }
}
