using GameClient.Core;
using GameClient.Core.Configs;
using GameClient.Dialogs;
using GameClient.Dialogs.Default;
using Shared;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace GameClient.Misc
{
    public static class LocalServerHandler
    {
        public static string DefaultDownloadUrlTemplate => Shared.KMHProject.GitHubLatestServerZipTemplate;

        public static string DownloadUrl
        {
            get
            {
                string @override = ModConfigGetter.LocalServerDownloadUrl;
                if (!string.IsNullOrWhiteSpace(@override)) return @override;
                string rid = CurrentRid;
                if (string.IsNullOrEmpty(rid)) return string.Empty;
                return DefaultDownloadUrlTemplate.Replace("{RID}", rid);
            }
            set => ModConfigGetter.LocalServerDownloadUrl = value ?? string.Empty;
        }

        public static bool HasBundle => !string.IsNullOrEmpty(FindBundleExecutable());
        public static bool HasDownloadUrl => !string.IsNullOrWhiteSpace(DownloadUrl);
        public static bool IsAvailable => CurrentRid != null && (HasBundle || HasDownloadUrl);

        public const string RidWin64 = "win-x64";
        public const string RidLinux64 = "linux-x64";
        public const string RidOsxX64 = "osx-x64";
        public const string RidOsxArm64 = "osx-arm64";

        public static string CurrentRid => _ridCache ?? (_ridCache = ResolveCurrentRid());
        private static string _ridCache;

        private static string ResolveCurrentRid()
        {
            // RuntimeInformation throws on some older Mono builds; fall
            // back to PlatformID so Windows still detects correctly.
            try
            {
                var os = System.Runtime.InteropServices.RuntimeInformation.OSDescription ?? string.Empty;
                bool isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
                bool isLinux   = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
                bool isOsx     = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX);
                var arch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture;

                if (isWindows && arch == System.Runtime.InteropServices.Architecture.X64) return RidWin64;
                if (isLinux   && arch == System.Runtime.InteropServices.Architecture.X64) return RidLinux64;
                if (isOsx     && arch == System.Runtime.InteropServices.Architecture.X64) return RidOsxX64;
                if (isOsx     && arch == System.Runtime.InteropServices.Architecture.Arm64) return RidOsxArm64;

                Printer.Message($"[LocalServer] Unsupported platform: OS={os}, arch={arch}", Printer.LogImportanceMode.Verbose);
                return null;
            }
            catch (Exception ex)
            {
                Printer.Message($"[LocalServer] RuntimeInformation failed ({ex.Message}); falling back to PlatformID", Printer.LogImportanceMode.Verbose);
                if (Environment.OSVersion.Platform == PlatformID.Win32NT) return RidWin64;
                return null;
            }
        }

        public static string ServerEntryFileName => (CurrentRid == RidWin64) ? "GameServer.exe" : "GameServer";

        public const string BundleFolderName = "LocalServer";
        public const string ServerExeName = "GameServer.exe"; // legacy alias
        private const string ZipFileName = "server.zip";

        // DLG_Wait is a global singleton; two simultaneous installs
        // would clobber its Instance ref and race on the install dir.
        // Interlocked guard is lock-free and cleared in finally.
        private static int _installInProgress;
        public static bool IsInstalling => Interlocked.CompareExchange(ref _installInProgress, 0, 0) != 0;
        private static bool TryBeginInstall() => Interlocked.CompareExchange(ref _installInProgress, 1, 0) == 0;
        private static void EndInstall() => Interlocked.Exchange(ref _installInProgress, 0);

        private static void ShowAlreadyInstallingDialog()
        {
            DLG_Base.PushNewDialog(new DLG_Message("Local server",
                new[] { "An install is already in progress.", "Wait for it to finish before starting another." }));
        }

        public static void ManageLocalServer()
        {
            string installed = FindInstalledExecutable();
            if (!string.IsNullOrEmpty(installed))
            {
                OpenInstalledMenu(installed);
                return;
            }

            if (HasBundle)
            {
                DLG_Base.PushNewDialog(new DLG_YesNo("Install the bundled KMH server to your AppData folder?", InstallFromBundle, null));
                return;
            }
            if (HasDownloadUrl)
            {
                DLG_Base.PushNewDialog(new DLG_YesNo("A KMH server build will be downloaded. Continue?", InstallFromDownload, null));
                return;
            }

            string ridLabel = CurrentRid ?? "<your platform>";
            string entryLabel = (CurrentRid != null) ? ServerEntryFileName : "GameServer.exe (Windows) or GameServer (Linux/macOS)";
            DLG_Base.PushNewDialog(new DLG_Message("Host Local Server",
                new[]
                {
                    "This feature has no server source available.",
                    "",
                    "KMH expects either:",
                    $"  • A bundled server at <ModRoot>/{BundleFolderName}/{ridLabel}/{entryLabel}",
                    "  • A download URL set in Mod Settings → RimWorld Together → Self-host",
                }));
        }

        private static void OpenInstalledMenu(string exePath)
        {
            string installDir = Path.GetDirectoryName(exePath) ?? Master.AppdataLocalServerPath;

            var opts = new List<Verse.FloatMenuOption>
            {
                new Verse.FloatMenuOption("Launch server now", () => LaunchServer(exePath)),
                new Verse.FloatMenuOption("Edit configs (ServerConfig, ActionConfig, …)", () => ShowConfigsMenu(installDir)),
                new Verse.FloatMenuOption("Open server folder", OpenExplorer),
            };

            if (HasBundle)
            {
                opts.Add(new Verse.FloatMenuOption("Reinstall from bundle (overwrite)", () =>
                {
                    DLG_Base.PushNewDialog(new DLG_YesNo(
                        "Reinstall will erase the current server folder and re-copy from the mod bundle. Continue?",
                        InstallFromBundle, null));
                }));
            }
            if (HasDownloadUrl)
            {
                opts.Add(new Verse.FloatMenuOption("Reinstall by download (overwrite)", () =>
                {
                    DLG_Base.PushNewDialog(new DLG_YesNo(
                        "Reinstall will erase the current server folder and re-download. Continue?",
                        InstallFromDownload, null));
                }));
            }

            Verse.Find.WindowStack.Add(new Verse.FloatMenu(opts));
        }

        private static void LaunchServer(string entryPath)
        {
            string workDir = Path.GetDirectoryName(entryPath) ?? Master.AppdataLocalServerPath;
            EnsureExecutableBit(entryPath);

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = entryPath,
                    WorkingDirectory = workDir,
                    UseShellExecute = true,
                });

                DLG_Base.PushNewDialog(new DLG_Message("Local server",
                    new[]
                    {
                        "Server launching in its own window.",
                        "Use the console to issue commands.",
                        "",
                        "On Linux/macOS the binary may run without a visible terminal — check the install folder for logs if you don't see a window."
                    }));
            }
            catch (Exception ex)
            {
                Printer.Warning($"[LocalServer] Launch failed: {ex.Message}");
                DLG_Base.PushNewDialog(new DLG_Message("Error", new[] { "Could not launch the server.", ex.Message }));
            }
        }

        // Steam Workshop + zip extracts on Linux/macOS strip POSIX exec
        // bits, and net472 has no File.SetUnixFileMode, so shell out
        // to chmod. Windows skips this entirely.
        private static void EnsureExecutableBit(string path)
        {
            if (CurrentRid == RidWin64) return;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            try
            {
                using (var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x \"{path}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }))
                {
                    if (p != null) p.WaitForExit(5000);
                }
            }
            catch (Exception ex)
            {
                Printer.Warning($"[LocalServer] chmod +x failed for {path}: {ex.Message}");
            }
        }

        private static void InstallFromBundle()
        {
            if (!TryBeginInstall()) { ShowAlreadyInstallingDialog(); return; }

            string bundleDir = GetBundleSourceDirectory();
            if (string.IsNullOrEmpty(bundleDir))
            {
                EndInstall();
                string rid = CurrentRid ?? "unknown";
                DLG_Base.PushNewDialog(new DLG_Message("Error",
                    new[]
                    {
                        $"No bundled server found for platform '{rid}'.",
                        $"Expected at <ModRoot>/{BundleFolderName}/{rid}/{ServerEntryFileName}",
                        "",
                        "If you're on a platform KMH doesn't bundle, set a download URL in Mod Settings → RimWorld Together → Self-host."
                    }));
                return;
            }

            string targetDir = GetInstallTargetDirectory();
            DLG_Wait wait = new DLG_Wait("Installing bundled server");
            DLG_Base.PushNewDialog(wait);

            Task.Run(() =>
            {
                try
                {
                    EnsureFolderExists();
                    WipeRidInstallDir(targetDir);

                    MainThreadHandler.Instance.Enqueue(() => wait.UpdateDescription("Copying files"));
                    CopyDirectory(bundleDir, targetDir);

                    // Pre-chmod so the first "Launch" click doesn't need to.
                    EnsureExecutableBit(Path.Combine(targetDir, ServerEntryFileName));

                    MainThreadHandler.Instance.Enqueue(() =>
                    {
                        try { DLG_Wait.Instance?.Close(); } catch { }
                        string installed = FindInstalledExecutable();
                        if (!string.IsNullOrEmpty(installed)) OpenInstalledMenu(installed);
                        else
                        {
                            DLG_Base.PushNewDialog(new DLG_Message("Error",
                                new[] { "Copy finished but the server executable wasn't found in the install dir.", "Check the bundle layout in your mod folder." }));
                        }
                    });
                }
                catch (Exception ex)
                {
                    Printer.Warning($"[LocalServer] Bundle install failed: {ex.Message}");
                    MainThreadHandler.Instance.Enqueue(() =>
                    {
                        try { DLG_Wait.Instance?.Close(); } catch { }
                        DLG_Base.PushNewDialog(new DLG_Message("Error", new[] { "Bundle install failed.", ex.Message }));
                    });
                }
                finally { EndInstall(); }
            });
        }

        // Wipes only the current RID's sub-folder so a multi-OS player
        // can reinstall one platform without nuking the others.
        private static void WipeRidInstallDir(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                foreach (string file in Directory.GetFiles(dir)) { try { File.Delete(file); } catch { } }
                foreach (string sub in Directory.GetDirectories(dir)) { try { Directory.Delete(sub, recursive: true); } catch { } }
            }
            catch (Exception ex)
            {
                Printer.Warning($"[LocalServer] WipeRidInstallDir warning: {ex.Message}");
            }
        }

        private static void InstallFromDownload()
        {
            if (!TryBeginInstall()) { ShowAlreadyInstallingDialog(); return; }

            if (!HasDownloadUrl)
            {
                EndInstall();
                DLG_Base.PushNewDialog(new DLG_Message("Error",
                    new[] { "No download URL configured.", "Set one in Mod Settings → RimWorld Together → Self-host." }));
                return;
            }

            DLG_Wait wait = new DLG_Wait("Downloading server");
            DLG_Base.PushNewDialog(wait);

            Task.Run(() =>
            {
                try
                {
                    EnsureFolderExists();
                    SetupTls();
                    DownloadServerWithProgress(wait);
                    MainThreadHandler.Instance.Enqueue(() => wait.UpdateDescription("Extracting server"));
                    UnzipServer();
                    MainThreadHandler.Instance.Enqueue(() => wait.UpdateDescription("Cleaning up"));
                    Cleanup();

                    string maybeInstalled = FindInstalledExecutable();
                    if (!string.IsNullOrEmpty(maybeInstalled)) EnsureExecutableBit(maybeInstalled);

                    MainThreadHandler.Instance.Enqueue(() =>
                    {
                        try { DLG_Wait.Instance?.Close(); } catch { }
                        string installed = FindInstalledExecutable();
                        if (!string.IsNullOrEmpty(installed)) OpenInstalledMenu(installed);
                        else
                        {
                            DLG_Base.PushNewDialog(new DLG_Message("Error",
                                new[] { "Download finished but the server executable wasn't found in the extracted files.", "The zip's layout may have changed." }));
                        }
                    });
                }
                catch (Exception ex)
                {
                    Printer.Warning($"[LocalServer] Install failed: {ex.Message}");
                    MainThreadHandler.Instance.Enqueue(() =>
                    {
                        try { DLG_Wait.Instance?.Close(); } catch { }
                        DLG_Base.PushNewDialog(new DLG_Message("Error", new[] { "Local server install failed.", ex.Message }));
                    });
                }
                finally { EndInstall(); }
            });
        }

        private static void EnsureFolderExists()
        {
            if (!Directory.Exists(Master.AppdataLocalServerPath))
                Directory.CreateDirectory(Master.AppdataLocalServerPath);
        }

        // Walks per-RID first, then a flat fallback (for pre-2.7 layouts),
        // then one level deep (for publish wrapper folders).
        private static string FindInstalledExecutable()
        {
            try
            {
                if (!Directory.Exists(Master.AppdataLocalServerPath)) return string.Empty;
                string rid = CurrentRid;
                if (rid == null) return string.Empty;
                string entryName = ServerEntryFileName;

                string ridDir = Path.Combine(Master.AppdataLocalServerPath, rid);
                if (Directory.Exists(ridDir))
                {
                    string ridEntry = Path.Combine(ridDir, entryName);
                    if (File.Exists(ridEntry)) return ridEntry;
                }

                string flat = Path.Combine(Master.AppdataLocalServerPath, entryName);
                if (File.Exists(flat)) return flat;

                foreach (string dir in Directory.GetDirectories(Master.AppdataLocalServerPath))
                {
                    string nested = Path.Combine(dir, entryName);
                    if (File.Exists(nested)) return nested;
                }
                return string.Empty;
            }
            catch { return string.Empty; }
        }

        private static string FindBundleExecutable()
        {
            try
            {
                string modRoot = Master.ModMainPath;
                if (string.IsNullOrEmpty(modRoot)) return string.Empty;
                string bundleRoot = Path.Combine(modRoot, BundleFolderName);
                if (!Directory.Exists(bundleRoot)) return string.Empty;
                string rid = CurrentRid;
                if (rid == null) return string.Empty;
                string entryName = ServerEntryFileName;

                string ridDir = Path.Combine(bundleRoot, rid);
                if (Directory.Exists(ridDir))
                {
                    string ridEntry = Path.Combine(ridDir, entryName);
                    if (File.Exists(ridEntry)) return ridEntry;
                }

                string flat = Path.Combine(bundleRoot, entryName);
                if (File.Exists(flat)) return flat;

                foreach (string dir in Directory.GetDirectories(bundleRoot))
                {
                    string nested = Path.Combine(dir, entryName);
                    if (File.Exists(nested)) return nested;
                }
                return string.Empty;
            }
            catch { return string.Empty; }
        }

        private static string GetBundleSourceDirectory()
        {
            string entry = FindBundleExecutable();
            if (string.IsNullOrEmpty(entry)) return string.Empty;
            return Path.GetDirectoryName(entry) ?? string.Empty;
        }

        private static string GetInstallTargetDirectory()
        {
            string rid = CurrentRid;
            if (rid == null) return Master.AppdataLocalServerPath;
            return Path.Combine(Master.AppdataLocalServerPath, rid);
        }

        // Hand-rolled recursive copy so behaviour is identical across
        // Windows / macOS / Linux (vs shelling out to robocopy/xcopy).
        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string target = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, target, overwrite: true);
            }
            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
            }
        }

        // Modern GitHub release endpoints reject TLS < 1.2. net472
        // defaults to TLS 1.0 on older Windows builds without this.
        private static void SetupTls()
        {
            try
            {
                ServicePointManager.SecurityProtocol |=
                    SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;
            }
            catch { }
        }

        private static void DownloadServerWithProgress(DLG_Wait wait)
        {
            string zipPath = Path.Combine(Master.AppdataLocalServerPath, ZipFileName);
            using (WebClient webClient = new WebClient())
            {
                webClient.Headers.Add("User-Agent", $"KMH/{CommonValues.ExecutableVersion}");

                long lastShownPercent = -1;
                webClient.DownloadProgressChanged += (s, e) =>
                {
                    // Throttle to whole-percent ticks so we don't enqueue
                    // a main-thread update on every byte.
                    if (e.ProgressPercentage == lastShownPercent) return;
                    lastShownPercent = e.ProgressPercentage;

                    long mbReceived = e.BytesReceived / 1024 / 1024;
                    long mbTotal = e.TotalBytesToReceive / 1024 / 1024;
                    string label = e.TotalBytesToReceive > 0
                        ? $"Downloading {e.ProgressPercentage}% ({mbReceived}/{mbTotal} MB)"
                        : $"Downloading ({mbReceived} MB)";
                    try { MainThreadHandler.Instance.Enqueue(() => wait.UpdateDescription(label)); }
                    catch { }
                };

                webClient.DownloadFile(DownloadUrl, zipPath);
            }
        }

        private static void UnzipServer()
        {
            string zipPath = Path.Combine(Master.AppdataLocalServerPath, ZipFileName);
            if (!File.Exists(zipPath)) throw new FileNotFoundException("Downloaded zip is missing", zipPath);

            string targetDir = GetInstallTargetDirectory();
            WipeRidInstallDir(targetDir);
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            ZipFile.ExtractToDirectory(zipPath, targetDir);
        }

        private static void Cleanup()
        {
            string zipPath = Path.Combine(Master.AppdataLocalServerPath, ZipFileName);
            try { File.Delete(zipPath); } catch { }
        }

        private static void OpenExplorer()
        {
            try { Process.Start(Master.AppdataLocalServerPath); }
            catch (Exception ex)
            {
                Printer.Warning($"[LocalServer] Could not open explorer: {ex.Message}");
            }
        }

        private static void ShowConfigsMenu(string installDir)
        {
            string configsDir = Path.Combine(installDir, "Configs");
            if (!Directory.Exists(configsDir))
            {
                DLG_Base.PushNewDialog(new DLG_Message("Configs not found",
                    new[]
                    {
                        "The server hasn't created its config files yet.",
                        "",
                        "Click \"Launch server now\" once to let the server generate them. Stop the server (Ctrl+C in its window), then come back here and \"Edit configs\" will list them.",
                        "",
                        $"Expected at: {configsDir}"
                    }));
                return;
            }

            string[] files;
            try { files = Directory.GetFiles(configsDir, "*.json"); }
            catch (Exception ex)
            {
                Printer.Warning($"[LocalServer] Could not list configs: {ex.Message}");
                DLG_Base.PushNewDialog(new DLG_Message("Error", new[] { "Could not read the configs folder.", ex.Message }));
                return;
            }

            if (files.Length == 0)
            {
                try { Process.Start(configsDir); } catch { }
                DLG_Base.PushNewDialog(new DLG_Message("Configs",
                    new[] { "No config files found yet. Opening the folder so you can check." }));
                return;
            }

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            var opts = new List<Verse.FloatMenuOption>();
            foreach (string file in files)
            {
                string display = Path.GetFileName(file);
                opts.Add(new Verse.FloatMenuOption(display, () => OpenConfigFile(file)));
            }
            opts.Add(new Verse.FloatMenuOption("— Open Configs folder —", () =>
            {
                try { Process.Start(configsDir); }
                catch (Exception ex)
                {
                    Printer.Warning($"[LocalServer] Could not open configs folder: {ex.Message}");
                }
            }));

            Verse.Find.WindowStack.Add(new Verse.FloatMenu(opts));
        }

        private static void OpenConfigFile(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Printer.Warning($"[LocalServer] Could not open config '{path}': {ex.Message}");
                DLG_Base.PushNewDialog(new DLG_Message("Error",
                    new[]
                    {
                        "Could not open the config file in your default editor.",
                        ex.Message,
                        "",
                        $"You can edit it manually at: {path}"
                    }));
            }
        }
    }
}
