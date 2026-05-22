namespace GameClient.Core
{
    public static class Master
    {
        public static string AppdataPath { get; set; }

        public static string AppdataRTPath { get; set; }

        public static string AppdataTempPath { get; set; }

        public static string AppdataVersionPath { get; set; }

        // KMH 26.5.22.1: Ported from upstream — local-server install
        // directory under %AppData%, used by LocalServerHandler to host
        // the user's own KMH server with one click. Kept separate from
        // AppdataTempPath so casual cache clears don't nuke the server
        // install.
        public static string AppdataLocalServerPath { get; set; }

        public static string ModMainPath { get; set; }

        public static string ModAssemblyPath { get; set; }

        public static string ModScriptsPath { get; set; }

        public static string SavesFolderPath { get; set; }

        // Values

        public static string ModPackageID { get; private set; } = "nova.rimworldtogether.kmh";

        public static string ModID { get; private set; } = "RimWorldTogether";
    }
}
