using GameClient.Dialogs;
using GameClient.Dialogs.Default;
using GameClient.Misc;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.IO;
using Verse;

namespace GameClient.Managers
{
    /// <summary>
    /// Disk-write half of the profile flow:
    ///   * EnsureBackup / RestoreBackup (personal config preservation)
    ///   * ApplyEnforcedProfile (one-shot copy + state update + restart prompt)
    ///   * ApplyProfileFolderToConfig (the actual file copy)
    ///   * ReapplyProfileToDiskIfActive (re-write after the watcher detected drift)
    ///   * RestorePersonalConfigsManual (user-driven undo from the main menu)
    /// </summary>
    public static partial class OptionsProfileSessionManager
    {
        public static void RestorePersonalConfigsManual()
        {
            try
            {
                if (State == null)
                    LoadOrCreateState();

                if (!Directory.Exists(BackupPath))
                {
                    DLG_Base.PushNewDialog(new DLG_Message("Profile", new[] { "No backup found." }));
                    return;
                }

                StopWatcher();

                IsInternalApplyInProgress = true;
                try
                {
                    RestoreBackupToConfig(softReload: true);
                }
                finally
                {
                    IsInternalApplyInProgress = false;
                }

                ClearSessionState();
                SafeDeleteDirectory(BackupPath);

                DLG_Base.PushNewDialog(new DLG_Message(
                    "Profile",
                    new[] { "Your original configs were restored.", "The game will now restart." },
                    onConfirm: delegate
                    {
                        try { GenCommandLine.Restart(); }
                        catch { Root.Shutdown(); }
                    }));
            }
            catch (Exception e)
            {
                Printer.Warning($"[OptionsProfile] RestorePersonalConfigsManual failed: {e}");
                DLG_Base.PushNewDialog(new DLG_Message("Error", new[] { "Restore failed. Check logs." }));
            }
        }

        public static void ReapplyProfileToDiskIfActive(bool softReload)
        {
            try
            {
                if (State == null || !State.IsEnforcedActive) return;
                if (SessionHandler.CurrentNetworkState == GameClient.Hooks.TCPNetwork.ClientNetwork.ClientNetworkState.Disconnected) return;

                string profileFolder = GetProfileFolder(State.ActiveProfileHash);
                if (!Directory.Exists(profileFolder)) return;

                IsInternalApplyInProgress = true;
                try
                {
                    ApplyProfileFolderToConfig(profileFolder, updateStateManifest: false);
                    WriteActiveMarker(State.ActiveProfileHash, State.ActiveProfileUpdatedUtcTicks, State.LastAppliedFileCount);
                    if (softReload) TrySoftReloadModSettings();
                }
                finally
                {
                    IsInternalApplyInProgress = false;
                }
            }
            catch (Exception e)
            {
                Printer.Warning($"[OptionsProfile] ReapplyProfileToDiskIfActive failed: {e}");
            }
        }

        private static void ApplyEnforcedProfile(string profileFolder, string hash, long updatedTicks)
        {
            try
            {
                if (State == null)
                    State = new SessionState();

                Directory.CreateDirectory(ConfigPath);
                Directory.CreateDirectory(ProfilesRoot);

                EnsureBackupExists();

                File.WriteAllText(CrashMarkerPath, DateTime.UtcNow.ToString("o"));

                IsInternalApplyInProgress = true;
                int copiedCount;

                try
                {
                    copiedCount = ApplyProfileFolderToConfig(profileFolder, updateStateManifest: true);
                    TrySoftReloadModSettings();
                }
                finally
                {
                    IsInternalApplyInProgress = false;
                    try { File.Delete(CrashMarkerPath); } catch { }
                }

                State.IsEnforcedActive = true;
                State.ActiveProfileHash = hash;
                State.ActiveProfileUpdatedUtcTicks = updatedTicks;
                State.LastAppliedUtcTicks = DateTime.UtcNow.Ticks;
                State.LastAppliedFileCount = copiedCount;
                SaveState();

                EnforcementGuard.MarkEnforced(hash);
                WriteActiveMarker(hash, updatedTicks, copiedCount);
                StartWatcher();

                DLG_Base.PushNewDialog(new DLG_Message(
                    "Server Config Enforced",
                    new[]
                    {
                        "The server config profile has been applied.\n\n" +
                        $"Files copied: {copiedCount}\n" +
                        $"Hash: {hash}\n\n",
                        "The game must restart now to finish applying the enforced config."
                    },
                    onConfirm: delegate
                    {
                        try { GenCommandLine.Restart(); }
                        catch { Root.Shutdown(); }
                    }));
            }
            catch (Exception e)
            {
                Printer.Warning($"[OptionsProfile] ApplyEnforcedProfile failed: {e}");
                DLG_Base.PushNewDialog(new DLG_Message("Error", new[] { "Failed to apply the server profile. Check logs." }));
            }
        }

