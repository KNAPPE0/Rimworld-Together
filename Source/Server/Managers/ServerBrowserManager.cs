using GameServer.Core;
using GameServer.Hooks.TCPNetwork;
using Shared;
using Shared.Files.Configs;
using Shared.Misc;
using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Net.Mime;
using System.Text;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using TCPNetwork.Packets.ServerBrowser;
using static Shared.CommonEnumerators;
// ReSharper disable FunctionNeverReturns

namespace GameServer.Managers
{
    public static class ServerBrowserManager
    {
        private const string GetPublicIpAddressURL = "https://api.ipify.org";
        
        private const int MaxDescriptionLength = 200;

        private const int MaxNameLength = 40;

        private static HttpClientHandler handler = new HttpClientHandler() { UseProxy = false };

        private static HttpClient Client = new HttpClient(handler) { DefaultRequestVersion = HttpVersion.Version11 };
        
        private static bool IsRunning { get; set; }= false;

        private static ServerAuth Auth = default;

        private static readonly byte[] TelemetryBuffer = new byte[ServerAuth.PacketSize + Telemetry.PacketSize];
        
        [HandlesPacket(PacketHeader.ServerBrowserReachability)]
        private static void ParsePacket(ServerClient client, byte[] bytes, PacketHeader header)
        {
            if(!IsRunning)
                ResponseShortcutManager.SendIllegalPacket(client, 
                    "Server did not have the server browser enabled, if you're seeing this then a bug occured", false);
            client.Listener.EnqueuePacket(PacketHeader.ServerBrowserReachability, new KeepAliveData());
            client.Listener.Disconnect();
        }

        public static void StartFeature()
        {
            if (Master.ServerBrowserConfig.EnableServerBrowser)
            {
                if (ValidateServerInformation())
                {
                    Printer.Warning("Server discovery is ENABLED");
                    Printer.Warning("The server details are currently being transmitted to the public browser");
                    IsRunning = true;
                    Task.Run(async () =>
                    {
                        await GetServerSecret();
                        while (true)
                        {
                            await SendServerUpdate();
                            await Task.Delay(ServerBrowserValues.HeartbeatDelay);
                        }
                    });
                }
            }

            else
            {
                Printer.Warning("Server discovery is DISABLED");
                Printer.Warning("Please turn the service ON in the settings if you want your server listed publicly");
                Printer.Title($"----------------------------------------");

                if (Master.ServerBrowserConfig.EnableServerTelemetry)
                {
                    Task.Run(async () =>
                    {
                        await GetServerSecret();
                        while (true)
                        {
                            await SendServerTelemetry();
                            await Task.Delay(ServerBrowserValues.HeartbeatDelay);
                        }
                    });
                }

                else
                {
                    Printer.Warning("Server telemetry is DISABLED");
                    Printer.Warning("No diagnostics details will be send to the master server");
                    Printer.Warning("Please consider ENABLING this feature! It helps the development of the mod!");
                    Printer.Title($"----------------------------------------");
                }
            }
        }

        private static bool ValidateServerInformation() 
        {
            ServerConfigFile serverInfo = Master.ServerConfig;
            ServerBrowserConfigFile serverBrowserInfo = Master.ServerBrowserConfig;

            if (serverInfo.Description.Length > MaxDescriptionLength) 
            {
                Printer.Error($"Server description is above {MaxDescriptionLength} characters, please shorten it. Server browser features have been turned off.");
                return false;
            }

            if (!IsValidEndPoint(serverBrowserInfo.PublicEndPoint))
            {
                Printer.Error($"Public endpoint \"{serverBrowserInfo.PublicEndPoint}\" is not a valid ip address. Server browser features have been turned off and faulty entry has been removed.");
                serverBrowserInfo.PublicEndPoint = "";
                ServerBrowserConfigFile.Save(ServerBrowserConfigFile.SavePath, serverBrowserInfo);
            }
            
            if (string.IsNullOrEmpty(serverBrowserInfo.PublicEndPoint))
            {
                if(!GetPublicIpAddressAsync().Result)
                {
                    Printer.Error(
                        $"Public endpoint is empty. Please set your public ip address or domain. Server browser features have been turned off.");
                    return false;
                }
            }
            
            if (serverInfo.Name.Length > MaxNameLength)
            {
                Printer.Error($"Server name is above {MaxNameLength} characters, please shorten it. Server browser features have been turned off.");
                return false;
            }

            if (serverInfo.Name == "RimWorld-Together-Server") 
            {
                Printer.Error($"Server name is the default name of {serverInfo.Name}. Please change the server name to something unique!. Server browser features have been turned off.");
                return false;
            }

            return true;
        }

        private static async Task<bool> GetPublicIpAddressAsync()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

