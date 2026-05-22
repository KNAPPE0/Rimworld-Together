using DiscordRPC;
using DiscordRPC.Logging;
using Shared;
using Shared.Misc;
using System;
using TCPNetwork;

namespace GameClient.Misc
{
    // Discord Rich Presence. Silently no-ops if Discord/SDK is unavailable.
    public static class DiscordHandler
    {
        // registration. Swap for a KMH-owned id to get branded presence.
        private const string PresenceID = "1505021874868981854";

        private static DiscordRpcClient _client;
        private static bool _initFailed;
        // Cached so "Playing for X min" doesn't reset on every refresh.
        private static Timestamps _sessionStartTimestamp;

        [OnSessionStart]
        private static void StartPresence()
        {
            try
            {
                _client = new DiscordRpcClient(PresenceID)
                {
                    Logger = new ConsoleLogger { Level = LogLevel.Warning }
                };
                _client.Initialize();

                _sessionStartTimestamp = Timestamps.Now;
                _client.SetPresence(BuildPresence(details: "On main menu", state: "KMH Edition"));
                Printer.Message("Discord Rich Presence started", Printer.LogImportanceMode.Verbose);
            }
            catch (Exception ex)
            {
                _initFailed = true;
                Printer.Message($"Discord Rich Presence unavailable: {ex.Message}", Printer.LogImportanceMode.Verbose);

                // ctor opens the IPC pipe; if Initialize() threw, dispose to avoid leaking it.
                if (_client != null)
                {
                    try { _client.Dispose(); } catch { }
                    _client = null;
                }
            }
        }

        [OnSessionEnd]
        private static void StopPresence()
        {
            if (_initFailed) return;
            if (_client == null) return;

            try
            {
                _client.Dispose();
                Printer.Message("Discord Rich Presence stopped", Printer.LogImportanceMode.Verbose);
            }
            catch (Exception ex)
            {
                Printer.Message($"Discord Rich Presence shutdown failed: {ex.Message}", Printer.LogImportanceMode.Verbose);
            }
            finally
            {
                _client = null;
                _sessionStartTimestamp = null;
            }
        }

        public static void SetState(string details, string state)
        {
            if (_initFailed || _client == null) return;
            try { _client.SetPresence(BuildPresence(details, state)); }
            catch { }
        }

        public static void RefreshFromSession()
        {
            if (_initFailed || _client == null) return;

            try
            {
                string details;
                string state;
                Party party = null;

                bool isDisconnected = SessionHandler.CurrentNetworkState
                    == GameClient.Hooks.TCPNetwork.ClientNetwork.ClientNetworkState.Disconnected;

                if (isDisconnected)
                {
                    details = "On main menu";
                    state = "KMH Edition";
                }
                else
                {
                    string user = string.IsNullOrEmpty(SessionHandler.Username) ? "Connecting" : SessionHandler.Username;
                    details = SessionHandler.IsReadyToPlay ? $"Playing as {user}" : "Joining server";

                    string endpoint = string.IsNullOrEmpty(Network.Ip) ? "Multiplayer" : $"{Network.Ip}:{Network.Port}";
                    // Discord's state field caps at 128 chars; IPv6 can blow that.
                    if (endpoint.Length > 60) endpoint = endpoint.Substring(0, 57) + "...";
                    state = endpoint;

                    int cur = SessionHandler.CurrentServerPlayers;
                    if (cur >= 0 && cur != int.MinValue)
                    {
                        party = new Party
                        {
                            ID = $"kmh-{Network.Ip}-{Network.Port}",
                            Size = Math.Max(1, cur),
                            Max = Math.Max(cur, 2)
                        };
                    }
                }

                RichPresence presence = BuildPresence(details, state);
                if (party != null) presence.Party = party;
                _client.SetPresence(presence);
            }
            catch (Exception ex)
            {
                Printer.Message($"Discord RP refresh failed: {ex.Message}", Printer.LogImportanceMode.Verbose);
            }
        }

        private static RichPresence BuildPresence(string details, string state)
        {
            return new RichPresence
            {
                Details = details ?? "Playing RimWorld Together",
                State = state ?? "KMH Edition",
                Timestamps = _sessionStartTimestamp ?? Timestamps.Now,
                Buttons = new[]
                {
                    new Button { Label = "Get the mod", Url = KMHProject.SteamWorkshopUrl }
                }
            };
        }
    }
}
