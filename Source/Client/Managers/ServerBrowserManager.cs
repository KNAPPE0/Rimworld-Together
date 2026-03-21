using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Shared;
using Steamworks;
using TCPNetwork;
using static Shared.CommonEnumerators;
using Shared.Misc;
using TCPNetwork.Packets.ServerBrowser;
using TCPNetwork.ServerBrowser;

namespace GameClient.Managers
{
    public static class ServerBrowserManager
    {
        private const int Concurrency = 10;
        private static readonly Memory<byte> SenderBuffer = new byte[] { (byte)PacketHeader.ServerBrowserReachability, 0, 0, 0, 0 };
        private static readonly Memory<byte> ReceiverBuffer = new byte[Concurrency * (sizeof(PacketHeader) + Network.PacketLengthSizeInBytes)];

        private static readonly HttpClient HttpClient = CreateHttpClient();

        private static volatile bool IsRunning = false;
        private static int PingedIndex { get; set; } = 0;

        public static ServerInfo[] AllServers { get; private set; } = Array.Empty<ServerInfo>();

        public static string LastBrowserError { get; private set; } = string.Empty;

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                UseProxy = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(8);
            return client;
        }

        public static void TurnOnReachabilityChecks()
        {
            if (IsRunning || AllServers == null || AllServers.Length == 0)
                return;

            IsRunning = true;
            _ = Task.Run(async () => await CheckForConnections());
        }

        public static void TurnOffReachabilityChecks()
        {
            PingedIndex = 0;
            IsRunning = false;
        }

        private static async Task CheckForConnections()
        {
            List<Task> tasks = new List<Task>(Concurrency);

            while (IsRunning && PingedIndex < AllServers.Length)
            {
                for (int i = 0; i < Concurrency && PingedIndex < AllServers.Length; i++, PingedIndex++)
                {
                    var server = AllServers[PingedIndex];
                    if (server == null)
                        continue;

                    if (server._version != CommonValues.ExecutableVersion)
                    {
                        Printer.Warning($"Server {server._name} did not have the same version as the client", LogImportanceMode.Verbose);
                        continue;
                    }

                    tasks.Add(TryReachOfServer(server));
                }

                await Task.WhenAll(tasks);
                tasks.Clear();
            }

            Printer.Warning("Finished checking for server reachability", LogImportanceMode.Verbose);
            TurnOffReachabilityChecks();
        }

        private static async Task TryReachOfServer(ServerInfo server)
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(500);

            try
            {
                var connectTask = client.ConnectAsync(server._ip, server._port);
                var completed = await Task.WhenAny(connectTask, Task.Delay(500, cts.Token));

                if (completed != connectTask)
                {
                    server.Reachability = Reachability.Unreachable;
                    Printer.Warning($"Server found but not reachable {server._name}", LogImportanceMode.Verbose);
                    return;
                }

                using NetworkStream stream = client.GetStream();
                await stream.WriteAsync(SenderBuffer, cts.Token);
                _ = await stream.ReadAsync(ReceiverBuffer, cts.Token);

                server.Reachability = Reachability.Reachable;
                Printer.Warning($"Server found and reachable {server._name}", LogImportanceMode.Verbose);
            }
            catch (OperationCanceledException)
            {
                server.Reachability = Reachability.Unreachable;
                Printer.Warning($"Server found but not reachable {server._name}", LogImportanceMode.Verbose);
            }
            catch
            {
                server.Reachability = Reachability.Unreachable;
                Printer.Warning($"Server found but not reachable {server._name}", LogImportanceMode.Verbose);
            }
        }

        public static bool GetAllServersAvailable()
        {
            try
            {
                LastBrowserError = string.Empty;

                using HttpResponseMessage response = HttpClient.GetAsync(ServerBrowserValues.GetServersUrl).GetAwaiter().GetResult();

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == HttpStatusCode.BadGateway ||
                        response.StatusCode == HttpStatusCode.ServiceUnavailable ||
                        response.StatusCode == HttpStatusCode.GatewayTimeout)
                    {
                        LastBrowserError = "The public server browser is temporarily unavailable.";
                        Printer.Warning($"Server browser unavailable: {(int)response.StatusCode} {response.ReasonPhrase}");
                    }
                    else
                    {
                        LastBrowserError = $"Failed to fetch server browser data ({(int)response.StatusCode}).";
                        Printer.Warning($"Error while trying to fetch info from the server browser: {(int)response.StatusCode} {response.ReasonPhrase}");
                    }

                    AllServers = Array.Empty<ServerInfo>();
                    return false;
                }

                byte[] responseBytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                PKT_AllServers data = Serializer.ConvertBytesToObject<PKT_AllServers>(responseBytes);

                AllServers = data?._serverInfos ?? Array.Empty<ServerInfo>();

                if (AllServers.Length == 0)
                {
                    LastBrowserError = "No public servers were returned by the browser.";
                    return false;
                }

                TurnOnReachabilityChecks();
                return true;
            }
            catch (TaskCanceledException)
            {
                LastBrowserError = "The server browser request timed out.";
                AllServers = Array.Empty<ServerInfo>();
                Printer.Warning("Server browser request timed out.");
                return false;
            }
            catch (HttpRequestException ex)
            {
                LastBrowserError = "Could not contact the public server browser.";
                AllServers = Array.Empty<ServerInfo>();
                Printer.Warning($"Error while trying to fetch info from the server browser: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                LastBrowserError = "Unexpected error while loading the server browser.";
                AllServers = Array.Empty<ServerInfo>();
                Printer.Warning($"Unexpected server browser error: {ex.Message}");
                return false;
            }
        }

        public static bool DownloadMod(ulong steamId)
        {
            try
            {
                SteamUGC.SubscribeItem(new PublishedFileId_t(steamId));
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}