                var ip = await Client.GetStringAsync(GetPublicIpAddressURL, cts.Token);

                ip = ip.Trim();

                if (!IsValidEndPoint(ip))
                    return false;

                Master.ServerBrowserConfig.PublicEndPoint = ip;
                ServerBrowserConfigFile.Save(ServerBrowserConfigFile.SavePath, Master.ServerBrowserConfig);
                Printer.Warning($"Public endpoint was empty for the server browser, but the server managed to automatically fetch the ip {ip}. If this is not the correct ip, make sure to change it in the config file!");
                return true;
            }
            catch (Exception ex)
            {
                Printer.Warning($"Failed to automatically resolve public IP address: {ex.Message}", LogImportanceMode.Verbose);
                return false;
            }
        }

        private static bool IsValidEndPoint(string endpoint)
        {
            try
            {
                if (Dns.GetHostAddresses(endpoint).Length > 0)
                    return true;
            }
            catch
            {
                return false;
            }
            return false;
        }

        private static async Task RegisterServer()
        {
            ServerInfo server = new ServerInfo()
            {
                _ip = Master.ServerBrowserConfig.PublicEndPoint,
                _port = Master.ServerConfig.Port,
                _name = Master.ServerConfig.Name,
                _description = Master.ServerConfig.Description,
                _maximumPlayerCount = Master.ServerConfig.MaxPlayers,
                _currentPlayerCount = Network.ServerClients.Count,
                _version = CommonValues.ExecutableVersion,
                _config = Master.ModConfig
            };
            var serializedServerInfo = Serializer.ConvertObjectToBytes(server);
            byte[] packet = new byte[ServerAuth.PacketSize + serializedServerInfo.Length];
            var packetSpan = packet.AsSpan();
            var serverAuth = TelemetryBuffer.AsSpan(0, ServerAuth.PacketSize);
            serverAuth.CopyTo(packetSpan.Slice(0, ServerAuth.PacketSize));
            serializedServerInfo.AsSpan().CopyTo(packetSpan.Slice(ServerAuth.PacketSize));
            HttpResponseMessage response = await Client.PostAsync(ServerBrowserValues.RegisterServerUrl, new ByteArrayContent(packet));
            response.EnsureSuccessStatusCode();
        }
        
        private static async Task GetServerSecret()
        {
            var ip = Master.ServerBrowserConfig.PublicEndPoint;
            var portStr = Master.ServerConfig.Port.ToString();
            if (!ushort.TryParse(portStr, out var port))
            {
                throw new Exception($"Non-numeric port for server: {portStr}");
            }

            var id = Hasher.GetServerId(ip, port);
            HttpResponseMessage response = await Client.GetAsync(ServerBrowserValues.GetSecretUrl);
            response.EnsureSuccessStatusCode();
            Span<byte> authRaw = await response.Content.ReadAsByteArrayAsync();
            if (authRaw.Length != sizeof(ulong))
            {
                throw new Exception($"Should never happen, packet size miss-match when receiving auth, got {authRaw.Length}");
            }
            Auth._secret = BinaryPrimitives.ReadUInt64LittleEndian(authRaw);
            Auth._id = id;

            Auth.CopyInto(TelemetryBuffer.AsSpan().Slice(0, ServerAuth.PacketSize));
        }
        
        private static async Task SendServerUpdate()
        {
            try
            {
                PrepareTelemetryIntoBuffer();
                ByteArrayContent body = new ByteArrayContent(TelemetryBuffer);
                HttpResponseMessage response = await Client.PostAsync(ServerBrowserValues.UpdateServerUrl, body);
                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    if (Master.ServerBrowserConfig.EnableServerBrowser)
                    {
                        await RegisterServer();
                    }
                    body = new ByteArrayContent(TelemetryBuffer);
                    response = await Client.PostAsync(ServerBrowserValues.UpdateServerUrl, body);
                    if (response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        Printer.Error($"Fell into forbidden loop, this should never happen.", LogImportanceMode.Verbose);
                    }
                    return;
                }
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                Printer.Error($"Error while notifying the Master Server\n {ex}", LogImportanceMode.Verbose);
            }
        }

        private static async Task SendServerTelemetry()
        {
            PrepareTelemetryIntoBuffer();
            ByteArrayContent body = new ByteArrayContent(TelemetryBuffer);
            HttpResponseMessage response = await Client.PostAsync(ServerBrowserValues.TelemetryServerUrl, body);
        }
        
        
        private static void PrepareTelemetryIntoBuffer()
        {
            var playerCount = Network.ServerClients.Count;
            var destination = TelemetryBuffer.AsSpan().Slice(ServerAuth.PacketSize);
            BinaryPrimitives.WriteInt32LittleEndian(destination, playerCount); ;
        }
    }
}
