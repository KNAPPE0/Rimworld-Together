using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using GameServer.Core;
using GameServer.Core.Configs;
using GameServer.Managers;
using GameServer.TCP;
using Shared;
using Shared.Packets.Data;
using static Shared.CommonEnumerators;

namespace GameServer.Misc
{
    public static class DiscordManager
    {
        // ───────────────────────── fields ─────────────────────────
        private static DiscordSocketClient _client = null!;
        private static DiscordConfigFile   _cfg    = null!;

        private static readonly SemaphoreSlim _presenceGate = new(1, 1);

        private static ulong? _liveStatsMsgId;
        private static Timer? _liveTimer;
        private static Timer? _midnightTimer;

        private static Color                 _embedColor;
        private static IReadOnlyList<string> _embedColumns = Array.Empty<string>();
        private static string                _titleTpl     = string.Empty;
        private static string                _footerTpl    = string.Empty;

        private static ConsoleTapBuffer _tapBuf = null!;

        // ─────────────────────── bootstrap ───────────────────────
        public static async Task InitializeAsync()
        {
            string cfgPath = Path.Combine(Master.ConfigsPath, "DiscordConfig.json");
            _cfg = Serializer.SerializeFromFile<DiscordConfigFile>(cfgPath);

            if (_cfg == null || !_cfg.Enabled)
            {
                Printer.Discord("[Discord] disabled in config.");
                return;
            }

            // colour & columns
            _embedColor = TryParseHex(_cfg.StatsEmbedColorHex) ?? Color.Blue;
            _embedColumns = _cfg.EmbedColumns?.Count switch
            {
                > 0 => _cfg.EmbedColumns
                          .Select(s => s.Trim().ToLowerInvariant())
                          .Where(col => col is "wealth" or "colonistcount" or "playtimeseconds" or "dayspassed")
                          .ToList(),
                _   => new[] { "wealth", "colonistcount", "playtimeseconds", "dayspassed" }
            };
            if (_embedColumns.Count == 0)
                _embedColumns = new[] { "wealth", "colonistcount", "playtimeseconds", "dayspassed" };

            _titleTpl  = string.IsNullOrWhiteSpace(_cfg.EmbedTitleTemplate)
                       ? "📊 Live Top {count} by {sortLabel}"
                       : _cfg.EmbedTitleTemplate.Trim();
            _footerTpl = string.IsNullOrWhiteSpace(_cfg.EmbedFooterTemplate)
                       ? "Updated on {timestampUtc}"
                       : _cfg.EmbedFooterTemplate.Trim();

            // console tap buffer
            _tapBuf = new ConsoleTapBuffer { SendToDiscord = SendConsoleMsgAsync };

            // discord client
            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel       = LogSeverity.Info,
                GatewayIntents = GatewayIntents.Guilds |
                                 GatewayIntents.GuildMessages |
                                 GatewayIntents.DirectMessages |
                                 GatewayIntents.MessageContent
            });
            _client.Log             += m => { Printer.Discord($"[Discord] {m}"); return Task.CompletedTask; };
            _client.Ready           += OnReady;
            _client.MessageReceived += OnMessageAsync;

