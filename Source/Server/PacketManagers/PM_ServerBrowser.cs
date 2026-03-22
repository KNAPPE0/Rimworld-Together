using GameServer.Core;
using GameServer.Hooks.TCPNetwork;
using GameServer.Managers;
using Shared;
using Shared.Files.Configs;
using Shared.Misc;
using System.Buffers.Binary;
using System.Net;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using TCPNetwork.ServerBrowser;
using static Shared.CommonEnumerators;
// ReSharper disable FunctionNeverReturns

namespace GameServer.PacketManager
{
    public class PM_ServerBrowser : PM_Base
    {
        private const string GetPublicIpAddressURL = "https://api.ipify.org";

        private const int MaxDescriptionLength = 200;
        private const int MaxNameLength = 40;

        private static readonly HttpClientHandler Handler = new HttpClientHandler { UseProxy = false };
        private static readonly HttpClient Client = CreateHttpClient();

        private static bool IsRunning { get; set; } = false;

        private static ServerAuth Auth = default;

        private static readonly byte[] TelemetryBuffer = new byte[ServerAuth.PacketSize + Telemetry.PacketSize];

        [HandlesPacket(PacketHeader.ServerBrowserReachability)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            if (!IsRunning)
            {
                ResponseShortcutManager.SendIllegalPacket(
                    client,
                    "Server did not have the server browser enabled, if you're seeing this then a bug occurred",
                    false);
                return;
            }

            client.Listener.EnqueuePacket(PacketHeader.ServerBrowserReachability, new PKT_KeepAlive());
            client.Listener.Disconnect();
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient(Handler)
            {
                DefaultRequestVersion = HttpVersion.Version11,
                Timeout = TimeSpan.FromSeconds(8)
            };

            return client;
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
                        await EnsureServerSecretSafe();

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
                Printer.Title("----------------------------------------");

                if (Master.ServerBrowserConfig.EnableServerTelemetry)
                {
                    Task.Run(async () =>
                    {
                        await EnsureServerSecretSafe();

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
                    Printer.Warning("No diagnostics details will be sent to the master server");
                    Printer.Warning("Please consider ENABLING this feature! It helps the development of the mod!");
                    Printer.Title("----------------------------------------");
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
                serverBrowserInfo.PublicEndPoint = string.Empty;
                ServerBrowserConfigFile.Save(ServerBrowserConfigFile.SavePath, serverBrowserInfo);
            }

            if (string.IsNullOrEmpty(serverBrowserInfo.PublicEndPoint))
            {
                if (!GetPublicIpAddressAsync().Result)
                {
                    Printer.Error("Public endpoint is empty. Please set your public ip address or domain. Server browser features have been turned off.");
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
                Printer.Error($"Server name is the default name of {serverInfo.Name}. Please change the server name to something unique. Server browser features have been turned off.");
                return false;
            }

            return true;
        }

        private static async Task<bool> GetPublicIpAddressAsync()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

                string ip = await Client.GetStringAsync(GetPublicIpAddressURL, cts.Token);
                ip = ip.Trim();

                if (!IsValidEndPoint(ip))
                    return false;

                Master.ServerBrowserConfig.PublicEndPoint = ip;
                ServerBrowserConfigFile.Save(ServerBrowserConfigFile.SavePath, Master.ServerBrowserConfig);

                Printer.Warning($"Public endpoint was empty for the server browser, but the server automatically fetched {ip}. If this is not correct, change it in the config file.");
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
                if (string.IsNullOrWhiteSpace(endpoint))
                    return false;

                return Dns.GetHostAddresses(endpoint).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static async Task EnsureServerSecretSafe()
        {
            try
            {
                await GetServerSecret();
            }
            catch (Exception ex)
            {
                Printer.Warning($"Failed to get master server secret: {ex.Message}");
            }
        }

        private static async Task RegisterServer()
        {
            ServerInfo server = new ServerInfo
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

            byte[] serializedServerInfo = Serializer.ConvertObjectToBytes(server);

            byte[] packet = new byte[ServerAuth.PacketSize + serializedServerInfo.Length];
            Span<byte> packetSpan = packet.AsSpan();

            Auth.CopyInto(packetSpan.Slice(0, ServerAuth.PacketSize));
            serializedServerInfo.AsSpan().CopyTo(packetSpan.Slice(ServerAuth.PacketSize));

            using HttpResponseMessage response = await Client.PostAsync(ServerBrowserValues.RegisterServerUrl, new ByteArrayContent(packet));

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Register failed with {(int)response.StatusCode} {response.ReasonPhrase}");
            }
        }

        private static async Task GetServerSecret()
        {
            string ip = Master.ServerBrowserConfig.PublicEndPoint;
            string portStr = Master.ServerConfig.Port.ToString();

            if (!ushort.TryParse(portStr, out ushort port))
                throw new Exception($"Non-numeric port for server: {portStr}");

            ulong id = GetServerId(ip, port);

            using HttpResponseMessage response = await Client.GetAsync(ServerBrowserValues.GetSecretUrl);
            response.EnsureSuccessStatusCode();

            byte[] authRaw = await response.Content.ReadAsByteArrayAsync();
            if (authRaw.Length != sizeof(ulong))
                throw new Exception($"Packet size mismatch when receiving auth, got {authRaw.Length}");

            Auth._secret = BinaryPrimitives.ReadUInt64LittleEndian(authRaw);
            Auth._id = id;

            Auth.CopyInto(TelemetryBuffer.AsSpan(0, ServerAuth.PacketSize));
        }

        private static ulong GetServerId(string ip, ushort port)
        {
            uint ipv4 = Hasher.GetHashFromIpv4(ip);
            return ((ulong)ipv4 << 32) | ((ulong)port << 16);
        }

        private static async Task SendServerUpdate()
        {
            try
            {
                PrepareTelemetryIntoBuffer();

                using ByteArrayContent body = new ByteArrayContent(TelemetryBuffer);
                HttpResponseMessage response = await Client.PostAsync(ServerBrowserValues.UpdateServerUrl, body);

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    Printer.Warning("Master server does not recognize this server yet. Attempting registration.", LogImportanceMode.Verbose);

                    await RegisterServer();

                    using ByteArrayContent retryBody = new ByteArrayContent(TelemetryBuffer);
                    response = await Client.PostAsync(ServerBrowserValues.UpdateServerUrl, retryBody);
                }

                if (response.StatusCode == HttpStatusCode.BadGateway ||
                    response.StatusCode == HttpStatusCode.ServiceUnavailable ||
                    response.StatusCode == HttpStatusCode.GatewayTimeout)
                {
                    Printer.Warning($"Master server temporarily unavailable: {(int)response.StatusCode} {response.ReasonPhrase}");
                    return;
                }

                response.EnsureSuccessStatusCode();
            }
            catch (TaskCanceledException)
            {
                Printer.Warning("Master server update timed out", LogImportanceMode.Verbose);
            }
            catch (HttpRequestException ex)
            {
                Printer.Warning($"Error while notifying the Master Server: {ex.Message}");
            }
            catch (Exception ex)
            {
                Printer.Warning($"Unexpected master server update error: {ex.Message}");
            }
        }

        private static async Task SendServerTelemetry()
        {
            try
            {
                PrepareTelemetryIntoBuffer();

                using ByteArrayContent body = new ByteArrayContent(TelemetryBuffer);
                using HttpResponseMessage response = await Client.PostAsync(ServerBrowserValues.TelemetryServerUrl, body);

                if (response.StatusCode == HttpStatusCode.BadGateway ||
                    response.StatusCode == HttpStatusCode.ServiceUnavailable ||
                    response.StatusCode == HttpStatusCode.GatewayTimeout)
                {
                    Printer.Warning($"Master telemetry temporarily unavailable: {(int)response.StatusCode} {response.ReasonPhrase}", LogImportanceMode.Verbose);
                    return;
                }
            }
            catch (TaskCanceledException)
            {
                Printer.Warning("Master telemetry timed out", LogImportanceMode.Verbose);
            }
            catch (Exception ex)
            {
                Printer.Warning($"Telemetry send failed: {ex.Message}", LogImportanceMode.Verbose);
            }
        }

        private static void PrepareTelemetryIntoBuffer()
        {
            int playerCount = Network.ServerClients.Count;
            Span<byte> destination = TelemetryBuffer.AsSpan(ServerAuth.PacketSize);
            BinaryPrimitives.WriteInt32LittleEndian(destination, playerCount);
        }
    }
}