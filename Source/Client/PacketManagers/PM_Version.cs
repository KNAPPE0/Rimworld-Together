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

        // Whitelist version strings before interpolating into the GitHub URL
        // — blocks path-traversal payloads ("../../...") slipping into the
        // download path.
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

                                // /c ""<path>"" — four quotes are required for cmd.exe
                                // to handle spaces in the path correctly.
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

        // net472 defaults to TLS 1.0; GitHub release endpoints require 1.2+.
        private static void SetupTls()
        {
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

            using (WebClient webClient = new WebClient())
            {
                webClient.Headers.Add("User-Agent", $"KMH/{CommonValues.ExecutableVersion}");

                if (wait != null)
                {
                    long lastShownPercent = -1;
                    webClient.DownloadProgressChanged += (s, e) =>
                    {
                        // Whole-percent throttle keeps main-thread queue clean.
                        if (e.ProgressPercentage == lastShownPercent) return;
                        lastShownPercent = e.ProgressPercentage;

                        long kbReceived = e.BytesReceived / 1024;
                        long kbTotal = e.TotalBytesToReceive / 1024;
                        string label = e.TotalBytesToReceive > 0
                            ? $"Downloading {e.ProgressPercentage}% ({kbReceived}/{kbTotal} KB)"
                            : $"Downloading ({kbReceived} KB)";
                        try { MainThreadHandler.Instance.Enqueue(() => wait.UpdateDescription(label)); }
                        catch { }
                    };
                }

                webClient.DownloadFile(new Uri(uri), downloadPath);
            }
        }
    }
}
