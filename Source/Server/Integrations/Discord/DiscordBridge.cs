using Discord;
using Discord.WebSocket;
using GameServer.Managers;
using GameServer.PacketManager;
using Shared.Misc;
using Shared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static Shared.Misc.Printer;

namespace GameServer.Integrations.Discord
{
    public static class DiscordBridge
    {
        private static readonly SemaphoreSlim StartSemaphore = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim SendSemaphore = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim MentionResolveSemaphore = new SemaphoreSlim(1, 1);

        private static DiscordSocketClient Client { get; set; }
        private static bool Started { get; set; }
        private static bool StopRequested { get; set; }

        private static ulong ChatChannelId { get; set; }
        private static ulong AdminChannelId { get; set; }
        private static ulong LeaderboardChannelId { get; set; }
        private static string CommandPrefix { get; set; } = "!";

        private static HashSet<ulong> AdminRoleIds { get; set; } = new HashSet<ulong>();

        // KMH: This server's identity tag, used to label outbound messages
        // and to differentiate them from other servers sharing the same channel.
        public static string ServerTag { get; private set; } = "S?";

        // Recognises a "bridge-formatted" chat line so the receiving bot can
        // pull the (tag, user, message) tuple back out cleanly. Format we emit:
        //     **[TAG] User:** message body
        // We rely on Discord's own bot-author check (raw.Author.Id != our bot id)
        // to distinguish "from another server" from "our own echo".
        private static readonly Regex BridgeChatRegex =
            new Regex(@"^\*\*\[(?<tag>[A-Za-z0-9_\-]{1,16})\]\s+(?<user>[^:*]{1,64})\:\*\*\s*(?<msg>.+)$",
                RegexOptions.Compiled | RegexOptions.Singleline);

        // Per-server cross-server bridge toggle.
        public static bool CrossServerBridgeEnabled { get; private set; }

        // Console embed/severity controls.
        public static bool UseEmbedsForConsole { get; private set; } = true;
        public static int ConsoleMinSeverity { get; private set; } = 0; // 0=all,1=warn+,2=error

        private static readonly ConcurrentQueue<OutboundMessage> Outbox = new ConcurrentQueue<OutboundMessage>();
        private static readonly SemaphoreSlim OutboxSignal = new SemaphoreSlim(0, int.MaxValue);
        private static Task OutboxWorkerTask { get; set; }

        // Console-embed coalescing buffer. Each warning/error that
        // arrives within ConsoleEmbedBatchWindowMs of the previous one is
        // appended into the same embed body so admin commands that print
        // many lines (e.g. !help) become a single Discord card instead
        // of N separate embeds.
        private static readonly object ConsoleEmbedLock = new object();
        private static List<string> ConsoleEmbedBuffer { get; set; } = new List<string>();
        private static LogMode ConsoleEmbedBufferMode { get; set; } = LogMode.Warning;
        private static System.Threading.Timer ConsoleEmbedFlushTimer { get; set; }
        private static int ConsoleEmbedBatchWindowMs { get; set; } = 1500;

        private static readonly AllowedMentions NoMentions = AllowedMentions.None;

        private static readonly ConcurrentDictionary<string, MentionCacheEntry> MentionCache =
            new ConcurrentDictionary<string, MentionCacheEntry>(StringComparer.OrdinalIgnoreCase);

        private static readonly Regex MentionTokenRegex =
            new Regex(@"(?<!\w)@(?:""([^""]{1,32})""|'([^']{1,32})'|([^\s@]{1,32}))", RegexOptions.Compiled);

        private static int BatchWindowMs { get; set; } = 250;
        private static int BurstCount { get; set; } = 4;
        private static int BurstWindowMs { get; set; } = 100;
        private static int MaxChunkLen { get; set; } = 1900;

        private static int MentionCacheMinutes { get; set; } = 30;
        private static int MaxMentionsPerMessage { get; set; } = 10;

        private static int ConsoleMirrorWindowMs { get; set; } = 2500;
        private static DateTime ConsoleMirrorUntilUtc { get; set; } = DateTime.MinValue;

        private static readonly object ConsoleRelayLock = new object();
        private static string LastConsoleRelayKey { get; set; } = string.Empty;
        private static DateTime LastConsoleRelayUtc { get; set; } = DateTime.MinValue;
        private static int LastConsoleRelayRepeats { get; set; } = 0;
        private static int ConsoleRepeatWindowMs { get; set; } = 1200;
        private static int ConsoleRepeatEvery { get; set; } = 5;

        public static void BeginConsoleMirrorWindow()
        {
            ConsoleMirrorUntilUtc = DateTime.UtcNow.AddMilliseconds(ConsoleMirrorWindowMs);
        }

        public static void TryRelayConsoleCommandToDiscord(string command)
        {
            if (!Started) return;
            if (Client == null) return;
            if (AdminChannelId == 0) return;
            if (string.IsNullOrWhiteSpace(command)) return;

            BeginConsoleMirrorWindow();
            Enqueue(AdminChannelId, $"`> {command.Trim()}`");
        }

        public static void TryRelayServerConsoleLine(string text, LogMode mode)
        {
            if (!Started) return;
            if (Client == null) return;
            if (AdminChannelId == 0) return;
            if (string.IsNullOrWhiteSpace(text)) return;

            bool isWarn = mode == LogMode.Warning;
            bool isError = mode == LogMode.Error;
            bool isImportant = isWarn || isError;

            // KMH: severity gate — admins can mute info-level relays.
            if (ConsoleMinSeverity >= 2 && !isError) return;
            if (ConsoleMinSeverity >= 1 && !isImportant) return;

            if (!isImportant && DateTime.UtcNow > ConsoleMirrorUntilUtc) return;

            string cleaned = text.TrimEnd();

            bool shouldSend = true;
            lock (ConsoleRelayLock)
            {
                string key = ((int)mode).ToString() + "|" + cleaned;
                DateTime now = DateTime.UtcNow;

                if (key == LastConsoleRelayKey && (now - LastConsoleRelayUtc).TotalMilliseconds <= ConsoleRepeatWindowMs)
                {
                    LastConsoleRelayRepeats++;

                    if (ConsoleRepeatEvery > 1 && (LastConsoleRelayRepeats % ConsoleRepeatEvery) != 0)
                    {
                        shouldSend = false;
                    }
                    else
                    {
                        cleaned = $"{cleaned} (repeated {LastConsoleRelayRepeats}x)";
                    }

                    LastConsoleRelayUtc = now;
                }
                else
                {
                    LastConsoleRelayKey = key;
                    LastConsoleRelayRepeats = 0;
                    LastConsoleRelayUtc = now;
                }
            }

            if (!shouldSend) return;

            // KMH: warnings + errors get an embed card with severity colour.
            if (UseEmbedsForConsole && isImportant)
            {
                EnqueueConsoleEmbedLine(mode, cleaned);
                return;
            }

            string prefix = "";
            if (isWarn) prefix = "⚠️ ";
            else if (isError) prefix = "❌ ";
            else if (mode == LogMode.Title) prefix = "✅ ";

            Enqueue(AdminChannelId, $"`[{ServerTag}]` `[{DateTime.Now:HH:mm:ss}]` {prefix}{cleaned}");
        }

