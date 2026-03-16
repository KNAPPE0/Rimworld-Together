using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
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
        // Using memories avoids an implicit conversion down the line
        private static readonly Memory<byte> SenderBuffer = new byte[]{(byte)PacketHeader.ServerBrowserReachability, 0, 0, 0, 0};
        private static readonly Memory<byte> ReceiverBuffer = new byte[Concurrency * (sizeof(PacketHeader) + Network.PacketLengthSizeInBytes)];
        
        private static readonly WebClient Client = new WebClient();

        // I know how much you like your getters and setters, but it don't work on volatile
        private static volatile bool IsRunning = false;
        private static int PingedIndex { get; set; }= 0;
        public static ServerInfo[] AllServers { get; private set; } = [];
        
        public static void TurnOnReachabilityChecks()
        {
            if(IsRunning || AllServers.Length == 0)
                return;
            IsRunning = true;

            _ = Task.Run( async () => CheckForConnections().GetAwaiter().GetResult());
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
                    if (server._version != CommonValues.ExecutableVersion)
                    {
                        Printer.Warning($"Server {server._name} did not have the same version as the client", LogImportanceMode.Verbose);
                        i--;
                        continue;
                    }
                    tasks.Add(TryReachOfServer(server, i));
                }
                await Task.WhenAll(tasks);
                tasks.Clear();
            }
            Printer.Warning("Finished checking for connections", LogImportanceMode.Verbose);
            TurnOffReachabilityChecks();
        }

        private static async Task TryReachOfServer(ServerInfo server, int index)
        {
            using var client = new TcpClient();
            var token = new CancellationTokenSource(500);
            try
            {
                var connectTask = client.ConnectAsync(server._ip, server._port);
                
                if (await Task.WhenAny(connectTask, Task.Delay(500)) != connectTask)
                {
                    server.Reachability = Reachability.Unreachable;
                    Printer.Warning($"Server found but not reachable {server._name}", LogImportanceMode.Verbose);
                    return;
                }

                using (var stream = client.GetStream())
                {
                    await stream.WriteAsync(SenderBuffer, token.Token);
                    _ = await stream.ReadAsync(ReceiverBuffer, token.Token);
                    server.Reachability = Reachability.Reachable;
                    Printer.Warning($"Server found and reachable {server._name}", LogImportanceMode.Verbose);
                }
            }
            catch (OperationCanceledException)
            {
                server.Reachability = Reachability.Unreachable;
                Printer.Warning($"Server found but not reachable {server._name}", LogImportanceMode.Verbose);
            }
            catch (Exception ex)
            {
                Printer.Warning($"Server found but not reachable {server._name}", LogImportanceMode.Verbose);
                server.Reachability = Reachability.Unreachable;
            }
        }
        
        public static void GetAllServersAvailable()
        {
            try
            {
                byte[] response = Client.DownloadData(ServerBrowserValues.GetServersUrl);
                PKT_AllServers data = Serializer.ConvertBytesToObject<PKT_AllServers>(response);
                AllServers = data._serverInfos;
                TurnOnReachabilityChecks();
            }
            catch (Exception ex)
            {
                Printer.Error($"Error while trying to fetch info from the server browser.\n{ex}");
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
