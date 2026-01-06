using System;

namespace Shared.Misc
{
    public static class ServerBrowserValues
    {
        public const string RegisterServer = "Servers/Register";
        public const string TelemetryServer = "Servers/Telemetry";
        public const string UpdateServer = "Servers/Update";
        public const string GetServers = "Servers/All";
        public static TimeSpan RemovalSpan;
        public static int HeartbeatDelay;
    }
}