        // KMH: Embed-styled console card for warnings/errors. Bypasses the
        // text-batching outbox so colour/title render correctly.
        /// <summary>
        /// KMH: If a bridge-formatted chat line comes from another server's bot
        /// in our chat channel, parse it and re-broadcast to our players.
        /// Returns true if the message was a cross-server chat (and was handled),
        /// otherwise false so the caller can drop it like any other bot noise.
        /// </summary>
        private static bool TryHandleCrossServerBridge(SocketMessage raw)
        {
            try
            {
                if (!CrossServerBridgeEnabled) return false;
                if (ChatChannelId == 0 || raw.Channel.Id != ChatChannelId) return false;

                string content = raw.Content?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(content)) return false;

                Match m = BridgeChatRegex.Match(content);
                if (!m.Success) return false;

                string fromTag = m.Groups["tag"].Value;
                string fromUser = m.Groups["user"].Value.Trim();
                string fromMsg = m.Groups["msg"].Value.Trim();

                // Defence-in-depth: if a bot somehow re-relays our own tag, ignore.
                if (string.Equals(fromTag, ServerTag, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (string.IsNullOrWhiteSpace(fromUser) || string.IsNullOrWhiteSpace(fromMsg))
                    return true;

                // Cap length so a hostile actor can't flood our chat.
                if (fromMsg.Length > 1000) fromMsg = fromMsg.Substring(0, 1000);

                // Surface as Discord-coloured chat with a clear cross-server tag
                // so our players can see WHICH server it came from.
                PM_Chat.BroadcastDiscordMessage($"[{fromTag}] {fromUser}", fromMsg);

                return true;
            }
            catch (Exception e)
            {
                Printer.Error($"[Discord] CrossServerBridge parse error: {e}");
                return false;
            }
        }

        /// <summary>
        /// Coalesces console embed lines that arrive close together
        /// into a single embed. The first arrival starts a timer; subsequent
        /// lines join the same embed until the timer fires. If an error
        /// arrives while the buffer is mid-warning, it elevates the whole
        /// batch to error severity (highest wins).
        /// </summary>
        private static void EnqueueConsoleEmbedLine(LogMode mode, string text)
        {
            if (Client == null || AdminChannelId == 0) return;

            bool startTimer = false;
            lock (ConsoleEmbedLock)
            {
                if (ConsoleEmbedBuffer.Count == 0)
                {
                    ConsoleEmbedBufferMode = mode;
                    startTimer = true;
                }
                else if (mode == LogMode.Error && ConsoleEmbedBufferMode != LogMode.Error)
                {
                    ConsoleEmbedBufferMode = LogMode.Error;
                }

                ConsoleEmbedBuffer.Add(text);
            }

            if (startTimer)
            {
                ConsoleEmbedFlushTimer?.Dispose();
                ConsoleEmbedFlushTimer = new System.Threading.Timer(
                    _ => FlushConsoleEmbedBuffer(),
                    null,
                    ConsoleEmbedBatchWindowMs,
                    System.Threading.Timeout.Infinite);
            }
        }

        private static void FlushConsoleEmbedBuffer()
        {
            List<string> lines;
            LogMode mode;
            lock (ConsoleEmbedLock)
            {
                if (ConsoleEmbedBuffer.Count == 0) return;
                lines = ConsoleEmbedBuffer;
                mode = ConsoleEmbedBufferMode;
                ConsoleEmbedBuffer = new List<string>();
            }

            _ = Task.Run(() => SendConsoleEmbedAsync(mode, lines));
        }

        private static async Task SendConsoleEmbedAsync(LogMode mode, List<string> lines)
        {
            try
            {
                if (Client == null) return;
                var channel = Client.GetChannel(AdminChannelId) as IMessageChannel;
                if (channel == null) return;

                var color = mode == LogMode.Error
                    ? new Color(220, 80, 80)
                    : new Color(255, 196, 97);
                string title = mode == LogMode.Error
                    ? (lines.Count > 1 ? $"❌ Server Errors (×{lines.Count})" : "❌ Server Error")
                    : (lines.Count > 1 ? $"⚠️ Server Warnings (×{lines.Count})" : "⚠️ Server Warning");

                StringBuilder sb = new StringBuilder();
                foreach (string line in lines)
                {
                    if (string.IsNullOrEmpty(line)) continue;
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(line);
                }

                string body = sb.ToString();
                // Discord embed description cap is 4096; leave headroom for code-block fences.
                if (body.Length > 3800) body = body.Substring(0, 3800) + "\n…(truncated)";

                var eb = new EmbedBuilder()
                    .WithTitle(title)
                    .WithDescription($"```\n{body}\n```")
                    .WithColor(color)
                    .WithFooter($"{ServerTag} · {DateTime.Now:HH:mm:ss}")
                    .WithTimestamp(DateTimeOffset.UtcNow);

                await SendSemaphore.WaitAsync();
                try { await channel.SendMessageAsync(embed: eb.Build(), allowedMentions: NoMentions); }
                finally { SendSemaphore.Release(); }
            }
            catch (Exception e) { Printer.Error($"[Discord] SendConsoleEmbed error: {e}"); }
        }

        // This is intentionally NOT timestamped and NOT gated by the mirror window.
        public static void TryRelayServerNoticeToDiscordChat(string text)
        {
            if (!Started) return;
            if (Client == null) return;
            if (ChatChannelId == 0) return;
            if (string.IsNullOrWhiteSpace(text)) return;

            string cleaned = SanitizeDiscordText(text.Trim());
            Enqueue(ChatChannelId, cleaned);
        }

        /// <summary>
        /// KMH: Send a rich embed to the chat channel (or admin if chat is unset).
        /// Embeds bypass the text-batching outbox and ship directly so the
        /// formatting is preserved.
        /// </summary>
        public static void TryRelayEmbedToDiscordChat(Embed embed)
        {
            if (!Started || Client == null || embed == null) return;
            ulong target = ChatChannelId != 0 ? ChatChannelId : AdminChannelId;
            if (target == 0) return;
            _ = Task.Run(() => SendEmbedAsync(target, embed));
        }

        private static async Task SendEmbedAsync(ulong channelId, Embed embed)
        {
            try
            {
                var channel = Client.GetChannel(channelId) as IMessageChannel;
                if (channel == null) return;
                await SendSemaphore.WaitAsync();
                try { await channel.SendMessageAsync(embed: embed, allowedMentions: NoMentions); }
                finally { SendSemaphore.Release(); }
            }
            catch (Exception e) { Printer.Error($"[Discord] SendEmbed error: {e}"); }
        }

        /// <summary>
        /// Result of a showcase post — the channel the message lives
        /// in (text channel ID or forum thread ID) and the message ID inside
        /// it. Stored on UserFile so subsequent edits know what to update.
        /// </summary>
        public readonly struct ShowcasePostResult
        {
            public readonly ulong ChannelId;
            public readonly ulong MessageId;
            public readonly string PermalinkOrError;

            public ShowcasePostResult(ulong channelId, ulong messageId, string permalinkOrError)
            {
                ChannelId = channelId;
                MessageId = messageId;
                PermalinkOrError = permalinkOrError;
            }

            public bool Success => ChannelId != 0 && MessageId != 0;
        }

        /// <summary>
        /// Post or edit a player's `!showcase` embed. Handles both
        /// forum channels (one thread per user, edits the OP on update) and
        /// regular text channels (one edit-in-place message per user).
        ///
        /// Flow:
        ///   * If <paramref name="existingChannelId"/> + <paramref name="existingMessageId"/>
        ///     are set, try to edit that message. On success, return the same IDs.
        ///   * On edit failure (deleted, thread archived, etc.) or first call,
        ///     create a new post:
        ///       - Forum channel → CreatePostAsync (new thread, OP carries the embed)
        ///       - Text channel  → SendMessageAsync (new message)
        ///     Return the new IDs.
        ///
        /// Returns a struct with the resulting IDs and a human-readable
        /// permalink (for the chat-reply to the user) or an error string.
        /// </summary>
        public static async Task<ShowcasePostResult> PostOrEditShowcaseAsync(
            ulong showcaseChannelId,
            string threadTitle,
            Embed embed,
            ulong existingChannelId,
            ulong existingMessageId)
        {
            if (!Started || Client == null || embed == null || showcaseChannelId == 0)
                return new ShowcasePostResult(0, 0, "Discord bridge not started or showcase channel not configured.");

            try
            {
                await SendSemaphore.WaitAsync();
                try
                {
                    // -- Try to edit the existing post first. --
                    if (existingChannelId != 0 && existingMessageId != 0)
                    {
                        try
                        {
                            var existingChannel = Client.GetChannel(existingChannelId) as IMessageChannel;
                            if (existingChannel != null)
                            {
                                var existingMsg = await existingChannel.GetMessageAsync(existingMessageId);
                                if (existingMsg is IUserMessage editable)
                                {
                                    await editable.ModifyAsync(props =>
                                    {
                                        props.Embed = embed;
                                        props.Content = string.Empty;
                                    });
                                    return new ShowcasePostResult(existingChannelId, existingMessageId,
                                        BuildJumpUrl(existingChannelId, existingMessageId));
                                }
                            }
                        }
                        catch (Exception editErr)
                        {
                            Printer.Warning($"[Discord] Showcase edit failed (will repost): {editErr.Message}", LogImportanceMode.Verbose);
                        }
                    }

                    // -- No editable post → create a new one. Branch on
                    //    forum vs text channel. --
                    var target = Client.GetChannel(showcaseChannelId);
                    if (target == null)
                        return new ShowcasePostResult(0, 0, "Configured showcase channel not found.");

                    if (target is IForumChannel forum)
                    {
                        // Trim the thread title to Discord's 100-char limit.
                        string safeTitle = string.IsNullOrWhiteSpace(threadTitle) ? "Player Showcase" : threadTitle.Trim();
                        if (safeTitle.Length > 100) safeTitle = safeTitle.Substring(0, 100);

                        var thread = await forum.CreatePostAsync(
                            title: safeTitle,
                            archiveDuration: ThreadArchiveDuration.OneWeek,
                            embeds: new[] { embed },
                            allowedMentions: NoMentions);

                        // The OP message in a forum thread is the FIRST message
                        // inside the thread. We need its ID for future edits.
                        ulong opId = 0;
                        try
                        {
                            await foreach (var page in thread.GetMessagesAsync(1))
                            {
                                foreach (var msg in page)
                                {
                                    opId = msg.Id;
                                    break;
                                }
                                if (opId != 0) break;
                            }
                        }
                        catch (Exception getErr)
                        {
                            Printer.Warning($"[Discord] Showcase: couldn't fetch OP message ID: {getErr.Message}");
                        }

                        return new ShowcasePostResult(thread.Id, opId, BuildJumpUrl(thread.Id, opId));
                    }
                    else if (target is IMessageChannel textCh)
                    {
                        var newMsg = await textCh.SendMessageAsync(embed: embed, allowedMentions: NoMentions);
                        ulong newId = newMsg?.Id ?? 0;
                        return new ShowcasePostResult(showcaseChannelId, newId, BuildJumpUrl(showcaseChannelId, newId));
                    }
                    else
                    {
                        return new ShowcasePostResult(0, 0, "Configured showcase channel is not a text or forum channel.");
                    }
                }
                finally { SendSemaphore.Release(); }
            }
            catch (Exception e)
            {
                Printer.Error($"[Discord] PostOrEditShowcase error: {e}");
                return new ShowcasePostResult(0, 0, $"Discord error: {e.Message}");
            }
        }

        /// <summary>
        /// Delete a previously-posted showcase. Returns true if the
        /// message was successfully removed (or already gone). Forum threads
        /// are deleted entirely; text-channel messages are removed without
        /// touching anything else.
        /// </summary>
        public static async Task<bool> DeleteShowcaseAsync(ulong channelId, ulong messageId)
        {
            if (!Started || Client == null || channelId == 0) return false;

            try
            {
                await SendSemaphore.WaitAsync();
                try
                {
                    var ch = Client.GetChannel(channelId);
                    if (ch is IThreadChannel thread)
                    {
                        await thread.DeleteAsync();
                        return true;
                    }
                    if (ch is IMessageChannel textCh && messageId != 0)
                    {
                        try { await textCh.DeleteMessageAsync(messageId); }
                        catch { /* already deleted - fine */ }
                        return true;
                    }
                }
                finally { SendSemaphore.Release(); }
            }
            catch (Exception e)
            {
                Printer.Warning($"[Discord] DeleteShowcase error: {e.Message}");
            }
            return false;
        }

        private static string BuildJumpUrl(ulong channelId, ulong messageId)
        {
            // Format: https://discord.com/channels/{guildId}/{channelId}/{messageId}
            ulong guildId = GetPrimaryGuild()?.Id ?? 0;
            if (guildId == 0 || channelId == 0 || messageId == 0) return string.Empty;
            return $"https://discord.com/channels/{guildId}/{channelId}/{messageId}";
        }

        /// <summary>
        /// KMH: Post or edit a leaderboard embed. Channel precedence:
        ///   1. <c>DiscordLeaderboardChannelId</c> (dedicated channel)
        ///   2. <c>DiscordChatChannelId</c> (chat fallback)
        ///   3. <c>DiscordAdminChannelId</c> (last resort)
        ///
        /// If <paramref name="existingMessageId"/> is non-zero, attempts to
        /// edit that message; on edit failure (deleted, etc.) falls back to
        /// posting a new one. Returns the resulting message ID, or 0 on failure.
        /// </summary>
        public static async Task<ulong> PostOrEditEmbedAsync(Embed embed, ulong existingMessageId)
        {
            if (!Started || Client == null || embed == null) return 0;
            // KMH: Prefer the dedicated leaderboard channel when configured,
            // otherwise fall through to chat → admin like before.
            ulong target = LeaderboardChannelId != 0
                ? LeaderboardChannelId
                : (ChatChannelId != 0 ? ChatChannelId : AdminChannelId);
            if (target == 0) return 0;

            try
            {
                var channel = Client.GetChannel(target) as IMessageChannel;
                if (channel == null) return 0;

                await SendSemaphore.WaitAsync();
                try
                {
                    if (existingMessageId != 0)
                    {
                        try
                        {
                            var msg = await channel.GetMessageAsync(existingMessageId);
                            if (msg is IUserMessage editable)
                            {
                                await editable.ModifyAsync(props =>
                                {
                                    props.Embed = embed;
                                    props.Content = string.Empty;
                                });
                                return existingMessageId;
                            }
                        }
                        catch (Exception editErr)
                        {
                            Printer.Warning($"[Discord] Edit failed (will repost): {editErr.Message}", LogImportanceMode.Verbose);
                        }
                    }

                    var newMsg = await channel.SendMessageAsync(embed: embed, allowedMentions: NoMentions);
                    return newMsg?.Id ?? 0;
                }
                finally { SendSemaphore.Release(); }
            }
            catch (Exception e)
            {
                Printer.Error($"[Discord] PostOrEditEmbed error: {e}");
                return 0;
            }
        }

        public static void TryStart()
        {
            _ = Task.Run(StartAsync);
        }

        private static async Task StartAsync()
        {
            await StartSemaphore.WaitAsync();
            try
            {
                if (Started) return;
                if (GameServer.Core.Master.ServerConfig == null) return;

                var cfg = GameServer.Core.Master.ServerConfig;
                if (!cfg.EnableDiscordBridge) return;

                var token = (cfg.DiscordBotToken ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(token))
                {
                    Printer.Warning("[Discord] EnableDiscordBridge is true but DiscordBotToken is empty.");
                    return;
                }

                ChatChannelId = ParseUlong(cfg.DiscordChatChannelId);
                AdminChannelId = ParseUlong(cfg.DiscordAdminChannelId);
                LeaderboardChannelId = ParseUlong(cfg.DiscordLeaderboardChannelId);
                CommandPrefix = string.IsNullOrWhiteSpace(cfg.DiscordCommandPrefix) ? "!" : cfg.DiscordCommandPrefix.Trim();
                AdminRoleIds = ParseRoleIds(cfg.DiscordAdminRoleIdsCsv);

                // KMH: server-identity + multi-server config.
                ServerTag = SanitiseServerTag(cfg.DiscordServerTag, cfg.Port);
                CrossServerBridgeEnabled = cfg.EnableCrossServerChatBridge;
                UseEmbedsForConsole = cfg.DiscordUseEmbedsForConsole;
                ConsoleMinSeverity = Math.Max(0, Math.Min(2, cfg.DiscordConsoleMinSeverity));

                if (ChatChannelId == 0 && AdminChannelId == 0)
                {
                    Printer.Warning("[Discord] DiscordChatChannelId and DiscordAdminChannelId are both missing/invalid.");
                    return;
                }

                var socketCfg = new DiscordSocketConfig
                {
                    GatewayIntents =
                        GatewayIntents.Guilds |
                        GatewayIntents.GuildMessages |
                        GatewayIntents.MessageContent,
                    AlwaysDownloadUsers = false,
                    MessageCacheSize = 0
                };

                Client = new DiscordSocketClient(socketCfg);
                Client.MessageReceived += OnMessageReceivedAsync;
                Client.ButtonExecuted += OnButtonExecutedAsync;

                await Client.LoginAsync(TokenType.Bot, token);
                await Client.StartAsync();

                Started = true;
                StopRequested = false;

                OutboxWorkerTask = Task.Run(OutboxWorkerAsync);
                DiscordPresence.TryStart(Client);

                Printer.Title("[Discord] Bridge started");
            }
            catch (Exception e)
            {
                Printer.Error($"[Discord] Bridge failed to start: {e}");
            }
            finally
            {
                StartSemaphore.Release();
            }
        }

        public static void TryStop()
        {
            _ = Task.Run(StopAsync);
        }

        private static async Task StopAsync()
        {
            try
            {
                if (!Started) return;

                StopRequested = true;
                OutboxSignal.Release();
                DiscordPresence.TryStop();

                if (OutboxWorkerTask != null)
                {
                    try { await OutboxWorkerTask; }
                    catch { }
                }

                if (Client != null)
                {
                    try { await Client.StopAsync(); } catch { }
                    try { await Client.LogoutAsync(); } catch { }
                }
            }
            catch { }
            finally
            {
                Started = false;
                Client = null;
            }
        }

        private static async Task OnMessageReceivedAsync(SocketMessage raw)
        {
            try
            {
                if (!Started) return;

                ulong ourBotId = Client?.CurrentUser?.Id ?? 0;
                bool authorIsBot = raw.Author?.IsBot == true;
                bool authorIsUs = raw.Author != null && raw.Author.Id == ourBotId;

                // KMH: Cross-server chat bridge.
                // Skip our own echoes always. Skip other bots unless the bridge is on
                // AND this is a bridge-formatted chat message from a different server.
                if (authorIsUs) return;
                if (authorIsBot && !TryHandleCrossServerBridge(raw)) return;

                var content = raw.Content?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(content)) return;

                // KMH: Optional bot-mention gate. When DiscordRequireBotMention
                // is on, every command must be addressed via @MyBot first —
                // critical for multi-bot channels (multiple RWT servers in one
                // Discord guild) so only the pinged bot responds.
                bool requireMention = GameServer.Core.Master.ServerConfig?.DiscordRequireBotMention ?? false;
                bool looksLikeCommand = content.StartsWith("!", StringComparison.Ordinal);

                if (looksLikeCommand && requireMention)
                {
                    if (!TryStripLeadingBotMention(ref content, ourBotId))
                        return; // no mention → not directed at us
                }
                else
                {
                    // Even when the gate is off, allow optional leading mention
                    // (so users can always `@MyBot !market` if they like).
                    TryStripLeadingBotMention(ref content, ourBotId);
                }

                // KMH: Handle !link command from any channel or DM
                if (content.StartsWith("!link ", StringComparison.OrdinalIgnoreCase))
                {
                    string token = content.Substring(6).Trim().ToUpper();
                    await HandleLinkCommand(raw, token);
                    return;
                }

                // !profile / !whoami — own status, or public profile of <username>. No admin flags leaked.
                if (content.Equals("!profile", StringComparison.OrdinalIgnoreCase) ||
                    content.Equals("!whoami", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleProfileCommand(raw, null);
                    return;
                }
                if (content.StartsWith("!profile ", StringComparison.OrdinalIgnoreCase))
                {
                    string target = content.Substring(9).Trim();
                    await HandleProfileCommand(raw, target);
                    return;
                }

                // KMH: Marketplace / treasury / quest commands run from any channel
                // the bot can read. Dispatched BEFORE the chat-broadcast path so they
                // don't leak into the in-game chat as a normal message.
                if (await DiscordMarketCommands.TryDispatchAsync(raw, content))
                    return;

                if (ChatChannelId != 0 && raw.Channel.Id == ChatChannelId)
                {
                    var name = GetBestName(raw);
                    var msg = SanitizeGameTextFromDiscord(raw, content);
                    if (msg.Length > 5000) msg = msg.Substring(0, 5000);

                    PM_Chat.BroadcastDiscordMessage(name, msg);
                    return;
                }

                if (AdminChannelId != 0 && raw.Channel.Id == AdminChannelId)
                {
                    if (!content.StartsWith(CommandPrefix, StringComparison.Ordinal)) return;
                    if (!IsAuthorizedAdmin(raw))
                    {
                        await raw.Channel.SendMessageAsync(embed: DiscordResponseBuilder.Build(
                            DiscordResponseBuilder.Style.Error, "Not Authorized",
                            "You don't have permission to run admin commands here."));
                        return;
                    }

                    var cmd = content.Substring(CommandPrefix.Length).Trim();
                    if (string.IsNullOrWhiteSpace(cmd)) return;

                    // Capture output explicitly — DON'T open the console mirror window,
                    // because it would re-relay every Printer.Warning line as its own
                    // "Server Warning" embed. We collect once, render once.
                    string[] outputLines = CMD_Base.ExecuteCommand(cmd, fromDiscord: true);

                    if (outputLines == null || outputLines.Length == 0)
                    {
                        await raw.Channel.SendMessageAsync(embed: DiscordResponseBuilder.Build(
                            DiscordResponseBuilder.Style.Success, "Executed",
                            $"`{cmd}` ran with no output."));
                        return;
                    }

                    // Strip standard log-level prefixes ("[OPTIONS]", " > ") that the
                    // Printer adds, so the embed is clean.
                    List<string> cleanedLines = new List<string>(outputLines.Length);
                    foreach (string raw1 in outputLines)
                    {
                        string ln = (raw1 ?? string.Empty).TrimEnd();
                        if (ln.Length == 0) continue;
                        cleanedLines.Add(ln);
                    }

                    // Pagination: Discord embed body cap is ~4096 chars. We chunk
                    // cleanedLines into pages whose size is driven by the
                    // DiscordOutputMode config (Compact/Normal/Verbose).
                    Embed pagedEmbed = DiscordResponseBuilder.BuildPaged(
                        DiscordResponseBuilder.Style.Admin,
                        $"`!{cmd}`",
                        cleanedLines,
                        pageOneIndexed: 1,
                        pageSize: DiscordResponseBuilder.PageSizeFromConfig,
                        footerHint: "use admin console for more pages");

                    await raw.Channel.SendMessageAsync(embed: pagedEmbed);
                }
            }
            catch (Exception e)
            {
                Printer.Error($"[Discord] MessageReceived error: {e}");
            }
        }

        // Dispatches "buy:<listingId>:<qty>" buttons to the marketplace handler.
        private static async Task OnButtonExecutedAsync(SocketMessageComponent component)
        {
            try
            {
                string id = component.Data?.CustomId ?? string.Empty;
                if (id.StartsWith("buy:", StringComparison.Ordinal))
                {
                    await DiscordMarketCommands.HandleBuyButtonAsync(component, id);
                    return;
                }
                // Unknown button → acknowledge so Discord doesn't show "this
                // interaction failed" for buttons from a different bot
                // version still hanging around.
                await component.DeferAsync(ephemeral: true);
            }
            catch (Exception e)
            {
                Printer.Warning($"[Discord] ButtonExecuted error: {e}");
                try { await component.RespondAsync($"❌ Button error: {e.Message}", ephemeral: true); }
                catch { }
            }
        }

        public static void TryRelayGameChatToDiscord(string username, string message)
        {
            if (!Started) return;
            if (Client == null) return;
            if (ChatChannelId == 0) return;
            if (string.IsNullOrWhiteSpace(message)) return;

            _ = Task.Run(() => RelayGameChatToDiscordAsync(username, message));
        }

        private static async Task RelayGameChatToDiscordAsync(string username, string message)
        {
            try
            {
                if (!Started) return;
                if (Client == null) return;
                if (ChatChannelId == 0) return;

                var safeUser = Escape(username);
                var safeMsg = SanitizeDiscordText(message);

                ulong[] mentionUserIds = Array.Empty<ulong>();
                var guild = GetPrimaryGuild();
                if (guild != null)
                {
                    var mentionResult = await ApplyUserMentionsAsync(guild, safeMsg);
                    safeMsg = mentionResult.Text;
                    mentionUserIds = mentionResult.UserIds;
                }

                // KMH: Tag with server identity. Format must match BridgeChatRegex
                // so other servers' bots can parse + relay back in-game.
                Enqueue(ChatChannelId, $"**[{ServerTag}] {safeUser}:** {safeMsg}", mentionUserIds);
            }
            catch (Exception e)
            {
                Printer.Error($"[Discord] RelayGameChatToDiscord error: {e}");
            }
        }

        private static void Enqueue(ulong channelId, string text, IReadOnlyCollection<ulong> mentionUserIds = null)
        {
            if (!Started) return;
            if (Client == null) return;
            if (channelId == 0) return;
            if (string.IsNullOrWhiteSpace(text)) return;

            Outbox.Enqueue(new OutboundMessage(channelId, text, mentionUserIds));
            OutboxSignal.Release();
        }

        private static async Task OutboxWorkerAsync()
        {
            DateTime burstStart = DateTime.UtcNow;
            int sentInBurst = 0;

            while (!StopRequested)
            {
                try
                {
                    await OutboxSignal.WaitAsync();

                    if (StopRequested) break;
                    if (Client == null) continue;

                    if (!Outbox.TryDequeue(out var first))
                        continue;

                    StringBuilder sb = new StringBuilder();
                    sb.Append(first.Text);

                    HashSet<ulong> mentionIds = null;
                    if (first.UserMentionIds != null && first.UserMentionIds.Length > 0)
                        mentionIds = new HashSet<ulong>(first.UserMentionIds);

                    DateTime batchUntil = DateTime.UtcNow.AddMilliseconds(BatchWindowMs);
                    while (DateTime.UtcNow < batchUntil && sb.Length < MaxChunkLen)
                    {
                        if (!Outbox.TryPeek(out var next)) break;
                        if (next.ChannelId != first.ChannelId) break;

                        if (!Outbox.TryDequeue(out next)) break;

                        string toAdd = "\n" + next.Text;
                        if (sb.Length + toAdd.Length > MaxChunkLen) break;

                        sb.Append(toAdd);

                        if (next.UserMentionIds != null && next.UserMentionIds.Length > 0)
                        {
                            mentionIds ??= new HashSet<ulong>();
                            foreach (var id in next.UserMentionIds) mentionIds.Add(id);
                        }
                    }

                    double elapsed = (DateTime.UtcNow - burstStart).TotalMilliseconds;
                    if (elapsed >= BurstWindowMs)
                    {
                        burstStart = DateTime.UtcNow;
                        sentInBurst = 0;
                    }
                    else if (sentInBurst >= BurstCount)
                    {
                        int wait = Math.Max(0, BurstWindowMs - (int)elapsed);
                        if (wait > 0) await Task.Delay(wait);
                        burstStart = DateTime.UtcNow;
                        sentInBurst = 0;
                    }

                    await SendToChannelAsync(first.ChannelId, sb.ToString(), mentionIds);
                    sentInBurst++;
                }
                catch (Exception e)
                {
                    Printer.Error($"[Discord] Outbox worker error: {e}");
                }
            }
        }

        private static async Task SendToChannelAsync(ulong channelId, string text, IReadOnlyCollection<ulong> mentionUserIds)
        {
            try
            {
                if (Client == null) return;

                var channel = Client.GetChannel(channelId) as IMessageChannel;
                if (channel == null) return;

                AllowedMentions mentions = NoMentions;
                if (mentionUserIds != null && mentionUserIds.Count > 0)
                {
                    mentions = new AllowedMentions
                    {
                        UserIds = new List<ulong>(mentionUserIds)
                    };
                }

                foreach (var chunk in Chunk(text, MaxChunkLen))
                {
                    await SendSemaphore.WaitAsync();
                    try { await channel.SendMessageAsync(chunk, allowedMentions: mentions); }
                    finally { SendSemaphore.Release(); }
                }
            }
            catch (Exception e)
            {
                Printer.Error($"[Discord] SendToChannel error: {e}");
            }
        }


        // KMH: Discord account linking
        private static async Task HandleLinkCommand(SocketMessage raw, string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token) || !token.StartsWith("RWT-"))
                {
                    await SafeReply(raw.Channel, "❌ Invalid token format. Use: `!link RWT-XXXXXX`");
                    return;
                }

