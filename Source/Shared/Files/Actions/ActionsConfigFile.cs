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

        // Seconds between pollution-spread packets per client; -1 disables.
        public double PollutionCooldown { get; set; } = -1;

        // Seconds between NPC packets per client; -1 disables.
        public double NPCCooldown { get; set; } = -1;

        public ActivityAction ActivityAction { get; set; } = new ActivityAction();

        public EventAction EventAction { get; set; } = new EventAction();

        public AidAction AidAction { get; set; } = new AidAction();

        public RoadsAction RoadsAction { get; set; } = new RoadsAction();

        public SiteAction SiteAction { get; set; } = new SiteAction();
    }
}
