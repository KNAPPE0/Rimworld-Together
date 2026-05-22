using GameServer.Core;
using GameServer.Hooks.TCPNetwork;
using Shared;
using Shared.Files.Configs.Mods;
using Shared.Misc;
using System.Net.Sockets;
using System.Reflection;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets.ServerBrowser;
using static Shared.Misc.Printer;

namespace GameServer.Hooks.ServerBrowser
{
    public static class ServerBrowserManager
    {
        private static string ServerIPV4 { get; set; } = string.Empty;

        private static bool WasStartedOnce { get; set; } = false;

        public enum BrowserMode { Normal, Lite }

        private static Action<PacketHeader, byte[], ServerClient> OnReadPacket { get; set; } = delegate (PacketHeader header, byte[] buffer, ServerClient client)
        {
            MethodInfo method = (MethodInfo)PM_Base.PacketDictionary[header][1];
            method.Invoke(PM_Base.PacketDictionary[header][0], new object[] { client, buffer, header });
        };

        private static Action<ServerClient> OnDisconnect { get; set; } = delegate
        {
            Task.Run(delegate
            {
                Network.BrowserEndpoint = null;
                Thread.Sleep(Network.BrowserTelemetryInterval);
                StartFeature();
            });
        };

        public static void StartFeature()
        {
            if (Network.BrowserEndpoint != null) Printer.Error("Server was already connected to browser");
            else if (!Master.ServerConfig.EnableServerTelemetry) return;
            else
            {
                while (Network.BrowserEndpoint == null)
                {
                    if (!Master.ServerConfig.EnableServerBrowser) ConnectToServerBrowser(BrowserMode.Lite);
                    else ConnectToServerBrowser(BrowserMode.Normal);
                }

                if (Master.ServerConfig.EnableServerBrowser)
                {
                    if (!WasStartedOnce)
                    {
                        WasStartedOnce = true;

                        Printer.Title(Printer.SeparatorString);
                        Printer.Warning("Server discovery is ENABLED");
                        Printer.Warning("The server details are currently being transmitted to the public browser");
                        Printer.Title(Printer.SeparatorString);
                    }
                }

                else
                {
                    if (!WasStartedOnce)
                    {
                        WasStartedOnce = true;

                        Printer.Title(Printer.SeparatorString);
                        Printer.Warning("Server discovery is DISABLED");
                        Printer.Warning("Please turn the service ON in the settings if you want your server listed publicly");
                        Printer.Title(Printer.SeparatorString);
                    }
                }
            }
        }

        private static bool ConnectToServerBrowser(BrowserMode mode)
        {
            try
            {
                ServerClient client = new ServerClient(new TcpClient(Network.BrowserIp, Network.BrowserServerPort), new NetworkRuleset(null, OnDisconnect, OnReadPacket, null, false));
                Network.BrowserEndpoint = client.Listener;
                SetupConnection(mode);
                return true;
            }

            catch (Exception ex) 
            { 
                Printer.Error(ex, LogImportanceMode.Ludicrous);
                return false;
            }
        }

        private static async void SetupConnection(BrowserMode mode)
        {
            // Cache public IP — avoid thrashing ipify on every reconnect.
            if (!WasStartedOnce || string.IsNullOrEmpty(ServerIPV4))
                ServerIPV4 = await GetPublicIP();

            PKT_ServerTelemetry telemetry = new PKT_ServerTelemetry();
            telemetry.Name = Master.ServerConfig.Name;
            telemetry.Description = Master.ServerConfig.Description;
            // Empty URLs hide the button client-side.
            telemetry.SteamWorkshopURL = Master.ServerConfig.SteamWorkshopURL ?? string.Empty;
            telemetry.DiscordURL = Master.ServerConfig.DiscordURL ?? string.Empty;
            telemetry.Version = CommonValues.ExecutableVersion;
            telemetry.Endpoint = ServerIPV4;
            telemetry.Port = Master.ServerConfig.Port;
            telemetry.IsPrivate = mode == BrowserMode.Lite;
            telemetry.CurrentPopulation = ServerNetwork.GetConnectedClients().Length;
            telemetry.MaxPopulation = Master.ServerConfig.MaxPlayers;
            telemetry.Mods = Master.ModConfig.ModConfigs.Where(fetch => fetch.Type != ModConfigFile.ModType.Forbidden)
                .OrderBy(fetch => fetch.FileName).ToList();

            Network.BrowserEndpoint.EnqueuePacket(PacketHeader.ServerBrowserTelemetry, telemetry);
        }

        public static async Task<string> GetPublicIP()
        {
            // ipify failures used to crash SetupConnection silently — swallow + log.
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string address = await client.GetStringAsync("https://api.ipify.org");
                    return address;
                }
            }
            catch (Exception ex)
            {
                Printer.Error(ex, LogImportanceMode.Ludicrous);
                return string.Empty;
            }
        }
    }
}
