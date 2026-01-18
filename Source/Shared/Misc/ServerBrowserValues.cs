using System;

namespace Shared.Misc
{
    public static class ServerBrowserValues
    {
        private const string DevServerBrowser = "https://rimworldtogetherdev.eragon.dev";
        private const string ProdServerBrowser = "https://rimworldtogether.eragon.dev";
        public const string ServerBrowserUrl = DevServerBrowser;
        public const string RegisterServerUrl = $"{ServerBrowserUrl}{RegisterServer}";
        public const string RegisterServer = "/servers/register";
        public const string TelemetryServerUrl = $"{ServerBrowserUrl}{TelemetryServer}";
        public const string TelemetryServer = "/servers/telemetry";
        public const string UpdateServerUrl = $"{ServerBrowserUrl}{UpdateServer}";
        public const string UpdateServer = "/servers/update";
        public const string GetServersUrl = $"{ServerBrowserUrl}{GetServers}";
        public const string GetServers = "/servers/all";
        public const string GetSecretUrl = $"{ServerBrowserUrl}{GetSecret}";
        public const string GetSecret = "/servers/getsecret";
        public static readonly TimeSpan RemovalSpan = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan HeartbeatDelay = TimeSpan.FromSeconds(15);
        
        #if RELEASE
        static ServerBrowserValues()
        {
            if (ServerBrowserUrl == DevServerBrowser)
            {
                Printer.Error($"Current server browser url is the one for development, switch it to production before publishing!");
            }
        }
        #endif
    }
}