        private static int ApplyProfileFolderToConfig(string profileFolder, bool updateStateManifest)
        {
            Directory.CreateDirectory(ConfigPath);

            RemovePreviouslyManagedFiles();

            List<string> managedFiles = new List<string>();
            int copied = 0;

            foreach (string dir in Directory.GetDirectories(profileFolder, "*", SearchOption.AllDirectories))
            {
                string relDir = MakeRelative(profileFolder, dir);
                if (string.IsNullOrEmpty(relDir)) continue;

                string targetDir = Path.Combine(ConfigPath, relDir);
                Directory.CreateDirectory(targetDir);
            }

            foreach (string file in Directory.GetFiles(profileFolder, "*", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (ConfigProfileUtility.DefaultExcludeFiles.Contains(name)) continue;

                string rel = MakeRelative(profileFolder, file);
                if (string.IsNullOrEmpty(rel)) continue;

                string dest = Path.Combine(ConfigPath, rel);
                string destFolder = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(destFolder))
                    Directory.CreateDirectory(destFolder);

                File.Copy(file, dest, true);
                managedFiles.Add(rel);
                copied++;
            }

            if (updateStateManifest)
            {
                if (State == null)
                    State = new SessionState();

                State.ManagedFiles = managedFiles;
            }

            return copied;
        }

        private static void RemovePreviouslyManagedFiles()
        {
            try
            {
                if (State?.ManagedFiles == null || State.ManagedFiles.Count == 0) return;

                for (int i = 0; i < State.ManagedFiles.Count; i++)
                {
                    string rel = State.ManagedFiles[i];
                    if (string.IsNullOrWhiteSpace(rel)) continue;

                    string full = Path.Combine(ConfigPath, rel);
                    try
                    {
                        if (File.Exists(full))
                            File.Delete(full);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void EnsureBackupExists()
        {
            try
            {
                if (Directory.Exists(BackupPath)) return;

                Directory.CreateDirectory(BackupPath);

                if (Directory.Exists(ConfigPath))
                    CopyDirectory(ConfigPath, BackupPath);
            }
            catch (Exception e)
            {
                Printer.Warning($"[OptionsProfile] Failed to create backup: {e}");
            }
        }

        private static void RestoreBackupToConfig(bool softReload)
        {
            Directory.CreateDirectory(ConfigPath);

            WipeDirectoryContents(ConfigPath);
            CopyDirectory(BackupPath, ConfigPath);

            if (softReload)
                TrySoftReloadModSettings();
        }

        private static void WriteActiveMarker(string hash, long updatedTicks, int copiedFiles)
        {
            try
            {
                Directory.CreateDirectory(ConfigPath);

                File.WriteAllText(
                    ActiveMarkerPath,
                    $"RWT Options Profile Active{Environment.NewLine}" +
                    $"Hash={hash}{Environment.NewLine}" +
                    $"UpdatedUtcTicks={updatedTicks}{Environment.NewLine}" +
                    $"AppliedUtc={DateTime.UtcNow:o}{Environment.NewLine}" +
                    $"CopiedFiles={copiedFiles}{Environment.NewLine}" +
                    $"BackupPath={BackupPath}{Environment.NewLine}" +
                    $"ProfilesRoot={ProfilesRoot}{Environment.NewLine}");
            }
            catch { }
        }
    }
}
