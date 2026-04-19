using GameClient.Dialogs;
using GameClient.Dialogs.Default;
using GameClient.Hooks.TCPNetwork;
using GameClient.Misc;
using Shared;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TCPNetwork;
using TCPNetwork.Packets;
using Verse;

namespace GameClient.Managers
{
    public static class OptionsProfileSessionManager
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
        private static SessionState State;

        private static readonly Dictionary<string, IncomingProfileBuffer> IncomingProfiles =
            new Dictionary<string, IncomingProfileBuffer>(StringComparer.OrdinalIgnoreCase);

        private static FileSystemWatcher Watcher;
        private static CancellationTokenSource WatcherToken;
        private static int PendingReapplyFlag;
        private static DateTime LastReapplyUtc = DateTime.MinValue;
        private static int PendingManualRequest;

        public static bool IsInternalApplyInProgress { get; private set; }

        private class SessionState
        {
            public bool IsEnforcedActive { get; set; }
            public string ActiveProfileHash { get; set; } = string.Empty;
            public long ActiveProfileUpdatedUtcTicks { get; set; }
            public long LastAppliedUtcTicks { get; set; }
            public int LastAppliedFileCount { get; set; }
            public List<string> ManagedFiles { get; set; } = new List<string>();
        }

        private class IncomingProfileBuffer
        {
            public string Hash;
            public long UpdatedTicks;
            public int ChunkCount;
            public byte[][] Chunks;
            public int ReceivedCount;
        }

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

        public static void RequestServerOptionsProfile(bool isManual)
        {
            try
            {
                if (Network.ServerEndpoint == null) return;

                if (isManual)
                    Interlocked.Exchange(ref PendingManualRequest, 1);

                PKT_ModConfig req = new PKT_ModConfig
                {
                    _requestOptionsProfile = true,
                    _stepMode = PKT_ModConfig.ModConfigStepMode.Send
                };

                Network.ServerEndpoint.EnqueuePacket(PacketHeader.ModManager, req);
                Printer.Warning("[OptionsProfile] Requested profile from server.");
            }
            catch (Exception e)
            {
                Printer.Warning($"[OptionsProfile] RequestServerOptionsProfile failed: {e}");
            }
        }

        public static void PublishCurrentConfigProfileToServer()
        {
            try
            {
                if (!SessionHandler.IsAdmin)
                {
                    DLG_Base.PushNewDialog(new DLG_Message("Error", new[] { "Admin only." }));
                    return;
                }

                if (Network.ServerEndpoint == null)
                {
                    DLG_Base.PushNewDialog(new DLG_Message("Error", new[] { "Not connected." }));
                    return;
                }

                Directory.CreateDirectory(ConfigPath);

                byte[] zip = ConfigProfileUtility.CreateConfigZipBytes(ConfigPath, ConfigProfileUtility.DefaultExcludeFiles);
                string hash = ConfigProfileUtility.Sha256Hex(zip);
                List<byte[]> chunks = ConfigProfileUtility.SplitIntoChunks(zip, ChunkSizeBytes);
                long ticks = DateTime.UtcNow.Ticks;

                for (int i = 0; i < chunks.Count; i++)
                {
                    PKT_ModConfig part = new PKT_ModConfig
                    {
                        _uploadOptionsProfile = true,
                        _stepMode = PKT_ModConfig.ModConfigStepMode.Send,
                        _optionsProfileHash = hash,
                        _optionsProfileUpdatedUtcTicks = ticks,
                        _chunkIndex = i,
                        _chunkCount = chunks.Count,
                        _chunkBytes = chunks[i]
                    };

                    Network.ServerEndpoint.EnqueuePacket(PacketHeader.ModManager, part);
                }

                if (SessionHandler.CurrentModConfig != null)
                    SessionHandler.CurrentModConfig.IsEnforced = true;

                DLG_Base.PushNewDialog(new DLG_Message("Profile",
                    new[] { "Published server options profile.", "Enforcement is now active on the server.", $"Hash: {hash}" }));
            }
            catch (Exception e)
            {
                Printer.Warning($"[OptionsProfile] PublishCurrentConfigProfileToServer failed: {e}");
                DLG_Base.PushNewDialog(new DLG_Message("Error", new[] { "Failed to publish profile. Check logs." }));
            }
        }

