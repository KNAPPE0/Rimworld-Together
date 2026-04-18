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

        private static string ActiveMarkerPath => Path.Combine(ConfigPath, "RWT_ACTIVE_PROFILE.txt");

        private static bool Bootstrapped;
        private static SessionState State;

        private static readonly Dictionary<string, IncomingProfileBuffer> IncomingProfiles =
            new Dictionary<string, IncomingProfileBuffer>(StringComparer.OrdinalIgnoreCase);

        private static FileSystemWatcher Watcher;
        private static CancellationTokenSource WatcherToken;
        private static int PendingReapplyFlag;
        private static DateTime LastReapplyUtc = DateTime.MinValue;
        private static bool IsApplying;

        private static int PendingManualRequest;

        private class SessionState
        {
            public bool IsEnforcedActive { get; set; } = false;
            public string ActiveProfileHash { get; set; } = string.Empty;
            public long ActiveProfileUpdatedUtcTicks { get; set; } = 0;

            public long LastAppliedUtcTicks { get; set; } = 0;
            public int LastAppliedFileCount { get; set; } = 0;
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

                // KMH: If enforcement is active, keep it - the user will rejoin the server.
                // Only restore if there's an explicit crash marker file indicating a failed apply.
                string crashMarker = Path.Combine(RootPath, "RWT_CRASH_DURING_APPLY");
                if (File.Exists(crashMarker) && Directory.Exists(BackupPath))
                {
                    Printer.Warning("[OptionsProfile] Detected crash during profile apply - restoring backup.");
                    RestoreBackupToConfig(softReload: true);
                    ClearSessionState();
                    SafeDeleteDirectory(BackupPath);
                EnforcementGuard.ClearMarker();
                    try { File.Delete(crashMarker); } catch { }
                }
            }
            catch (Exception e)
            {
                Printer.Warning($"[OptionsProfile] Bootstrap failed: {e}");
            }
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

        /// <summary>Returns true if a personal config backup exists.</summary>
        public static bool HasBackup()
        {
            try { return Directory.Exists(BackupPath); }
            catch { return false; }
        }

        /// <summary>Returns true if enforcement is currently active.</summary>
        public static bool IsEnforcementActive()
        {
            // Delegate to EnforcementGuard for reliable detection
            return EnforcementGuard.IsActive;
        }

        /// <summary>Returns the currently applied profile hash, or empty.</summary>
        public static string GetActiveHash()
        {
            return State?.ActiveProfileHash ?? string.Empty;
        }

        public static void OnServerEnforcementReceived()
        {
            Printer.Warning("[OptionsProfile] Server enforcement received! Requesting profile...");
            RequestServerOptionsProfile(isManual: false);
        }

        public static void RequestServerOptionsProfile(bool isManual)
        {
            try
            {
                if (isManual) Interlocked.Exchange(ref PendingManualRequest, 1);

                if (Network.ServerEndpoint == null) return;

                PKT_ModConfig req = new PKT_ModConfig
                {
                    _requestOptionsProfile = true,
                    _stepMode = PKT_ModConfig.ModConfigStepMode.Send
                };

                Network.ServerEndpoint.EnqueuePacket(PacketHeader.ModManager, req);
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

                // Update local enforcement state so the admin sees it immediately
                SessionHandler.CurrentModConfig.IsEnforced = true;

                DLG_Base.PushNewDialog(new DLG_Message("Profile",
                    new[] { "Published server options profile.", "Enforcement is now ACTIVE.", $"Hash: {hash}", $"Chunks: {chunks.Count}" }));
            }
            catch (Exception e)
            {
                Printer.Warning($"[OptionsProfile] PublishCurrentConfigProfileToServer failed: {e}");
                DLG_Base.PushNewDialog(new DLG_Message("Error", new[] { "Failed to publish. Check logs." }));
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
                    MainThreadHandler.Instance.Enqueue(delegate {
                        DLG_Base.PushNewDialog(new DLG_Message("Profile",
                            new[] { "Server has no options profile set yet.", "Ask an admin to publish one." }));
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

            if (buffer.ReceivedCount >= buffer.ChunkCount)
            {
                byte[] full = Combine(buffer.Chunks);
                IncomingProfiles.Remove(buffer.Hash);

                string computed = ConfigProfileUtility.Sha256Hex(full);
                if (!string.Equals(computed, buffer.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    MainThreadHandler.Instance.Enqueue(delegate {
                        DLG_Base.PushNewDialog(new DLG_Message("Error",
                            new[] { "Options profile download failed (hash mismatch)." }));
                    });
                    return;
                }

                string profileFolder = GetProfileFolder(buffer.Hash);

                SafeDeleteDirectory(profileFolder);
                Directory.CreateDirectory(profileFolder);

                ConfigProfileUtility.ExtractZipBytesToFolder(full, profileFolder);

                int extractedCount = SafeCountFiles(profileFolder);

                // KMH: Check if we already have this profile applied - skip if same hash
                bool stateActive = State != null && State.IsEnforcedActive;
                bool hashMatch = stateActive && string.Equals(State.ActiveProfileHash, buffer.Hash, StringComparison.OrdinalIgnoreCase);
                Printer.Warning($"[OptionsProfile] Hash check: StateActive={stateActive} HashMatch={hashMatch} StateHash={State?.ActiveProfileHash} BufferHash={buffer.Hash}");
                if (hashMatch)
                {
                    bool wasManual = Interlocked.Exchange(ref PendingManualRequest, 0) == 1;
                    if (wasManual)
                        MainThreadHandler.Instance.Enqueue(delegate {
                            DLG_Base.PushNewDialog(new DLG_Message("Profile", 
                                new[] { "Server profile already applied.", $"Hash: {buffer.Hash}" }));
                        });
                    return;
                }

                Printer.Warning($"[OptionsProfile] All chunks received. Applying profile. Hash={buffer.Hash} Files={extractedCount}");
                // Must run on main thread for UI dialogs
                string _pf = profileFolder; string _h = buffer.Hash; long _t = buffer.UpdatedTicks; int _ec = extractedCount;
                MainThreadHandler.Instance.Enqueue(delegate { ApplyEnforcedProfile(_pf, _h, _t, _ec); });
            }
        }

        public static void TryRestoreOnDisconnect()
        {
            try
            {
                // KMH: Do NOT restore configs on disconnect - user keeps server configs
                // until they manually restore or join a different server.
                // Only stop the filesystem watcher.
                StopWatcher();
            }
            catch (Exception e)
            {
                Printer.Warning($"[OptionsProfile] TryRestoreOnDisconnect failed: {e}");
            }
        }

        public static void RestorePersonalConfigsManual()
        {
            try
            {
                if (!Directory.Exists(BackupPath))
                {
                    DLG_Base.PushNewDialog(new DLG_Message("Profile", new[] { "No backup found." }));
                    return;
                }

                StopWatcher();

                RestoreBackupToConfig(softReload: true);
                ClearSessionState();
                SafeDeleteDirectory(BackupPath);
                EnforcementGuard.ClearMarker();

                DLG_Base.PushNewDialog(new DLG_Message("Profile",
                    new[] { "Restored personal configs.", "A restart is recommended to fully apply." },
                    onConfirm: delegate
                    {
                        try { GenCommandLine.Restart(); }
                        catch { Verse.Root.Shutdown(); }
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

                int copied = ApplyProfileFolderToConfig_NonDestructive(profileFolder, ConfigProfileUtility.DefaultExcludeFiles);
                WriteActiveMarker(State.ActiveProfileHash, State.ActiveProfileUpdatedUtcTicks, copied);

                if (softReload) TrySoftReloadModSettings();
            }
            catch (Exception e)
            {
                Printer.Warning($"[OptionsProfile] ReapplyProfileToDiskIfActive failed: {e}");
            }
        }

        private static void ApplyEnforcedProfile(string profileFolder, string hash, long updatedTicks, int extractedCount)
        {
            try
            {
                Directory.CreateDirectory(ConfigPath);
                Directory.CreateDirectory(ProfilesRoot);

                EnsureBackupExists();

                // Write crash marker - if game crashes during apply, Bootstrap will restore
                string crashMarker = Path.Combine(RootPath, "RWT_CRASH_DURING_APPLY");
                File.WriteAllText(crashMarker, DateTime.UtcNow.ToString("o"));

                int copiedCount = ApplyProfileFolderToConfig_NonDestructive(profileFolder, ConfigProfileUtility.DefaultExcludeFiles);

                // Remove crash marker - apply succeeded
                try { File.Delete(crashMarker); } catch { }

                State.IsEnforcedActive = true;
                State.ActiveProfileHash = hash;
                State.ActiveProfileUpdatedUtcTicks = updatedTicks;
                State.LastAppliedUtcTicks = DateTime.UtcNow.Ticks;
                State.LastAppliedFileCount = copiedCount;
                SaveState();

                WriteActiveMarker(hash, updatedTicks, copiedCount);

                TrySoftReloadModSettings();

                StartWatcher();

                // KMH: Write enforcement marker
                Printer.Warning($"[OptionsProfile] Writing enforcement marker. Hash={hash}");
                EnforcementGuard.MarkEnforced(hash);

                // KMH: Force restart after enforcement profile applied
                Action doRestart = delegate
                {
                    try { GenCommandLine.Restart(); }
                    catch { Verse.Root.Shutdown(); }
                };

                DLG_Base.PushNewDialog(new DLG_Message("Server Config Enforced",
                    new[] { "Server config profile has been applied.", $"Files: {copiedCount} | Hash: {hash}", "The game must restart to apply changes." },
                    doRestart));
            }
            catch (Exception e)
            {
                Printer.Warning($"[OptionsProfile] ApplyEnforcedProfile failed: {e}");
                DLG_Base.PushNewDialog(new DLG_Message("Error", new[] { "Failed to apply options profile. Check logs." }));
            }
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

            if (softReload) TrySoftReloadModSettings();
        }

        private static int ApplyProfileFolderToConfig_NonDestructive(string profileFolder, HashSet<string> excludeFiles)
        {
            Directory.CreateDirectory(ConfigPath);

            int copied = 0;

            foreach (string dir in Directory.GetDirectories(profileFolder, "*", SearchOption.AllDirectories))
            {
                string rel = MakeRelative(profileFolder, dir);
                if (string.IsNullOrEmpty(rel)) continue;

                string targetDir = Path.Combine(ConfigPath, rel);
                Directory.CreateDirectory(targetDir);
            }

            foreach (string file in Directory.GetFiles(profileFolder, "*", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (excludeFiles != null && excludeFiles.Contains(name)) continue;

                string rel = MakeRelative(profileFolder, file);
                if (string.IsNullOrEmpty(rel)) continue;

                string dest = Path.Combine(ConfigPath, rel);
                string destFolder = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(destFolder)) Directory.CreateDirectory(destFolder);

                File.Copy(file, dest, overwrite: true);
                copied++;
            }

            return copied;
        }

        private static void WriteActiveMarker(string hash, long updatedTicks, int copiedFiles)
        {
            try
            {
                Directory.CreateDirectory(ConfigPath);

                string lines =
                    $"RWT Options Profile Active{Environment.NewLine}" +
                    $"Hash={hash}{Environment.NewLine}" +
                    $"UpdatedUtcTicks={updatedTicks}{Environment.NewLine}" +
                    $"AppliedUtc={DateTime.UtcNow:o}{Environment.NewLine}" +
                    $"CopiedFiles={copiedFiles}{Environment.NewLine}" +
                    $"ProfilesRoot={ProfilesRoot}{Environment.NewLine}" +
                    $"BackupPath={BackupPath}{Environment.NewLine}";

                File.WriteAllText(ActiveMarkerPath, lines);
            }
            catch { }
        }

        private static int SafeCountFiles(string folder)
        {
            try
            {
                if (!Directory.Exists(folder)) return 0;
                return Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Length;
            }
            catch
            {
                return 0;
            }
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
            try { Serializer.SerializeToFile(StatePath, State); }
            catch { }
        }

        private static void ClearSessionState()
        {
            State.IsEnforcedActive = false;
                EnforcementGuard.ClearMarker();
            State.ActiveProfileHash = string.Empty;
            State.ActiveProfileUpdatedUtcTicks = 0;
            State.LastAppliedUtcTicks = 0;
            State.LastAppliedFileCount = 0;
            SaveState();

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
            for (int i = 0; i < parts.Length; i++) total += parts[i]?.Length ?? 0;

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
            if (string.IsNullOrEmpty(sourceDir) || string.IsNullOrEmpty(targetDir)) return;
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
                if (!string.IsNullOrEmpty(destFolder)) Directory.CreateDirectory(destFolder);

                File.Copy(file, dest, overwrite: true);
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
            try { Directory.Delete(dir, recursive: true); } catch { }
        }

        private static string MakeRelative(string root, string fullPath)
        {
            string r = Path.GetFullPath(root);
            if (!r.EndsWith(Path.DirectorySeparatorChar.ToString())) r += Path.DirectorySeparatorChar;

            string f = Path.GetFullPath(fullPath);
            if (!f.StartsWith(r, StringComparison.OrdinalIgnoreCase)) return string.Empty;

            return f.Substring(r.Length);
        }

        private static void StartWatcher()
        {
            try
            {
                StopWatcher();

                if (SessionHandler.CurrentNetworkState == ClientNetwork.ClientNetworkState.Disconnected) return;
                if (State == null || !State.IsEnforcedActive) return;
                if (!Directory.Exists(ConfigPath)) return;

                Watcher = new FileSystemWatcher(ConfigPath)
                {
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.DirectoryName
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
            if (IsApplying) return;
            if (State == null || !State.IsEnforcedActive) return;
            if (SessionHandler.CurrentNetworkState == ClientNetwork.ClientNetworkState.Disconnected) return;

            Interlocked.Exchange(ref PendingReapplyFlag, 1);
        }

        private static void WatcherLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                Thread.Sleep(250);

                if (State == null || !State.IsEnforcedActive) continue;
                if (SessionHandler.CurrentNetworkState == ClientNetwork.ClientNetworkState.Disconnected) continue;

                if (Interlocked.CompareExchange(ref PendingReapplyFlag, 0, 1) == 1)
                {
                    if ((DateTime.UtcNow - LastReapplyUtc).TotalMilliseconds < 1000) continue;
                    LastReapplyUtc = DateTime.UtcNow;

                    try
                    {
                        IsApplying = true;
                        ReapplyProfileToDiskIfActive(softReload: false);
                    }
                    finally
                    {
                        IsApplying = false;
                    }
                }
            }
        }

        [OnSessionEnd]
        private static void RestoreOnGameExit()
        {
            // KMH: Do NOT restore on game exit - keep server configs so the user
            // can restart and rejoin the same server without re-downloading.
            // Only stop the watcher to avoid issues.
            StopWatcher();
        }
    }
}