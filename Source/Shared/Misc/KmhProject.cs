namespace Shared
{
    // Single source of truth for KMH and official RWT URLs.
    // Update GitHubOwner/GitHubRepo here and every consumer
    // (mod settings UI, LocalServerHandler, welcome dialog, About.xml hints) picks it up automatically.
    public static class KMHProject
    {
        // KMH
        public const string GitHubOwner = "KNAPPE0";
        public const string GitHubRepo = "Rimworld-Together";
        public const string DiscordUrl = "https://discord.gg/gDwmsy7VVy";
        public const string SteamWorkshopUrl = "https://steamcommunity.com/sharedfiles/filedetails/?id=3638751319";

        public static string GitHubUrl => $"https://github.com/{GitHubOwner}/{GitHubRepo}";
        public static string GitHubReleasesUrl => $"{GitHubUrl}/releases";
        public static string GitHubLatestServerZipTemplate => $"{GitHubUrl}/releases/latest/download/{{RID}}.zip";

        // Official RWT (the mod KMH is based on)
        public static class Official
        {
            public const string GitHubOwner = "RimWorld-Together";
            public const string GitHubRepo = "Rimworld-Together";
            public const string DiscordUrl = "https://discord.gg/yUF2ec8Vt8";
            public const string SteamWorkshopUrl = "https://steamcommunity.com/sharedfiles/filedetails/?id=3005289691";

            public static string GitHubUrl => $"https://github.com/{GitHubOwner}/{GitHubRepo}";
        }
    }
}
