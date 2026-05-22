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
    /// <summary>
    /// KMH 26.5.22.1: One-click self-hosting for KMH.
    ///
    /// <para><b>Two sources, in priority order:</b></para>
    /// <list type="number">
    ///   <item><b>Bundled with the mod.</b> If the mod folder contains
    ///   <c>LocalServer/GameServer.exe</c> (or one level deep), it's
    ///   copied to %AppData% on first launch and used from there.
    ///   This is the preferred path — players never touch the network,
    ///   and the bundled build is guaranteed to match the KMH client
    ///   protocol because it shipped alongside.</item>
    ///   <item><b>Downloaded from a configured URL.</b> If no bundle is
    ///   present AND
    ///   <see cref="ModConfigGetter.LocalServerDownloadUrl"/> is set,
    ///   that URL is fetched and unzipped into %AppData%. Useful for
    ///   point-releases between mod versions, or for KMH-staff testing
    ///   a candidate server build without rebuilding the mod.</item>
    /// </list>
    ///
    /// <para>If neither path is available, the main-menu entry stays
    /// hidden and direct calls produce a friendly "feature not
    /// configured" message that explains exactly what KMH needs.</para>
    ///
    /// <para><b>Why both?</b> Bundled is zero-friction for players but
    /// requires re-shipping the mod for every server-only patch. URL is
    /// flexible but needs the player to be online and KMH staff to
    /// maintain a release endpoint. Supporting both means a normal KMH
    /// install just works (bundled), while operators can override with
    /// a URL when they need to.</para>
    ///
    /// <para><b>Bundling layout</b> (what KMH ships):</para>
    /// <code>
    /// {ModRoot}/
    ///   1.6/
    ///     Assemblies/   (client DLLs — already there)
    ///   LocalServer/
    ///     GameServer.exe
    ///     Shared.dll
    ///     TCPNetwork.dll
    ///     [...]
    /// </code>
    ///
    /// <para>Build the server with
    /// <c>dotnet publish -c Release -r win-x64 --self-contained false</c>
    /// and drop the publish output into <c>{ModRoot}/LocalServer/</c>.
    /// The <see cref="HasBundle"/> probe finds it automatically.</para>
    ///
    /// <para><b>KMH-vs-upstream protocol incompatibility</b>: KMH adds
    /// packet headers for treasury, marketplace, quests, guilds,
    /// leaderboards, and linked Discord accounts that vanilla RWT
    /// doesn't recognise. A KMH client connected to a vanilla RWT
    /// server gets immediate disconnect on the first KMH-specific
    /// packet. Both source paths above must point at a KMH-built
    /// server — that's why the URL is opt-in and there's no upstream
    /// default.</para>
    /// </summary>
    public static class LocalServerHandler
    {
        // ------------- Configuration accessors -------------

        /// <summary>
        /// KMH 26.5.22.1: Optional fallback download URL — used only
        /// when no bundle is found in the mod folder. Persisted via
        /// <see cref="ModConfigGetter"/>.
        /// </summary>
        public static string DownloadUrl
        {
            get => ModConfigGetter.LocalServerDownloadUrl ?? string.Empty;
            set => ModConfigGetter.LocalServerDownloadUrl = value ?? string.Empty;
        }

        /// <summary>True if the mod folder contains a usable bundle.</summary>
        public static bool HasBundle => !string.IsNullOrEmpty(FindBundleExecutable());

        /// <summary>True if a fallback download URL is configured.</summary>
        public static bool HasDownloadUrl =>
            !string.IsNullOrWhiteSpace(ModConfigGetter.LocalServerDownloadUrl);

        /// <summary>
        /// KMH 26.5.22.1: True if EITHER a bundle exists for the current
        /// platform OR a URL is configured. Cross-platform — KMH now
        /// ships per-RID sub-folders inside the bundle so the right
        /// build is picked at runtime.
        /// </summary>
        public static bool IsAvailable => CurrentRid != null && (HasBundle || HasDownloadUrl);

        // ------------- Platform detection (RID) -------------

        /// <summary>
        /// KMH 26.5.22.1: RuntimeIdentifier names that map onto
        /// <c>dotnet publish -r &lt;RID&gt;</c> output layouts. The
        /// bundle layout follows the same names so the same publish
        /// output drops in unchanged:
        /// <code>
        /// LocalServer/
        ///   win-x64/   ← Windows 64-bit
        ///   linux-x64/ ← Linux 64-bit
        ///   osx-x64/   ← macOS Intel
        ///   osx-arm64/ ← macOS Apple Silicon
        /// </code>
        /// </summary>
        public const string RidWin64 = "win-x64";
        public const string RidLinux64 = "linux-x64";
        public const string RidOsxX64 = "osx-x64";
        public const string RidOsxArm64 = "osx-arm64";

        /// <summary>
        /// KMH 26.5.22.1: The RID we're running on, or <c>null</c> if
        /// the current OS+architecture combo is unsupported. Cached
        /// once per process — Environment.OSVersion + IntPtr.Size are
        /// constant for the process lifetime, and macOS ARM detection
        /// reflects via P/Invoke which is fine but cheaper to memo.
        /// </summary>
        public static string CurrentRid => _ridCache ?? (_ridCache = ResolveCurrentRid());
        private static string _ridCache;

        private static string ResolveCurrentRid()
        {
            // KMH 26.5.22.1: .NET Standard 2.0 (which net472 supports)
            // ships System.Runtime.InteropServices.RuntimeInformation,
            // which gives us a reliable OS + architecture probe without
            // platform-specific P/Invoke. Wrap in try/catch because some
            // older Mono builds shipped a stub that throws on call.
            try
            {
                var os = System.Runtime.InteropServices.RuntimeInformation.OSDescription ?? string.Empty;
                bool isWindows = System.Runtime.InteropServices.RuntimeInformation
                    .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
                bool isLinux = System.Runtime.InteropServices.RuntimeInformation
                    .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
                bool isOsx = System.Runtime.InteropServices.RuntimeInformation
                    .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX);
                var arch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture;

                if (isWindows && arch == System.Runtime.InteropServices.Architecture.X64) return RidWin64;
                if (isLinux && arch == System.Runtime.InteropServices.Architecture.X64) return RidLinux64;
                if (isOsx && arch == System.Runtime.InteropServices.Architecture.X64) return RidOsxX64;
                if (isOsx && arch == System.Runtime.InteropServices.Architecture.Arm64) return RidOsxArm64;

                // Unknown / unsupported (e.g. 32-bit OS, ARM Linux).
                // RimWorld 1.6 itself is 64-bit-only, so this branch
                // should never fire in practice.
                Printer.Message($"[LocalServer] Unsupported platform: OS={os}, arch={arch}", Printer.LogImportanceMode.Verbose);
                return null;
            }
            catch (Exception ex)
            {
                // Fallback to the simple platform check — better than
                // hiding the feature entirely if RuntimeInformation
                // misbehaves on a niche runtime.
                Printer.Message($"[LocalServer] RuntimeInformation failed ({ex.Message}); falling back to PlatformID", Printer.LogImportanceMode.Verbose);
                if (Environment.OSVersion.Platform == PlatformID.Win32NT) return RidWin64;
                return null;
            }
        }

        /// <summary>
        /// KMH 26.5.22.1: Server binary filename — single-file
        /// self-contained publish output for every platform:
        /// <list type="bullet">
        ///   <item><b>Windows</b> — <c>GameServer.exe</c></item>
        ///   <item><b>Linux / macOS</b> — <c>GameServer</c> (no extension)</item>
        /// </list>
        /// All four RIDs ship as <c>dotnet publish ... --self-contained
        /// true -p:PublishSingleFile=true</c>, so the binary embeds the
        /// .NET 8 runtime and every dependency. Players need nothing
        /// installed — double-click and go.
        /// </summary>
        public static string ServerEntryFileName
        {
            get
            {
                if (CurrentRid == RidWin64) return "GameServer.exe";
                return "GameServer"; // Linux/macOS — no extension
            }
        }

        // ------------- Layout constants -------------

        /// <summary>Sub-folder inside the mod root that holds the bundled server.</summary>
        public const string BundleFolderName = "LocalServer";

        /// <summary>
        /// KMH 26.5.22.1: Legacy alias — older callers / docs may still
        /// reference <c>ServerExeName</c>. New code should use
        /// <see cref="ServerEntryFileName"/> for platform correctness.
        /// </summary>
        public const string ServerExeName = "GameServer.exe";

        private const string ZipFileName = "server.zip";

        // ------------- Re-entrancy guard -------------

        /// <summary>
        /// KMH 26.5.22.1: 0 = idle, 1 = install in progress.
        ///
        /// <para>The DLG_Wait dialog is a global singleton via
        /// <see cref="DLG_Wait.Instance"/>, so two concurrent installs
        /// would clobber each other's Instance reference — the first
        /// completion's <c>Close()</c> would close the second's dialog,
        /// leaving the first install with no UI to dismiss. The
        /// install dir is also shared, so two concurrent writes would
        /// race on the same files.</para>
        ///
        /// <para>Guarded with <see cref="Interlocked.CompareExchange"/>
        /// so the check is lock-free and atomic. Cleared on the main
        /// thread inside the finally block of each install path so
        /// failure modes (exception during download, etc.) still reset
        /// the flag.</para>
        /// </summary>
        private static int _installInProgress;

        /// <summary>
        /// KMH 26.5.22.1: True if an install is currently running.
        /// Surfaced primarily for diagnostics — callers should rely on
        /// <see cref="TryBeginInstall"/> / <see cref="EndInstall"/>
        /// rather than reading this directly.
        /// </summary>
        public static bool IsInstalling => Interlocked.CompareExchange(ref _installInProgress, 0, 0) != 0;

        /// <summary>
        /// KMH 26.5.22.1: Atomically claim the install slot. Returns
        /// true on success (caller may proceed with the install) and
        /// false if another install is already running (caller should
        /// no-op or show a "please wait" message).
        /// </summary>
        private static bool TryBeginInstall()
        {
            return Interlocked.CompareExchange(ref _installInProgress, 1, 0) == 0;
        }

        /// <summary>
        /// KMH 26.5.22.1: Release the install slot. Always called in a
        /// finally block so abnormal terminations (download failure,
        /// disk full, etc.) don't permanently lock out future installs.
        /// </summary>
        private static void EndInstall()
        {
            Interlocked.Exchange(ref _installInProgress, 0);
        }

        /// <summary>
        /// KMH 26.5.22.1: User-facing "please wait" dialog when the
        /// user clicks an install option while another install is
        /// already in progress. Centralised so both bundle and
        /// download paths share the message.
        /// </summary>
        private static void ShowAlreadyInstallingDialog()
        {
            DLG_Base.PushNewDialog(new DLG_Message("Local server",
                new[]
                {
                    "An install is already in progress.",
                    "Wait for it to finish before starting another."
                }));
        }

        // ------------- Entry point -------------

        /// <summary>
        /// Entry point — call from a UI button or gizmo. Dispatches to
        /// the right install / launch path based on what's available.
        /// </summary>
        public static void ManageLocalServer()
        {
            // Already installed in AppData → straight to launch menu.
            string installed = FindInstalledExecutable();
            if (!string.IsNullOrEmpty(installed))
            {
                OpenInstalledMenu(installed);
                return;
            }

            // Not installed — pick the best source.
            if (HasBundle)
            {
                DLG_Base.PushNewDialog(new DLG_YesNo(
                    "Install the bundled KMH server to your AppData folder?",
                    InstallFromBundle, null));
                return;
            }

            if (HasDownloadUrl)
            {
                DLG_Base.PushNewDialog(new DLG_YesNo(
                    "A KMH server build will be downloaded. Continue?",
                    InstallFromDownload, null));
                return;
            }

            // KMH 26.5.22.1: No source — show the helpful "how to
            // enable" message. The expected-path string uses CurrentRid +
            // ServerEntryFileName so Linux/macOS users see the correct
            // platform-specific filename (GameServer, no extension)
            // instead of the Windows .exe.
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
                    "",
                    "Upstream RimWorld Together's server doesn't speak KMH's protocol — KMH adds packet headers for treasury, marketplace, quests, guilds, leaderboards, and Discord linking that vanilla RWT doesn't recognise. Vanilla RWT's release zip would result in an immediately-broken host."
                }));
        }

        // ------------- Installed-server menu -------------

        /// <summary>
        /// KMH 26.5.22.1: FloatMenu for an already-installed server.
        /// Reinstall branches by source: if a bundle exists, recopy
        /// from there; otherwise redownload from the URL.
        ///
        /// <para>KMH 26.5.22.1: "Edit configs" entry opens a sub-menu
        /// listing the JSON config files in the server's working dir
        /// so players don't have to navigate to AppData and find them
        /// manually. Each entry opens the file in the system default
        /// editor (Notepad on Windows, TextEdit on macOS, xdg-open's
        /// pick on Linux).</para>
        /// </summary>
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

        /// <summary>
        /// KMH 26.5.22.1: Launch the installed server. All four RIDs
        /// ship as self-contained single-file binaries (no .NET runtime
        /// installation needed on the player's machine) so the launch
        /// is a direct exec — same path on every platform. The only
        /// platform-specific dance is ensuring the executable bit is
        /// set on Linux/macOS (Steam Workshop and zip extracts don't
        /// always preserve it), handled by
        /// <see cref="EnsureExecutableBit"/>.
        /// </summary>
        private static void LaunchServer(string entryPath)
        {
            string workDir = Path.GetDirectoryName(entryPath) ?? Master.AppdataLocalServerPath;

            // KMH 26.5.22.1: Guarantee +x on Linux/macOS before launch.
            // Idempotent — no-op on Windows, no-op if already set.
            EnsureExecutableBit(entryPath);

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = entryPath,
                    WorkingDirectory = workDir,
                    // UseShellExecute lets the OS open the binary in its
                    // own console window on Windows (matches a manual
                    // double-click). On Linux/macOS the behaviour is
                    // OS/desktop-environment dependent; the binary runs
                    // either way.
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
                DLG_Base.PushNewDialog(new DLG_Message("Error",
                    new[] { "Could not launch the server.", ex.Message }));
            }
        }

        /// <summary>
        /// KMH 26.5.22.1: Set the executable bit (+x) on the given file
        /// for non-Windows platforms. Steam Workshop downloads + zip
        /// extracts on macOS/Linux don't always preserve POSIX permission
        /// bits, so a freshly-installed binary needs <c>chmod +x</c>
        /// before it can be exec'd.
        ///
        /// <para>.NET Framework 4.7.2 (KMH client's target) doesn't have
        /// <c>File.SetUnixFileMode</c> (that's .NET 7+), so we shell out
        /// to the system <c>chmod</c> tool. Universal on Linux/macOS
        /// (POSIX requires it). Wrapped in try/catch — chmod failure
        /// shouldn't block launch on the off-chance the bit was already
        /// set by some other means.</para>
        /// </summary>
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
                    if (p != null) p.WaitForExit(5000); // 5s safety timeout
                }
            }
            catch (Exception ex)
            {
                Printer.Warning($"[LocalServer] chmod +x failed for {path}: {ex.Message}");
            }
        }

        // ------------- Bundled-source install -------------

        /// <summary>
        /// KMH 26.5.22.1: Copy the bundled server from
        /// <c>{ModRoot}/{BundleFolderName}/</c> into
        /// <see cref="Master.AppdataLocalServerPath"/> so the player
        /// has a writable copy. The mod folder may be read-only
        /// (Steam Workshop) so we never launch directly from there —
        /// the server writes logs, configs, saves, and so a writable
        /// install dir is required.
        ///
        /// Runs on a background thread to keep the UI responsive (large
        /// bundle = lots of file IO).
        /// </summary>
        private static void InstallFromBundle()
        {
            // KMH 26.5.22.1: Single-install gate. Returns immediately if
            // another install is already running (double-click, second
            // entry in a FloatMenu, etc.) — prevents DLG_Wait singleton
            // clash and install-dir race.
            if (!TryBeginInstall())
            {
                ShowAlreadyInstallingDialog();
                return;
            }

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
                        "If you're on a platform KMH doesn't bundle (e.g. Linux/macOS in some KMH releases), set a download URL in Mod Settings → RimWorld Together → Self-host."
                    }));
                return;
            }

            // KMH 26.5.22.1: Target = AppData/LocalServer/<RID>/ so the
            // on-disk layout mirrors the bundle and FindInstalledExecutable
            // picks it up. We only wipe the per-RID install sub-dir
            // (not the whole LocalServer dir) so installs for OTHER RIDs
            // — and importantly the player's ServerConfig.json / saves /
            // world data which live in the top-level LocalServer/Assets
            // path — survive the reinstall.
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

                    // KMH 26.5.22.1: Set +x on the target binary before
                    // the launch menu opens, so the FIRST click of
                    // "Launch server now" doesn't need to chmod (matters
                    // on macOS Workshop where the bit gets stripped
                    // during the Steam download).
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
                        DLG_Base.PushNewDialog(new DLG_Message("Error",
                            new[] { "Bundle install failed.", ex.Message }));
                    });
                }
                finally
                {
                    // KMH 26.5.22.1: Always release the install slot,
                    // even on exception, so a failed install doesn't
                    // permanently lock the user out of trying again.
                    EndInstall();
                }
            });
        }

        /// <summary>
        /// KMH 26.5.22.1: Erase the install target dir contents for the
        /// CURRENT RID only. Used before each bundle copy so re-installs
        /// don't accumulate stale files, while preserving installs for
        /// OTHER RIDs (a multi-OS player can have all four sub-folders
        /// in <c>%AppData%/.../LocalServer/</c> without each platform's
        /// reinstall wiping the others).
        /// </summary>
        private static void WipeRidInstallDir(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                foreach (string file in Directory.GetFiles(dir))
                {
                    try { File.Delete(file); } catch { }
                }
                foreach (string sub in Directory.GetDirectories(dir))
                {
                    try { Directory.Delete(sub, recursive: true); } catch { }
                }
            }
            catch (Exception ex)
            {
                Printer.Warning($"[LocalServer] WipeRidInstallDir warning: {ex.Message}");
            }
        }

        // ------------- Download-source install -------------

        /// <summary>
        /// KMH 26.5.22.1: Download the configured URL, unzip into the
        /// install dir, and surface the installed menu. Runs on a
        /// background thread; DLG_Wait shows live percent progress.
        /// </summary>
        private static void InstallFromDownload()
        {
            // KMH 26.5.22.1: Single-install gate — same reasoning as
            // InstallFromBundle above. Both paths share the gate so
            // toggling between Reinstall-by-bundle and Reinstall-by-download
            // can't accidentally start two parallel installs.
            if (!TryBeginInstall())
            {
                ShowAlreadyInstallingDialog();
                return;
            }

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

                    // KMH 26.5.22.1: Set +x on the extracted binary
                    // (zips don't reliably preserve POSIX bits).
                    string maybeInstalled = FindInstalledExecutable();
                    if (!string.IsNullOrEmpty(maybeInstalled))
                        EnsureExecutableBit(maybeInstalled);

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
                        DLG_Base.PushNewDialog(new DLG_Message("Error",
                            new[] { "Local server install failed.", ex.Message }));
                    });
                }
                finally
                {
                    // KMH 26.5.22.1: Always release the install slot so
                    // an interrupted/failed download doesn't permanently
                    // lock the user out.
                    EndInstall();
                }
            });
        }

        // ------------- File-system helpers -------------

        private static void EnsureFolderExists()
        {
            if (!Directory.Exists(Master.AppdataLocalServerPath))
                Directory.CreateDirectory(Master.AppdataLocalServerPath);
        }

        /// <summary>
        /// KMH 26.5.22.1: Find the installed server entry point for the
        /// CURRENT platform, looking under the per-RID sub-folder of
        /// the install dir.
        ///
        /// <para>Expected layout after install (all RIDs ship as
        /// self-contained single-file binaries — players need no .NET
        /// runtime installed):</para>
        /// <code>
        /// %AppData%/RimWorld Together/LocalServer/
        ///   win-x64/
        ///     GameServer.exe     ← Windows (self-contained .exe)
        ///   linux-x64/
        ///     GameServer         ← Linux (self-contained, no extension)
        ///   osx-x64/
        ///     GameServer         ← macOS Intel (self-contained, no extension)
        ///   osx-arm64/
        ///     GameServer         ← macOS Apple Silicon (self-contained)
        /// </code>
        ///
        /// <para>Backwards-compat: if the file is at the LocalServer
        /// root (older flat layout from 26.5.20.x bundles), we still
        /// pick it up so the upgrade path doesn't strand existing
        /// installs.</para>
        /// </summary>
        private static string FindInstalledExecutable()
        {
            try
            {
                if (!Directory.Exists(Master.AppdataLocalServerPath)) return string.Empty;
                string rid = CurrentRid;
                if (rid == null) return string.Empty;
                string entryName = ServerEntryFileName;

                // Preferred: per-RID sub-folder.
                string ridDir = Path.Combine(Master.AppdataLocalServerPath, rid);
                if (Directory.Exists(ridDir))
                {
                    string ridEntry = Path.Combine(ridDir, entryName);
                    if (File.Exists(ridEntry)) return ridEntry;
                }

                // Backwards-compat: legacy flat layout (Windows-only old
                // bundle that lived at LocalServer/GameServer.exe).
                string flat = Path.Combine(Master.AppdataLocalServerPath, entryName);
                if (File.Exists(flat)) return flat;

                // Last-resort: scan one level deep so an unzip into a
                // wrapper folder (some publish pipelines do this) still
                // resolves.
                foreach (string dir in Directory.GetDirectories(Master.AppdataLocalServerPath))
                {
                    string nested = Path.Combine(dir, entryName);
                    if (File.Exists(nested)) return nested;
                }
                return string.Empty;
            }
            catch { return string.Empty; }
        }

        /// <summary>
        /// KMH 26.5.22.1: Find the bundled server for the CURRENT
        /// platform under the mod folder. Same RID layout as
        /// <see cref="FindInstalledExecutable"/> — the bundle is a
        /// direct copy of the AppData install layout, which lets
        /// <see cref="CopyDirectory"/> work without any flattening.
        ///
        /// <para>Mod-folder layout:</para>
        /// <code>
        /// {ModRoot}/LocalServer/
        ///   win-x64/   ← ships with the mod for Windows players
        ///   linux-x64/ ← ships with the mod for Linux players (optional)
        ///   osx-x64/   ← ships with the mod for macOS Intel (optional)
        ///   osx-arm64/ ← ships with the mod for macOS ARM (optional)
        /// </code>
        ///
        /// <para>Builders are free to ship only the RIDs they support
        /// — players on an unbundled platform fall back to the URL or
        /// see the menu entry hidden if no URL is set. KMH's official
        /// release ships <c>win-x64</c> self-contained (zero player
        /// runtime install) plus framework-dependent builds for the
        /// other three RIDs (require .NET 8 runtime).</para>
        /// </summary>
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

                // Preferred: per-RID sub-folder.
                string ridDir = Path.Combine(bundleRoot, rid);
                if (Directory.Exists(ridDir))
                {
                    string ridEntry = Path.Combine(ridDir, entryName);
                    if (File.Exists(ridEntry)) return ridEntry;
                }

                // Backwards-compat: pre-26.5.22.1 flat layout.
                string flat = Path.Combine(bundleRoot, entryName);
                if (File.Exists(flat)) return flat;

                // One level deeper (publish wrapper folder).
                foreach (string dir in Directory.GetDirectories(bundleRoot))
                {
                    string nested = Path.Combine(dir, entryName);
                    if (File.Exists(nested)) return nested;
                }
                return string.Empty;
            }
            catch { return string.Empty; }
        }

        /// <summary>
        /// KMH 26.5.22.1: Resolve the source DIRECTORY for the current
        /// RID's bundle. Used by <see cref="InstallFromBundle"/> to copy
        /// only the relevant platform's files (not all of them — that
        /// would bloat the install dir 4×).
        /// </summary>
        private static string GetBundleSourceDirectory()
        {
            string entry = FindBundleExecutable();
            if (string.IsNullOrEmpty(entry)) return string.Empty;
            return Path.GetDirectoryName(entry) ?? string.Empty;
        }

        /// <summary>
        /// KMH 26.5.22.1: Target directory for the current RID under
        /// AppData. Used by <see cref="InstallFromBundle"/> as the copy
        /// destination so the on-disk layout matches the bundle source.
        /// </summary>
        private static string GetInstallTargetDirectory()
        {
            string rid = CurrentRid;
            if (rid == null) return Master.AppdataLocalServerPath; // fallback — shouldn't happen
            return Path.Combine(Master.AppdataLocalServerPath, rid);
        }

        /// <summary>
        /// KMH 26.5.22.1: Recursive copy from <paramref name="sourceDir"/>
        /// to <paramref name="destDir"/>. We do this by hand (instead of
        /// shelling to robocopy/xcopy) so behaviour is identical across
        /// Windows / macOS / Linux. <c>File.Copy(..., overwrite: true)</c>
        /// is intentional — the WipeInstallDir already cleaned out the
        /// target, but overwrite=true is a defensive safety net.
        /// </summary>
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

        // ------------- Network helpers (download path only) -------------

        private static void SetupTls()
        {
            // KMH 26.5.22.1: GitHub release endpoints reject TLS < 1.2
            // since 2022. .NET Framework 4.7.2 still defaults to SSL3 +
            // TLS 1.0 on Windows 10 21H1 and earlier; without this line
            // WebClient throws "Could not create SSL/TLS secure channel".
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
            if (!File.Exists(zipPath))
                throw new FileNotFoundException("Downloaded zip is missing", zipPath);

            // KMH 26.5.22.1: RID-scoped wipe + extract. Earlier versions
            // wiped EVERY sub-directory under AppdataLocalServerPath
            // before extract, which destroyed bundle installs for other
            // RIDs (the original "multi-OS coexistence" promise). Now we
            // only touch the current-RID sub-folder, matching what
            // InstallFromBundle does. A player who has Windows + Linux
            // bundles installed side-by-side can re-download one
            // without nuking the other.
            string targetDir = GetInstallTargetDirectory();
            WipeRidInstallDir(targetDir);

            // ExtractToDirectory creates the target if missing; explicit
            // CreateDirectory is defensive for older Framework versions
            // whose ExtractToDirectory doesn't auto-create.
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

        // ------------- In-game config editing -------------

        /// <summary>
        /// KMH 26.5.22.1: List the config JSON files in the server's
        /// working directory and let the player click one to open it
        /// in their system default editor (Notepad / TextEdit / etc.).
        /// Saves them from having to navigate to AppData and find the
        /// files by hand.
        ///
        /// <para>The server creates its configs on first launch into
        /// <c>{installDir}/Configs/</c>. If that folder doesn't exist
        /// yet — meaning the server has never been started — we tell
        /// the player to launch first, then come back.</para>
        ///
        /// <para>We open the file via <c>Process.Start(path)</c> with
        /// <c>UseShellExecute = true</c>, which on Windows means "use
        /// the OS file association" — same as double-clicking. On Mac
        /// it routes through <c>open</c>, on Linux through
        /// <c>xdg-open</c> (if installed). Works without us having to
        /// hard-code any editor names.</para>
        /// </summary>
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
            try
            {
                files = Directory.GetFiles(configsDir, "*.json");
            }
            catch (Exception ex)
            {
                Printer.Warning($"[LocalServer] Could not list configs: {ex.Message}");
                DLG_Base.PushNewDialog(new DLG_Message("Error",
                    new[] { "Could not read the configs folder.", ex.Message }));
                return;
            }

            if (files.Length == 0)
            {
                // Configs/ exists but empty — fall back to opening the
                // folder so the player can investigate.
                try { Process.Start(configsDir); } catch { }
                DLG_Base.PushNewDialog(new DLG_Message("Configs",
                    new[] { "No config files found yet. Opening the folder so you can check." }));
                return;
            }

            // Sort alphabetically so the same dialog rendering reliably
            // produces the same row order. Players muscle-memory the
            // positions.
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            var opts = new List<Verse.FloatMenuOption>();
            foreach (string file in files)
            {
                string display = Path.GetFileName(file);
                opts.Add(new Verse.FloatMenuOption(display, () => OpenConfigFile(file)));
            }

            // Always offer "Open Configs folder" as the escape hatch
            // for advanced edits (e.g. delete a file, copy one across,
            // version-control the folder, etc.).
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

        /// <summary>
        /// KMH 26.5.22.1: Open a single JSON config in the system
        /// default editor. Cross-platform via
        /// <c>UseShellExecute = true</c>: the OS picks the file
        /// association. On Linux this requires <c>xdg-open</c> to be
        /// installed (any modern desktop has it); on macOS it goes
        /// through <c>open</c>, which is always present.
        /// </summary>
        private static void OpenConfigFile(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                });
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