                string discordId = raw.Author.Id.ToString();
                string discordName = GetBestName(raw);

                // Use the in-memory UserManagerH cache rather than
                // re-reading every UserFile from disk and SerializeToFile-ing
                // them back. Two reasons:
                //   1. The old path was O(n²) disk reads (every-file scan for
                //      the token, then for every match a second every-file
                //      scan to detect Discord-ID dupes).
                //   2. More importantly, the old path could OVERWRITE live
                //      in-flight stat updates: PlayerStatsManager mutates the
                //      cached UserFile and calls SaveUserFile. The old !link
                //      code re-read from disk into a NEW UserFile object and
                //      stomped it back — silently dropping any stat increments
                //      that happened between the disk read and the write.
                //   3. SaveUserFile fires the OnUserFileSaved event so the
                //      cache stays consistent.
                TCPNetwork.Files.Client.UserFile[] all =
                    GameServer.Managers.UserManagerH.GetAllUserFiles();

                TCPNetwork.Files.Client.UserFile target = null;
                foreach (TCPNetwork.Files.Client.UserFile uf in all)
                {
                    if (uf == null) continue;
                    if (string.IsNullOrEmpty(uf.DiscordLinkToken)) continue;
                    if (uf.DiscordLinkToken != token) continue;
                    target = uf;
                    break;
                }

