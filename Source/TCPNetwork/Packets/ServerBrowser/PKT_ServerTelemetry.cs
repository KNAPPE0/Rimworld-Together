using Shared.Files.Configs.Mods;
using System.Collections.Generic;

namespace TCPNetwork.Packets.ServerBrowser
{
    public class PKT_ServerTelemetry : PKT_Base
    {
        public string Hash { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Version { get; set; } = string.Empty;

        public string Endpoint { get; set; } = string.Empty;

        public int Port { get; set; } = int.MaxValue;

        public int CurrentPopulation { get; set; } = int.MaxValue;

        public int MaxPopulation { get; set; } = int.MaxValue;

        // KMH 26.5.22.1: Ported from upstream RWT (Apr 2026). These let
        // the server publish its workshop + Discord URLs into the public
        // browser listing — DLG_ServerBrowser shows them as one-click
        // buttons so players can join the server's community without
        // having to track down the link separately. Empty strings hide
        // the corresponding button (the default — non-public servers
        // don't need to advertise either).
        public string SteamWorkshopURL { get; set; } = string.Empty;

        public string DiscordURL { get; set; } = string.Empty;

        public bool IsPrivate { get; set; } = false;

        public List<ModConfig> Mods { get; set; } = new List<ModConfig>();
    }
}
