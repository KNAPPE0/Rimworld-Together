using GameClient.Core;
using GameClient.Dialogs;
using GameClient.Dialogs.Default;
using GameClient.Misc;
using Shared;
using Shared.Misc;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;
using UnityEngine;

namespace GameClient.PacketManagers
{
    public class PM_Version : PM_Base
    {
        [HandlesPacket(PacketHeader.VersionManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_Version data = Serializer.ConvertBytesToObject<PKT_Version>(bytes);

            switch (data._step)
            {
                case PKT_Version.VersionStep.Ask:
                    SendClientVersion();
                    break;

                case PKT_Version.VersionStep.Pass:
                    PM_Login.UseLoginData();
                    break;
            }
        }

        public static void SendClientVersion()
        {
            Network.ServerEndpoint.TargetClient.VerifyUser();

            PKT_Version data = new PKT_Version();
            data._version = CommonValues.ExecutableVersion;
            Network.ServerEndpoint.EnqueuePacket(PacketHeader.VersionManager, data);
        }

        public static void PromptChangeVersion()
        {
            DLG_Base.PushNewDialog(new DLG_Inputs("Version selection",
                new string[] { "Release number", "Password (optional)" },
                new bool[] { false, true }, ChangeVersion));
        }

        // KMH 26.5.22.1: Constrain inputs that get interpolated into the
        // download URL. Vanilla allows arbitrary text → a malicious
        // suggestion ("../../etc/passwd") would slip into the URL and
        // could be turned into a path traversal once the zip lands on
        // disk. We restrict to: digits, dots, dashes, parentheses, the
        // literal "(KMH)" suffix string, and spaces. Anything else is
        // treated as user error and we abort with a friendly message.
        private static readonly Regex AllowedVersionChars = new Regex(@"^[0-9.\-\(\)A-Za-z ]+$", RegexOptions.Compiled);

        private static void ChangeVersion()
        {
            string requested = DLG_Inputs.DialogInputResults[0]?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(requested) || !AllowedVersionChars.IsMatch(requested) || requested.Length > 48)
            {
                DLG_Base.PushNewDialog(new DLG_Message("ERROR",
                    new[] { "Invalid version string.", "Use digits, dots, dashes, and KMH suffix only." }));
                return;
            }

            string downloadPath = Path.Combine(Master.AppdataVersionPath, "3005289691.zip");
            string uri = $"https://github.com/RimWorld-Together/Rimworld-Together/releases/download/{Uri.EscapeDataString(requested)}/3005289691.zip";

            // KMH 26.5.22.1: Async path so the main UI thread doesn't
            // freeze for the duration of the download. Mirrors the
            // LocalServerHandler approach — DLG_Wait shows live stage
            // labels, and any failure produces a user-visible error
            // dialog instead of silently leaving the user staring.
            DLG_Wait wait = new DLG_Wait("Downloading version");
            DLG_Base.PushNewDialog(wait);

            Task.Run(() =>
            {
                try
                {
                    SetupTls();
                    DownloadVersion(uri, downloadPath, wait);

                    MainThreadHandler.Instance.Enqueue(() =>
                    {
                        try { DLG_Wait.Instance?.Close(); } catch { }

                        Action toDo = delegate
                        {
                            try
                            {
                                string scriptPath = Path.Combine(Master.ModScriptsPath, "VersionUpdater.bat");
                                string copyPath = Path.Combine(Master.AppdataTempPath, "VersionUpdater.bat");
                                string modPath = Path.Combine(Master.AppdataTempPath, "ModPath.txt");

                                if (File.Exists(copyPath)) File.Delete(copyPath);
                                File.Copy(scriptPath, copyPath);
                                File.WriteAllText(modPath, Master.ModMainPath);

                                // KMH 26.5.22.1: Properly balanced quotes for paths with
                                // spaces. cmd.exe's `/c "command"` parses oddly when the
                                // command itself contains quoted spaces — the standard
                                // workaround is `/c "" "<path>" ""`, equivalently
                                // `/c ""<path>""`, where cmd strips the outer empty ""
                                // and runs the inner quoted path. The previous form
                                // `/c ""<path>"` (three quotes) worked by accident on
                                // AppData paths without spaces — would fail silently on
                                // a user whose Windows profile has a space in it.
                                ProcessStartInfo processInfo = new ProcessStartInfo("cmd.exe", $"/c \"\"{copyPath}\"\"");
                                processInfo.UseShellExecute = false;
                                Process.Start(processInfo);

                                Application.Quit();
                            }
                            catch (Exception scriptEx)
                            {
                                Printer.Error($"[VersionUpdater] {scriptEx.Message}");
                                DLG_Base.PushNewDialog(new DLG_Message("ERROR",
                                    new[] { "Updater script could not be launched.", scriptEx.Message }));
                            }
                        };

                        DLG_Base.PushNewDialog(new DLG_Message("MESSAGE",
                            new[] { "Download complete.", "The game will close to apply the new version." }, toDo));
                    });
                }
                catch (Exception ex)
                {
                    Printer.Warning($"[VersionUpdater] Download failed: {ex.Message}");
                    MainThreadHandler.Instance.Enqueue(() =>
                    {
                        try { DLG_Wait.Instance?.Close(); } catch { }
                        DLG_Base.PushNewDialog(new DLG_Message("ERROR",
                            new[] { "Failed to download the specified version.", ex.Message }));
                    });
                }
            });
        }

        private static void SetupTls()
        {
            // KMH 26.5.22.1: Force TLS 1.2 — same reason as LocalServerHandler;
            // .NET Framework 4.7.2 default protocol set is rejected by
            // modern GitHub release endpoints.
            try
            {
                ServicePointManager.SecurityProtocol |=
                    SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;
            }
            catch { }
        }

        private static void DownloadVersion(string uri, string downloadPath, DLG_Wait wait = null)
        {
            if (File.Exists(downloadPath)) File.Delete(downloadPath);

            // KMH 26.5.22.1: Wire up DownloadProgressChanged so the
            // DLG_Wait label cycles between the stage name and a "X% (Y
            // KB / Z KB)" indicator. Without this, slow downloads (e.g.
            // 30 MB on a flaky connection) look like the client is
            // hung. WebClient's event fires on the worker thread, so
            // we hop back to the main thread to update the dialog.
            using (WebClient webClient = new WebClient())
            {
                webClient.Headers.Add("User-Agent", $"KMH/{CommonValues.ExecutableVersion}");

                if (wait != null)
                {
                    long lastShownPercent = -1;
                    webClient.DownloadProgressChanged += (s, e) =>
                    {
                        // Throttle: only refresh the label on whole-percent
                        // changes so we don't queue 1000 enqueues for the
                        // main thread.
                        if (e.ProgressPercentage == lastShownPercent) return;
                        lastShownPercent = e.ProgressPercentage;

                        long kbReceived = e.BytesReceived / 1024;
                        long kbTotal = e.TotalBytesToReceive / 1024;
                        string label = e.TotalBytesToReceive > 0
                            ? $"Downloading {e.ProgressPercentage}% ({kbReceived}/{kbTotal} KB)"
                            : $"Downloading ({kbReceived} KB)";
                        try { MainThreadHandler.Instance.Enqueue(() => wait.UpdateDescription(label)); }
                        catch { /* dialog may have closed mid-tick */ }
                    };
                }

                // KMH 26.5.22.1: Use the synchronous DownloadFile on the
                // worker thread (we're already inside Task.Run). Anything
                // async-aware here would force WaitForExit on the
                // download completion semaphore — adding zero value.
                webClient.DownloadFile(new Uri(uri), downloadPath);
            }
        }
    }
}