                if (target == null)
                {
                    await SafeReply(raw.Channel, "❌ Token not found. Generate one with `/link` in-game first.");
                    return;
                }

                // Token expiry — clear it via SaveUserFile so the cache stays
                // consistent and any in-flight stat updates on this UserFile
                // aren't dropped.
                if (System.DateTime.UtcNow.Ticks > target.DiscordLinkTokenExpiry)
                {
                    target.DiscordLinkToken = null;
                    target.DiscordLinkTokenExpiry = 0;
                    target.SaveUserFile();
                    await SafeReply(raw.Channel, "❌ Token expired. Generate a new one with `/link` in-game.");
                    return;
                }

                // Duplicate-Discord-ID check — O(n) single pass over cache.
                foreach (TCPNetwork.Files.Client.UserFile uf in all)
                {
                    if (uf == null || uf == target) continue;
                    if (string.Equals(uf.DiscordId, discordId, StringComparison.Ordinal)
                        && !string.Equals(uf.Username, target.Username, StringComparison.OrdinalIgnoreCase))
                    {
                        await SafeReply(raw.Channel,
                            $"❌ Your Discord is already linked to `{uf.Username}`. They must `/unlink` first.");
                        return;
                    }
                }

                // Link via SaveUserFile so the cache + OnUserFileSaved event
                // both fire correctly.
                target.DiscordId = discordId;
                target.DiscordUsername = discordName;
                target.DiscordLinkToken = null;
                target.DiscordLinkTokenExpiry = 0;
                target.SaveUserFile();