        public static void ReceiveOptionsProfilePacket(PKT_ModConfig data)
        {
            if (data == null) return;

            if (data._noOptionsProfileAvailable)
            {
                bool wasManual = Interlocked.Exchange(ref PendingManualRequest, 0) == 1;

                if (wasManual)
                {
                    MainThreadHandler.Instance.Enqueue(delegate
                    {
                        DLG_Base.PushNewDialog(new DLG_Message("Profile",
                            new[] { "This server does not currently have a published options profile." }));
                    });
                }

                return;
            }

            if (!data._isOptionsProfileChunk) return;
            if (string.IsNullOrEmpty(data._optionsProfileHash)) return;
            if (data._chunkCount <= 0) return;
            if (data._chunkIndex < 0 || data._chunkIndex >= data._chunkCount) return;

            if (!IncomingProfiles.TryGetValue(data._optionsProfileHash, out IncomingProfileBuffer buffer))
            {
                buffer = new IncomingProfileBuffer
                {
                    Hash = data._optionsProfileHash,
                    UpdatedTicks = data._optionsProfileUpdatedUtcTicks,
                    ChunkCount = data._chunkCount,
                    Chunks = new byte[data._chunkCount][],
                    ReceivedCount = 0
                };

                IncomingProfiles[data._optionsProfileHash] = buffer;
            }

            if (buffer.Chunks[data._chunkIndex] == null)
            {
                buffer.Chunks[data._chunkIndex] = data._chunkBytes ?? Array.Empty<byte>();
                buffer.ReceivedCount++;
            }

            if (buffer.ReceivedCount < buffer.ChunkCount)
                return;

            byte[] full = Combine(buffer.Chunks);
            IncomingProfiles.Remove(buffer.Hash);

            string computed = ConfigProfileUtility.Sha256Hex(full);
            if (!string.Equals(computed, buffer.Hash, StringComparison.OrdinalIgnoreCase))
            {
                MainThreadHandler.Instance.Enqueue(delegate
                {
                    DLG_Base.PushNewDialog(new DLG_Message("Error",
                        new[] { "Options profile download failed because the hash did not match." }));
                });
                return;
            }

            string profileFolder = GetProfileFolder(buffer.Hash);
            SafeDeleteDirectory(profileFolder);
            Directory.CreateDirectory(profileFolder);
            ConfigProfileUtility.ExtractZipBytesToFolder(full, profileFolder);

            bool alreadyActive = State != null
                && State.IsEnforcedActive
                && string.Equals(State.ActiveProfileHash, buffer.Hash, StringComparison.OrdinalIgnoreCase);

            if (alreadyActive)
            {
                Printer.Warning($"[OptionsProfile] Profile {buffer.Hash} already active. Reapplying to disk.");
                ReapplyProfileToDiskIfActive(softReload: true);
                return;
            }

            string pf = profileFolder;
            string h = buffer.Hash;
            long t = buffer.UpdatedTicks;

            MainThreadHandler.Instance.Enqueue(delegate
            {
                ApplyEnforcedProfile(pf, h, t);
            });
        }

        public static void TryRestoreOnDisconnect()
        {
            StopWatcher();
        }

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
                if (SessionHandler.CurrentNetworkState == ClientNetwork.ClientNetworkState.Disconnected) return;

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

