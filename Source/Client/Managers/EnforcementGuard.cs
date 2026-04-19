using System;
using System.IO;
using Verse;

namespace GameClient.Managers
{
    public static class EnforcementGuard
    {
        private static string RootPath => Directory.GetParent(GenFilePaths.ConfigFolderPath).FullName;
        private static string MarkerPath => Path.Combine(RootPath, "RWT_ENFORCED.txt");
        private static string BackupPath => Path.Combine(RootPath, "ConfigRWT_BACKUP");

        public static bool IsActive
        {
            get
            {
                try
                {
                    return File.Exists(MarkerPath);
                }
                catch
                {
                    return false;
                }
            }
        }

        public static bool HasBackup
        {
            get
            {
                try
                {
                    return Directory.Exists(BackupPath);
                }
                catch
                {
                    return false;
                }
            }
        }

        public static string GetHash()
        {
            try
            {
                if (!File.Exists(MarkerPath)) return string.Empty;

                string[] lines = File.ReadAllLines(MarkerPath);
                foreach (string line in lines)
                {
                    if (line.StartsWith("Hash=", StringComparison.OrdinalIgnoreCase))
                        return line.Substring("Hash=".Length).Trim();
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static void MarkEnforced(string hash)
        {
            try
            {
                string folder = Path.GetDirectoryName(MarkerPath);
                if (!string.IsNullOrEmpty(folder))
                    Directory.CreateDirectory(folder);

                File.WriteAllText(
                    MarkerPath,
                    $"Enforced=true{Environment.NewLine}" +
                    $"Hash={hash ?? string.Empty}{Environment.NewLine}" +
                    $"AppliedUtc={DateTime.UtcNow:o}{Environment.NewLine}");
            }
            catch (Exception e)
            {
                Shared.Misc.Printer.Warning($"[EnforcementGuard] Failed to write marker: {e}");
            }
        }

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