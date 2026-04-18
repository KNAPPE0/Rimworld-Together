using System;
using System.IO;
using Verse;

namespace GameClient.Managers
{
    /// <summary>
    /// KMH: Simple enforcement detection using a plain text marker file.
    /// When server configs are enforced, a marker file is written.
    /// When configs are restored, the marker file is deleted.
    /// All enforcement checks go through this class.
    /// </summary>
    public static class EnforcementGuard
    {
        private static string RootPath => Directory.GetParent(GenFilePaths.ConfigFolderPath).FullName;
        private static string MarkerPath => Path.Combine(RootPath, "RWT_ENFORCED.txt");
        private static string BackupPath => Path.Combine(RootPath, "ConfigRWT_BACKUP");

        /// <summary>Is enforcement currently active? Checks marker file and backup dir.</summary>
        public static bool IsActive
        {
            get
            {
                try
                {
                    if (File.Exists(MarkerPath)) return true;
                    if (Directory.Exists(BackupPath)) return true;
                }
                catch { }
                return false;
            }
        }

        /// <summary>Does a backup of original configs exist?</summary>
        public static bool HasBackup
        {
            get
            {
                try { return Directory.Exists(BackupPath); }
                catch { return false; }
            }
        }

        /// <summary>Get the enforced profile hash, or empty string.</summary>
        public static string GetHash()
        {
            try
            {
                if (File.Exists(MarkerPath))
                    return File.ReadAllText(MarkerPath).Trim();
            }
            catch { }
            return string.Empty;
        }

        /// <summary>Mark enforcement as active. Called after profile is applied.</summary>
        public static void MarkEnforced(string hash)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath));
                File.WriteAllText(MarkerPath, hash ?? "enforced");
            }
            catch (Exception e)
            {
                Shared.Misc.Printer.Warning($"[EnforcementGuard] Failed to write marker: {e.Message}");
            }
        }

        /// <summary>Clear enforcement marker. Called after configs are restored.</summary>
        public static void ClearMarker()
        {
            try
            {
                if (File.Exists(MarkerPath))
                    File.Delete(MarkerPath);
            }
            catch { }
        }
    }
}