                await SafeReply(raw.Channel, $"✅ Linked! Discord `{discordName}` ↔ In-game `{target.Username}`");

                // Notify in-game if player is online.
                var onlineClient = GameServer.Hooks.TCPNetwork.ServerNetwork.GetConnectedClientFromUsername(target.Username);
                if (onlineClient != null)
                    GameServer.PacketManager.PM_Chat.SendConsoleMessage(onlineClient, $"Discord linked to {discordName}!");

                Printer.Warning($"[Discord] Linked {discordName} ({discordId}) to {target.Username}");
            }
            catch (System.Exception e)
            {
                Printer.Error($"[Discord] Link error: {e}");
                await SafeReply(raw.Channel, "❌ An error occurred during linking.");
            }
        }

        private static async Task HandleProfileCommand(SocketMessage raw, string targetUsername)
        {
            try
            {
                // Cache-only lookup; no admin-flag or Discord-ID leak.
                // targetUsername != null = public profile of any player.
                TCPNetwork.Files.Client.UserFile userFile = null;

                if (!string.IsNullOrWhiteSpace(targetUsername))
                {
                    // Public profile lookup by in-game username.
                    userFile = GameServer.Managers.UserManagerH.GetUserFileFromName(targetUsername.Trim());
                    if (userFile == null)
                    {
                        await SafeReply(raw.Channel, $"❌ No player called `{targetUsername.Trim()}` on this server.");
                        return;
                    }
                }
                else
                {
                    // Default: the caller's own profile via their linked Discord ID.
                    string discordId = raw.Author.Id.ToString();
                    foreach (TCPNetwork.Files.Client.UserFile uf in GameServer.Managers.UserManagerH.GetAllUserFiles())
                    {
                        if (uf != null && string.Equals(uf.DiscordId, discordId, StringComparison.Ordinal))
                        {
                            userFile = uf;
                            break;
                        }
                    }
                    if (userFile == null)
                    {
                        await SafeReply(raw.Channel,
                            "❌ No linked account found. Use `/link` in-game first, then `!link <token>` here.\n" +
                            "_Tip: you can look up another player without linking via_ `!profile <username>`.");
                        return;
                    }
                }

                bool online = GameServer.Hooks.TCPNetwork.ServerNetwork.GetConnectedClientFromUsername(userFile.Username) != null;
                string status = online ? "🟢 Online" : "⚫ Offline";
                string guild = string.IsNullOrEmpty(userFile.GuildName) ? "_None_" : userFile.GuildName;

                // Tenure
                string tenure = "—";
                if (userFile.FirstSeenUtcTicks > 0)
                {
                    System.TimeSpan span = System.DateTime.UtcNow - new System.DateTime(userFile.FirstSeenUtcTicks, System.DateTimeKind.Utc);
                    if (span.TotalDays >= 1) tenure = $"{(int)span.TotalDays}d";
                    else if (span.TotalHours >= 1) tenure = $"{(int)span.TotalHours}h";
                    else tenure = $"{System.Math.Max(1, (int)span.TotalMinutes)}m";
                }

                // Build the public-safe embed. NO IsAdmin, NO Discord ID,
                // NO token, NO IP — only what a player would expect to be
                // visible on a public profile card.
                var eb = new EmbedBuilder()
                    .WithTitle($"👤 {userFile.Username}")
                    .WithColor(new Color(112, 161, 255))
                    .WithDescription(string.IsNullOrEmpty(userFile.DiscordUsername)
                        ? null
                        : $"_Linked to_ **{userFile.DiscordUsername}**")
                    .AddField("Status", status, true)
                    .AddField("Guild", guild, true)
                    .AddField("Tenure", tenure, true)
                    .AddField("Silver donated", $"`{userFile.LifetimeSilverDonated:N0}s`", true)
                    .AddField("Sales earned", $"`{userFile.LifetimeSilverEarnedFromSales:N0}s`", true)
                    .AddField("Quests done", $"`{userFile.LifetimeQuestsCompleted}`", true)
                    .AddField("Sites built", $"`{userFile.LifetimeSitesBuilt}`", true)
                    .AddField("Worker XP", $"`{userFile.LifetimeWorkerXpEarned:N0}`", true);

                // If they have a showcase posted, link to it.
                ulong scCh = 0, scMsg = 0;
                ulong.TryParse(userFile.DiscordShowcaseChannelId ?? "0", out scCh);
                ulong.TryParse(userFile.DiscordShowcaseMessageId ?? "0", out scMsg);
                if (scCh != 0 && scMsg != 0)
                {
                    string link = BuildJumpUrl(scCh, scMsg);
                    if (!string.IsNullOrEmpty(link))
                        eb.AddField("🛒 Sell showcase", $"[Jump to showcase]({link})", false);
                }

                eb.WithFooter($"{ServerTag}");
                await SendEmbedReplyAsync(raw.Channel, eb.Build());
            }
            catch (Exception e)
            {
                Printer.Warning($"[Discord] HandleProfileCommand error: {e}");
            }
        }

        // Embed sibling of SafeReply.
        private static async Task SendEmbedReplyAsync(ISocketMessageChannel channel, Embed embed)
        {
            if (channel == null || embed == null) return;
            await SendSemaphore.WaitAsync();
            try { await channel.SendMessageAsync(embed: embed, allowedMentions: NoMentions); }
            finally { SendSemaphore.Release(); }
        }

        private static async Task SafeReply(ISocketMessageChannel channel, string text)
        {
            if (channel == null) return;

            foreach (var chunk in Chunk(text, MaxChunkLen))
            {
                await SendSemaphore.WaitAsync();
                try { await channel.SendMessageAsync(chunk, allowedMentions: NoMentions); }
                finally { SendSemaphore.Release(); }
            }
        }

        private static IEnumerable<string> Chunk(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) yield break;
            if (text.Length <= maxLen) { yield return text; yield break; }

            var sb = new StringBuilder(maxLen);
            foreach (var c in text)
            {
                if (sb.Length >= maxLen)
                {
                    yield return sb.ToString();
                    sb.Clear();
                }
                sb.Append(c);
            }
            if (sb.Length > 0) yield return sb.ToString();
        }

        private static string GetBestName(SocketMessage msg)
        {
            if (msg.Author is SocketGuildUser gu)
            {
                if (!string.IsNullOrWhiteSpace(gu.Nickname)) return gu.Nickname;
                if (!string.IsNullOrWhiteSpace(gu.DisplayName)) return gu.DisplayName;

                string global = TryGetGlobalName(msg.Author);
                if (!string.IsNullOrWhiteSpace(global)) return global;

                return gu.Username;
            }

            string global2 = TryGetGlobalName(msg.Author);
            if (!string.IsNullOrWhiteSpace(global2)) return global2;

            return msg.Author?.Username ?? "Discord";
        }

        private static string TryGetGlobalName(IUser user)
        {
            try
            {
                if (user == null) return string.Empty;
                PropertyInfo p = user.GetType().GetProperty("GlobalName", BindingFlags.Public | BindingFlags.Instance);
                if (p == null) return string.Empty;

                object v = p.GetValue(user, null);
                return v as string ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool IsAuthorizedAdmin(SocketMessage msg)
        {
            if (AdminRoleIds.Count == 0) return true;
            if (msg.Author is not SocketGuildUser gu) return false;

            foreach (var r in gu.Roles)
                if (AdminRoleIds.Contains(r.Id))
                    return true;

            return false;
        }

        private static HashSet<ulong> ParseRoleIds(string csv)
        {
            HashSet<ulong> set = new HashSet<ulong>();
            if (string.IsNullOrWhiteSpace(csv)) return set;

            foreach (var part in csv.Split(','))
            {
                if (ulong.TryParse(part.Trim(), out var id))
                    set.Add(id);
            }
            return set;
        }

        /// <summary>
        /// KMH: If <paramref name="content"/> starts with an @-mention of our
        /// bot (the literal `&lt;@id&gt;` or `&lt;@!id&gt;` Discord uses), strip
        /// it (and any trailing whitespace) and return true. Otherwise leave
        /// the content alone and return false.
        ///
        /// Discord's mention text format:
        ///   &lt;@USERID&gt;     — user mention
        ///   &lt;@!USERID&gt;    — nickname mention (older client format)
        /// </summary>
        private static bool TryStripLeadingBotMention(ref string content, ulong botId)
        {
            if (string.IsNullOrEmpty(content) || botId == 0) return false;

            string m1 = $"<@{botId}>";
            string m2 = $"<@!{botId}>";

            string trimmed = content.TrimStart();
            int matchLen = 0;
            if (trimmed.StartsWith(m1, StringComparison.Ordinal)) matchLen = m1.Length;
            else if (trimmed.StartsWith(m2, StringComparison.Ordinal)) matchLen = m2.Length;
            else return false;

            content = trimmed.Substring(matchLen).TrimStart();
            return true;
        }

        private static ulong ParseUlong(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return 0;
            return ulong.TryParse(input.Trim(), out var v) ? v : 0;
        }

        /// <summary>
        /// KMH: Sanitise the configured server-tag down to a short ASCII slug.
        /// Falls back to "S{port}" so multi-server clusters still get a sensible
        /// default if the operator forgot to set DiscordServerTag.
        /// </summary>
        private static string SanitiseServerTag(string raw, int port)
        {
            string r = (raw ?? string.Empty).Trim();
            // Letters/digits/dash/underscore only, max 16 chars.
            StringBuilder sb = new StringBuilder(r.Length);
            foreach (char c in r)
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_') sb.Append(c);
                if (sb.Length >= 16) break;
            }
            string clean = sb.ToString();
            if (string.IsNullOrEmpty(clean))
                clean = port > 0 ? $"S{port}" : "S?";
            return clean;
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("*", "\\*").Replace("_", "\\_").Replace("`", "\\`");
        }

        private static string SanitizeDiscordText(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;

            s = s.Replace("@everyone", "@\u200beveryone").Replace("@here", "@\u200bhere");
            s = s.Replace("<@", "<\u200b@").Replace("<#", "<\u200b#");
            return s;
        }

        private static string SanitizeGameTextFromDiscord(SocketMessage raw, string content)
        {
            try
            {
                var result = content;

                if (raw.MentionedUsers != null)
                {
                    foreach (var u in raw.MentionedUsers)
                    {
                        var n = u.Username ?? "user";
                        result = result.Replace($"<@{u.Id}>", "@" + n).Replace($"<@!{u.Id}>", "@" + n);
                    }
                }

                if (raw.MentionedRoles != null)
                {
                    foreach (var r in raw.MentionedRoles)
                    {
                        var n = r.Name ?? "role";
                        result = result.Replace($"<@&{r.Id}>", "@" + n);
                    }
                }

                return result;
            }
            catch
            {
                return content;
            }
        }

        private static SocketGuild GetPrimaryGuild()
        {
            if (Client == null) return null;

            if (ChatChannelId != 0 && Client.GetChannel(ChatChannelId) is SocketGuildChannel c1)
                return c1.Guild;

            if (AdminChannelId != 0 && Client.GetChannel(AdminChannelId) is SocketGuildChannel c2)
                return c2.Guild;

            return null;
        }

        private static async Task<MentionApplyResult> ApplyUserMentionsAsync(SocketGuild guild, string input)
        {
            if (guild == null) return new MentionApplyResult(input, Array.Empty<ulong>());
            if (string.IsNullOrEmpty(input)) return new MentionApplyResult(string.Empty, Array.Empty<ulong>());

            var matches = MentionTokenRegex.Matches(input);
            if (matches.Count == 0) return new MentionApplyResult(input, Array.Empty<ulong>());

            Dictionary<string, ulong> resolvedInMessage = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
            HashSet<ulong> allowedIds = new HashSet<ulong>();
            StringBuilder sb = new StringBuilder(input.Length);

            int last = 0;
            int mentionCount = 0;

            await MentionResolveSemaphore.WaitAsync();
            try
            {
                foreach (Match m in matches)
                {
                    if (!m.Success) continue;
                    if (m.Index < last) continue;

                    sb.Append(input, last, m.Index - last);

                    string rawName = m.Groups[1].Success ? m.Groups[1].Value
                                   : m.Groups[2].Success ? m.Groups[2].Value
                                   : m.Groups[3].Value;

                    string name = NormalizeMentionName(rawName);
                    if (string.IsNullOrWhiteSpace(name) || IsBlockedMentionName(name) || mentionCount >= MaxMentionsPerMessage)
                    {
                        sb.Append(m.Value);
                        last = m.Index + m.Length;
                        continue;
                    }

                    if (!resolvedInMessage.TryGetValue(name, out var id))
                    {
                        id = await ResolveUserIdAsync(guild, name);
                        resolvedInMessage[name] = id;
                    }

                    if (id != 0)
                    {
                        sb.Append("<@").Append(id).Append('>');
                        allowedIds.Add(id);
                        mentionCount++;
                    }
                    else
                    {
                        sb.Append(m.Value);
                    }

                    last = m.Index + m.Length;
                }
            }
            finally
            {
                MentionResolveSemaphore.Release();
            }

            if (last < input.Length)
                sb.Append(input, last, input.Length - last);

            return new MentionApplyResult(sb.ToString(), allowedIds.Count == 0 ? Array.Empty<ulong>() : allowedIds.ToArray());
        }

        private static bool IsBlockedMentionName(string name)
        {
            return name.Equals("everyone", StringComparison.OrdinalIgnoreCase)
                || name.Equals("here", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeMentionName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            name = name.Trim();

            if (name.Length > 32) name = name.Substring(0, 32);

            if (name.Contains('_'))
                name = name.Replace('_', ' ');

            return name.Trim();
        }

        private static async Task<ulong> ResolveUserIdAsync(SocketGuild guild, string name)
        {
            if (guild == null) return 0;

            var now = DateTime.UtcNow;
            if (MentionCache.TryGetValue(name, out var cached) && cached.ExpiresUtc > now)
                return cached.UserId;

            ulong id = 0;

            id = TryMatchUserInCache(guild, name);
            if (id == 0 && name.Contains(' '))
                id = TryMatchUserInCache(guild, name.Replace(" ", string.Empty));

            if (id == 0)
            {
                try
                {
                    var users = await guild.SearchUsersAsync(name, 25);
                    if (users != null)
                    {
                        IGuildUser best = null;
                        foreach (var u in users)
                        {
                            if (u == null) continue;

                            if (IsUserMatch(u, name))
                            {
                                best = u;
                                break;
                            }

                            best ??= u;
                        }
                        id = best?.Id ?? 0;
                    }
                }
                catch { }
            }

            var exp = now.AddMinutes(MentionCacheMinutes);
            MentionCache[name] = new MentionCacheEntry(id, exp);
            return id;
        }

        private static ulong TryMatchUserInCache(SocketGuild guild, string name)
        {
            try
            {
                foreach (var u in guild.Users)
                {
                    if (u == null) continue;
                    if (IsUserMatch(u, name)) return u.Id;
                }
            }
            catch { }
            return 0;
        }

        private static bool IsUserMatch(IGuildUser u, string name)
        {
            if (u == null) return false;
            if (string.IsNullOrWhiteSpace(name)) return false;

            if (u is SocketGuildUser su)
            {
                if (string.Equals(su.Username, name, StringComparison.OrdinalIgnoreCase)) return true;
                if (!string.IsNullOrWhiteSpace(su.Nickname) && string.Equals(su.Nickname, name, StringComparison.OrdinalIgnoreCase)) return true;
                if (!string.IsNullOrWhiteSpace(su.DisplayName) && string.Equals(su.DisplayName, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            else
            {
                if (string.Equals(u.Username, name, StringComparison.OrdinalIgnoreCase)) return true;
                if (!string.IsNullOrWhiteSpace(u.Nickname) && string.Equals(u.Nickname, name, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        private readonly struct OutboundMessage
        {
            public readonly ulong ChannelId;
            public readonly string Text;
            public readonly ulong[] UserMentionIds;

            public OutboundMessage(ulong channelId, string text, IReadOnlyCollection<ulong> mentionUserIds)
            {
                ChannelId = channelId;
                Text = text;

                if (mentionUserIds == null || mentionUserIds.Count == 0)
                {
                    UserMentionIds = null;
                }
                else
                {
                    UserMentionIds = new ulong[mentionUserIds.Count];
                    int i = 0;
                    foreach (var id in mentionUserIds) UserMentionIds[i++] = id;
                }
            }
        }

        private readonly struct MentionApplyResult
        {
            public readonly string Text;
            public readonly ulong[] UserIds;

            public MentionApplyResult(string text, ulong[] userIds)
            {
                Text = text;
                UserIds = userIds ?? Array.Empty<ulong>();
            }
        }

        private readonly struct MentionCacheEntry
        {
            public readonly ulong UserId;
            public readonly DateTime ExpiresUtc;

            public MentionCacheEntry(ulong userId, DateTime expiresUtc)
            {
                UserId = userId;
                ExpiresUtc = expiresUtc;
            }
        }
    }
}