        public static void HandleJoinRequirement(PKT_ModConfig data)
        {
            try
            {
                string requiredHash = data?._optionsProfileHash ?? string.Empty;
                string localHash = GetActiveHash();

                bool alreadyMatching =
                    IsEnforcementActive() &&
                    !string.IsNullOrWhiteSpace(localHash) &&
                    string.Equals(localHash, requiredHash, StringComparison.OrdinalIgnoreCase);

                if (alreadyMatching)
                {
                    Printer.Warning($"[OptionsProfile] Required profile already active locally. Hash={localHash}");
                    return;
                }

                MainThreadHandler.Instance.Enqueue(delegate
                {
                    DLG_Base.PushNewDialog(new DLG_Message(
                        "Server Config Required",
                        new[]
                        {
                            "This server requires its config profile before you can join.",
                            "The profile is being downloaded and will be verified.",
                            "After it is applied, the game will restart."
                        }));
                });

                RequestServerOptionsProfile(isManual: false);
            }
            catch (Exception e)
            {
                Printer.Warning($"[OptionsProfile] HandleJoinRequirement failed: {e}");
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

        private static void LoadOrCreateState()
        {
            try
            {
                if (File.Exists(StatePath))
                    State = Serializer.SerializeFromFile<SessionState>(StatePath);
                else
                {
                    State = new SessionState();
                    Serializer.SerializeToFile(StatePath, State);
                }
            }
            catch
            {
                State = new SessionState();
                try { Serializer.SerializeToFile(StatePath, State); } catch { }
            }
        }

        private static void SaveState()
        {
            try
            {
                Serializer.SerializeToFile(StatePath, State);
            }
            catch { }
        }

        private static void ClearSessionState()
        {
            if (State == null)
                State = new SessionState();

            State.IsEnforcedActive = false;
            State.ActiveProfileHash = string.Empty;
            State.ActiveProfileUpdatedUtcTicks = 0;
            State.LastAppliedUtcTicks = 0;
            State.LastAppliedFileCount = 0;
            State.ManagedFiles = new List<string>();

            SaveState();
            EnforcementGuard.ClearMarker();

            try
            {
                if (File.Exists(ActiveMarkerPath))
                    File.Delete(ActiveMarkerPath);
            }
            catch { }
        }

        private static string GetProfileFolder(string hash)
        {
            return Path.Combine(ProfilesRoot, hash);
        }

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
                MethodInfo m = typeof(LoadedModManager).GetMethod("ReadModSettings", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
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

        private static void StartWatcher()
        {
            try
            {
                StopWatcher();

                if (State == null || !State.IsEnforcedActive) return;
                if (!Directory.Exists(ConfigPath)) return;

                Watcher = new FileSystemWatcher(ConfigPath)
                {
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName | NotifyFilters.Size
                };

                Watcher.Changed += OnConfigChanged;
                Watcher.Created += OnConfigChanged;
                Watcher.Deleted += OnConfigChanged;
                Watcher.Renamed += OnConfigChanged;

                WatcherToken = new CancellationTokenSource();
                Task.Run(() => WatcherLoop(WatcherToken.Token));
            }
            catch
            {
                StopWatcher();
            }
        }

        private static void StopWatcher()
        {
            try
            {
                WatcherToken?.Cancel();
                WatcherToken = null;

                if (Watcher != null)
                {
                    Watcher.EnableRaisingEvents = false;
                    Watcher.Changed -= OnConfigChanged;
                    Watcher.Created -= OnConfigChanged;
                    Watcher.Deleted -= OnConfigChanged;
                    Watcher.Renamed -= OnConfigChanged;
                    Watcher.Dispose();
                    Watcher = null;
                }

                PendingReapplyFlag = 0;
            }
            catch { }
        }

        private static void OnConfigChanged(object sender, FileSystemEventArgs e)
        {
            if (IsInternalApplyInProgress) return;
            if (State == null || !State.IsEnforcedActive) return;
            if (SessionHandler.IsAdmin) return;

            Interlocked.Exchange(ref PendingReapplyFlag, 1);
        }

        private static void WatcherLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                Thread.Sleep(250);

                if (State == null || !State.IsEnforcedActive) continue;

                if (Interlocked.CompareExchange(ref PendingReapplyFlag, 0, 1) == 1)
                {
                    if ((DateTime.UtcNow - LastReapplyUtc).TotalMilliseconds < 1000) continue;
                    LastReapplyUtc = DateTime.UtcNow;

                    try
                    {
                        ReapplyProfileToDiskIfActive(softReload: false);
                    }
                    catch { }
                }
            }
        }

        [OnSessionEnd]
        private static void OnSessionEnd_StopWatcherOnly()
        {
            StopWatcher();
        }
    }
}