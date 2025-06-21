using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GameServer.Misc;

namespace GameServer.Misc
{
    public class ConsoleTapBuffer
    {
        public Func<string, Task>? SendToDiscord;

        const int TapBatchMax = 15;
        const int TapDelayMs  = 4000;

        readonly List<string> _buf = new();
        readonly object       _lock = new();
        CancellationTokenSource? _cts;

        public ConsoleTapBuffer() => Printer.ConsoleTap += OnConsoleTap;

        void OnConsoleTap(Printer.LogKind kind, string txt)
        {
            // Skip any output while a console command is actively sending its own block
            if (!string.IsNullOrEmpty(Printer.DiscordConsoleUser)) return;
            if (SendToDiscord == null) return;

            string icon = kind switch
            {
                Printer.LogKind.Error   => ":x:",
                Printer.LogKind.Warning => ":warning:",
                _                       => ":information_source:"
            };

            lock (_lock)
            {
                _buf.Add($"{icon} {txt}");

                if (_buf.Count >= TapBatchMax)
                {
                    CancelDebounce();
                    _ = FlushAsync();
                    return;
                }

                CancelDebounce();
                _cts = new CancellationTokenSource();
                _ = DebounceFlushAsync(_cts.Token);
            }
        }

        async Task DebounceFlushAsync(CancellationToken tok)
        {
            try { await Task.Delay(TapDelayMs, tok); }
            catch (TaskCanceledException) { return; }
            await FlushAsync();
        }

        async Task FlushAsync()
        {
            string payload;
            lock (_lock)
            {
                if (_buf.Count == 0) return;
                payload = string.Join('\n', _buf);
                _buf.Clear();
            }
            await SendToDiscord!(payload);
        }

        void CancelDebounce()
        {
            try { _cts?.Cancel(); } catch { }
            _cts = null;
        }
    }
}