            try
            {
                await _client.LoginAsync(TokenType.Bot, _cfg.BotToken);
                await _client.StartAsync();
            }
            catch (Exception ex)
            {
                Printer.Error($"[Discord] start-up failed: {ex.Message}");
            }
        }

        // ───────────────────── ready / presence ───────────────────
        private static async Task OnReady()
        {
            Printer.Discord("[Discord] ready.");
            await UpdatePresenceAsync();

            if (_cfg.StatsChannelId != 0)
            {
                await PostInitialLiveEmbedAsync();
                ScheduleMidnightTimer();
            }
        }

        public static async Task UpdatePresenceAsync()
        {
            if (!_cfg.UseOnlineCount || _client?.LoginState != LoginState.LoggedIn)
                return;
            if (!await _presenceGate.WaitAsync(0))
                return;

            try
            {
                var names = NetworkHelper.GetConnectedClientsSafe()
                                         .Select(c => c.UserFile.Label)
                                         .ToList();
                string status = names.Count switch
                {
                    0    => "No players online",
                    <= 5 => $"Players online: {names.Count}: {string.Join(", ", names)}",
                    _    => $"Players online: {names.Count}: {string.Join(", ", names.Take(5))} …"
                };
                await _client.SetGameAsync(status);
            }
            finally { _presenceGate.Release(); }
        }

        // ───────────────────── regex helpers ─────────────────────
        private static readonly Regex _guildEmoji = new(@"<a?:([A-Za-z0-9_]+):\d+>", RegexOptions.Compiled);

        // ────────────────── game → discord (chat) ─────────────────
        public static async Task SendChatMessageAsync(string user, string msg)
        {
            if (!_cfg.Enabled) return;
            if (_client.GetChannel(_cfg.ChatChannelId) is not IMessageChannel ch) return;

            msg = _guildEmoji.Replace(msg, m => $":{m.Groups[1].Value}:");
            await ch.SendMessageAsync($"**{user}**: {msg}", allowedMentions: AllowedMentions.None);

            if (_cfg.UseOnlineCount) await UpdatePresenceAsync();
        }

        // ─────── console logs → discord (new + legacy alias) ──────
        public static async Task SendConsoleMsgAsync(string payload)
        {
            if (!_cfg.Enabled) return;
            if (_client.GetChannel(_cfg.ConsoleChannelId) is not IMessageChannel ch) return;
            await ch.SendMessageAsync($"```{payload}```", allowedMentions: AllowedMentions.None);
        }

        // *** legacy alias (other classes still call this) ***
        public static Task SendConsoleMessageAsync(string payload) => SendConsoleMsgAsync(payload);

        // ────────────────── discord → server logic ────────────────
        private static Task OnMessageAsync(SocketMessage msg)
        {
            if (!_cfg.Enabled || msg.Author.IsBot) return Task.CompletedTask;

            string raw = msg.Content.Trim();
            ulong  cid = msg.Channel.Id;
            string tag1 = $"<@{_client.CurrentUser.Id}>", tag2 = $"<@!{_client.CurrentUser.Id}>";

            // console channel
            if (cid == _cfg.ConsoleChannelId)
            {
                if (raw.StartsWith(tag1) || raw.StartsWith(tag2))
                {
                    string cmd = raw[(raw.IndexOf('>') + 1)..].TrimStart();
                    if (!string.IsNullOrWhiteSpace(cmd))
                    {
                        string name = (msg.Author as SocketGuildUser)?.DisplayName ?? msg.Author.Username;
                        ConsoleManager.ProcessDiscordCommand(cmd, name);
                    }
                }
                return Task.CompletedTask;
            }

            // chat channel
            if (cid == _cfg.ChatChannelId)
            {
                // leaderboard command
                if (raw.StartsWith(tag1) || raw.StartsWith(tag2))
                {
                    string payload = raw[(raw.IndexOf('>') + 1)..].TrimStart();
                    payload = _guildEmoji.Replace(payload, m => $":{m.Groups[1].Value}:");
                    var parts = payload.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0 && parts[0].Equals("leaderboard", StringComparison.OrdinalIgnoreCase))
                    {
                        LeaderboardCommandParser.Parse(parts, out int limit, out string sortKey);

                        var embed = StatsEmbedBuilder.BuildLive(
                            limit, sortKey, _embedColor, _embedColumns, _titleTpl, _footerTpl);
                        _ = msg.Channel.SendMessageAsync(embed: embed);

                        SendLeaderboardSummaryToGame(limit, sortKey);
                    }
                    return Task.CompletedTask;
                }

                // normal chat → in-game
                var chatData = new ChatData
                {
                    _username      = msg.Author.Username,
                    _message       = raw,
                    _usernameColor = UserColor.Discord,
                    _messageColor  = MessageColor.Discord
                };
                NetworkHelper.SendPacketToAllClients(PacketHeader.ChatManager, chatData);
            }

            return Task.CompletedTask;
        }

        // ───────── helper: post neat summary back to RimWorld ─────
        private static void SendLeaderboardSummaryToGame(int limit, string sortKeyRaw)
        {
            string sortKey = sortKeyRaw.ToLowerInvariant();

            Func<StatisticsData, double> keySelector = sortKey switch
            {
                "colonistcount"   => s => s._colonistCount,
                "playtimeseconds" => s => s._playtimeSeconds,
                "dayspassed"      => s => s._daysPassed,
                _                 => s => s._wealth
            };

            string label = sortKey switch
            {
                "colonistcount"   => "Colonists",
                "playtimeseconds" => "Playtime",
                "dayspassed"      => "Days",
                _                 => "Wealth"
            };

            var rows = StatsManager.GetAllLiveStats()
                                   .OrderByDescending(keySelector)
                                   .Take(limit)
                                   .ToList();
            if (rows.Count == 0) return;

            var lines = new List<string> { $"Top {rows.Count} by {label}:" };

            for (int i = 0; i < rows.Count; i++)
            {
                var s = rows[i];
                string name = UserManagerH.GetUserFileFromName(s._uid)?.Label ?? s._uid;

                string value = sortKey switch
                {
                    "colonistcount"   => s._colonistCount.ToString(),
                    "playtimeseconds" =>
                        TimeSpan.FromSeconds(s._playtimeSeconds).ToString(@"h\h\ m\m"),
                    "dayspassed"      => s._daysPassed.ToString(),
                    _                 => $"${s._wealth:N0}"
                };

                lines.Add($"{i + 1}. {name} – {value}");
            }

            var data = new ChatData
            {
                _username      = "SERVER",
                _message       = string.Join('\n', lines),
                _usernameColor = UserColor.Server,
                _messageColor  = MessageColor.Server
            };
            NetworkHelper.SendPacketToAllClients(PacketHeader.ChatManager, data);
        }

        // ───────────────────── live-embed timers ──────────────────
        private static async Task PostInitialLiveEmbedAsync()
        {
            if (_client.GetChannel(_cfg.StatsChannelId) is not SocketTextChannel ch)
                return;

            var embed = StatsEmbedBuilder.BuildLive(
                10, "wealth", _embedColor, _embedColumns, _titleTpl, _footerTpl);
            _liveStatsMsgId = (await ch.SendMessageAsync(embed: embed)).Id;

            _liveTimer = new Timer(async _ =>
            {
                try { await RefreshLiveEmbedAsync(); } catch { }
            }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        private static async Task RefreshLiveEmbedAsync()
        {
            if (_liveStatsMsgId == null) return;
            if (_client.GetChannel(_cfg.StatsChannelId) is not SocketTextChannel ch) return;

            if (await ch.GetMessageAsync(_liveStatsMsgId.Value) is IUserMessage msg)
            {
                var embed = StatsEmbedBuilder.BuildLive(
                    10, "wealth", _embedColor, _embedColumns, _titleTpl, _footerTpl);
                await msg.ModifyAsync(p => p.Embed = embed);
            }
        }

        private static void ScheduleMidnightTimer()
        {
            DateTime now  = DateTime.UtcNow;
            DateTime next = new DateTime(now.Year, now.Month, now.Day).AddDays(1);
            TimeSpan delay = next - now;

            _midnightTimer = new Timer(async _ =>
            {
                _ = PostInitialLiveEmbedAsync();
                ScheduleMidnightTimer();
            }, null, delay, Timeout.InfiniteTimeSpan);
        }

        // ───────────────────── shutdown / util ────────────────────
        public static async Task ShutdownAsync()
        {
            _liveTimer?.Dispose();
            _midnightTimer?.Dispose();

            if (_client != null)
            {
                await _client.LogoutAsync();
                await _client.StopAsync();
                await _client.DisposeAsync();
            }
            Printer.Discord("[Discord] disconnected.");
        }

        private static Color? TryParseHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return null;
            hex = hex.Trim().TrimStart('#');
            return int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb)
                 ? new Color((uint)rgb)
                 : null;
        }
    }
}
