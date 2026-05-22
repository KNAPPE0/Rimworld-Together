using GameClient.Hooks.TCPNetwork;
using GameClient.Misc;
using Shared.Misc;
using System;
using System.IO;
using Verse;

namespace GameClient.Managers
{
    /// <summary>
    /// Client-side options-profile session: tracks the active enforced profile,
    /// applies/restores files on disk, and manages the file-system watcher that
    /// reverts user edits while enforcement is active.
    ///
    /// Concrete responsibilities live in partial files under
    /// <c>OptionsProfile/</c>:
    ///   * Transport — chunk receive + publish/request
    ///   * Apply     — backup/restore + write profile to disk
    ///   * Watcher   — FileSystemWatcher + reapply loop
    ///   * SessionState — disk-backed state record
    /// </summary>
    public static partial class OptionsProfileSessionManager
    {
        private const int ChunkSizeBytes = 256 * 1024;

        private static string ConfigPath => GenFilePaths.ConfigFolderPath;
        private static string RootPath => Directory.GetParent(GenFilePaths.ConfigFolderPath).FullName;
        private static string BackupPath => Path.Combine(RootPath, "ConfigRWT_BACKUP");
        private static string ProfilesRoot => Path.Combine(RootPath, "ConfigRWT_PROFILES");
        private static string StatePath => Path.Combine(RootPath, "RWT_SESSION_STATE.json");
        private static string CrashMarkerPath => Path.Combine(RootPath, "RWT_CRASH_DURING_APPLY");
        private static string ActiveMarkerPath => Path.Combine(ConfigPath, "RWT_ACTIVE_PROFILE.txt");

        private static bool Bootstrapped;

        public static bool IsInternalApplyInProgress { get; private set; }

        public static void Bootstrap()
        {
            if (Bootstrapped) return;
            Bootstrapped = true;

            try
            {
                Directory.CreateDirectory(ProfilesRoot);
                LoadOrCreateState();

                if (File.Exists(CrashMarkerPath) && Directory.Exists(BackupPath))
                {
                    Printer.Warning("[OptionsProfile] Crash marker detected. Restoring personal backup.");
                    RestoreBackupToConfig(softReload: true);
                    ClearSessionState();
                    SafeDeleteDirectory(BackupPath);

                    try { File.Delete(CrashMarkerPath); } catch { }
                }

                if (State != null && State.IsEnforcedActive)
                    EnforcementGuard.MarkEnforced(State.ActiveProfileHash);
            }
            catch (Exception e)
            {
                Printer.Warning($"[OptionsProfile] Bootstrap failed: {e}");
            }
        }

        public static bool HasBackup()
        {
            try { return Directory.Exists(BackupPath); }
            catch { return false; }
        }

        public static bool IsEnforcementActive()
        {
            return (State != null && State.IsEnforcedActive) || EnforcementGuard.IsActive;
        }

        public static string GetActiveHash()
        {
            return State?.ActiveProfileHash ?? string.Empty;
        }

        public static string GetStatusLine()
        {
            if (State == null) return "Server Options Profile: Unknown";
            if (!State.IsEnforcedActive) return "Server Options Profile: Inactive";

            string applied = State.LastAppliedUtcTicks > 0
                ? new DateTime(State.LastAppliedUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("g")
                : "Unknown";

            return $"Server Options Profile: ACTIVE ({State.ActiveProfileHash}) | Applied: {applied} | Files: {State.LastAppliedFileCount}";
        }

        public static void TryRestoreOnDisconnect()
        {
            StopWatcher();
        }

        private static string GetProfileFolder(string hash)
        {
            return Path.Combine(ProfilesRoot, hash);
        }

        // -- shared low-level utilities --

        private static byte[] Combine(byte[][] parts)
        {
            int total = 0;
            for (int i = 0; i < parts.Length; i++)
                total += parts[i]?.Length ?? 0;

            byte[] all = new byte[total];
            int offset = 0;

            for (int i = 0; i < parts.Length; i++)
            {
                byte[] p = parts[i] ?? Array.Empty<byte>();
                Buffer.BlockCopy(p, 0, all, offset, p.Length);
                offset += p.Length;
            }

            return all;
        }

        private static bool TrySoftReloadModSettings()
        {
            try
            {
                var m = typeof(LoadedModManager).GetMethod("ReadModSettings",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (m != null)
                {
                    m.Invoke(null, null);
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            if (!Directory.Exists(sourceDir))
            {
                Directory.CreateDirectory(targetDir);
                return;
            }

            Directory.CreateDirectory(targetDir);

            foreach (string dir in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                string rel = dir.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(targetDir, rel));
            }

            foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string rel = file.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar);
                string dest = Path.Combine(targetDir, rel);

                string destFolder = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(destFolder))
                    Directory.CreateDirectory(destFolder);

                File.Copy(file, dest, true);
            }
        }

        private static void WipeDirectoryContents(string dir)
        {
            if (!Directory.Exists(dir)) return;

            foreach (string file in Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly))
            {
                try { File.Delete(file); } catch { }
            }

            foreach (string subDir in Directory.GetDirectories(dir, "*", SearchOption.TopDirectoryOnly))
            {
                SafeDeleteDirectory(subDir);
            }
        }

        private static void SafeDeleteDirectory(string dir)
        {
            if (!Directory.Exists(dir)) return;
            try { Directory.Delete(dir, true); } catch { }
        }

        private static string MakeRelative(string root, string fullPath)
        {
            string r = Path.GetFullPath(root);
            if (!r.EndsWith(Path.DirectorySeparatorChar.ToString()))
                r += Path.DirectorySeparatorChar;

            string f = Path.GetFullPath(fullPath);
            if (!f.StartsWith(r, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            return f.Substring(r.Length);
        }
    }
}
