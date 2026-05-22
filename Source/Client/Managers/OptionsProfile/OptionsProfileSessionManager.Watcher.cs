using GameClient.Misc;
using Shared;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GameClient.Managers
{
    /// <summary>
    /// Filesystem watcher that reverts user edits to enforced config files
    /// while a server profile is active. Async polling loop coalesces
    /// rapid event bursts into a single reapply.
    /// </summary>
    public static partial class OptionsProfileSessionManager
    {
        private static FileSystemWatcher Watcher;
        private static CancellationTokenSource WatcherToken;
        private static int PendingReapplyFlag;
        private static DateTime LastReapplyUtc = DateTime.MinValue;

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
                _ = WatcherLoopAsync(WatcherToken.Token);
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

        private static async Task WatcherLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try { await Task.Delay(250, token); }
                catch (TaskCanceledException) { return; }

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
