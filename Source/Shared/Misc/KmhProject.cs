namespace Shared
{
    // Single source of truth for KMH-side and upstream RWT URLs.
    // Update GitHubOwner/GitHubRepo here and every consumer (mod
    // settings UI, LocalServerHandler, welcome dialog, About.xml
    // hints) picks it up automatically.
    public static class KmhProject
    {
        // ---- KMH (this fork) ----
        public const string GitHubOwner = "KNAPPE0";
        public const string GitHubRepo = "Rimworld-Together";
        public const string DiscordUrl = "https://discord.gg/gDwmsy7VVy";
        public const string SteamWorkshopUrl = "https://steamcommunity.com/sharedfiles/filedetails/?id=3638751319";

        public static string GitHubUrl => $"https://github.com/{GitHubOwner}/{GitHubRepo}";
        public static string GitHubReleasesUrl => $"{GitHubUrl}/releases";
        public static string GitHubLatestServerZipTemplate => $"{GitHubUrl}/releases/latest/download/{{RID}}.zip";

        // ---- Upstream RWT (the mod KMH is based on) ----
        public static class Upstream
        {
            public const string GitHubOwner = "RimWorld-Together";
            public const string GitHubRepo = "Rimworld-Together";
            public const string DiscordUrl = "https://discord.gg/yUF2ec8Vt8";

            public static string GitHubUrl => $"https://github.com/{GitHubOwner}/{GitHubRepo}";
        }
    }
}
