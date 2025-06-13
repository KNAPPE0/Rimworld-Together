// File: DiscordConfigFile.cs  (Server File)
using System;
using System.Collections.Generic;

namespace GameServer.Core.Configs
{
    [Serializable]
    public class DiscordConfigFile
    {
        public bool Enabled = false;
        public string BotToken = "YOUR_DISCORD_BOT_TOKEN";
        public ulong ChatChannelId = 0UL;
        public ulong ConsoleChannelId = 0UL;
        public ulong StatsChannelId = 0UL;
        public bool UseOnlineCount = true;

        // Live‐stats embed customization:
        public string StatsEmbedColorHex = "#1E90FF";
        public List<string> EmbedColumns = new List<string>(); 
        public string EmbedTitleTemplate = "📊 Live Top {count} by {sortLabel}";
        public string EmbedFooterTemplate = "Updated on {timestampUtc}";
    }
}