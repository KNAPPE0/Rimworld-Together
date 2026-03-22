namespace Shared
{
    public static class CommonValues
    {
        public static readonly string MasterServer = "https://rimworldtogether.eragon.dev";

        public static string ExecutableVersion { get; set; } = "26.3.22.2 KMH";

        public static string DefaultSaveFormat { get; set; } = ".json";

        public static string TempSaveFormat { get; set; } = ".temp";

        public static string CompressedSaveFormat { get; set; } = ".zip";

        public static string ServerUsersPath { get; set; } = string.Empty;

        public static string ServerSitesPath { get; set; } = string.Empty;
    }
}