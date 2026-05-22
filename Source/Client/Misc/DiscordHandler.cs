using DiscordRPC;
using DiscordRPC.Logging;
using Shared;
using Shared.Misc;
using System;
using TCPNetwork;

namespace GameClient.Misc
{
    /// <summary>
    /// KMH 26.5.22.1: Ported from upstream RWT (May 2026) with KMH hardening.
    ///
    /// <para>Discord Rich Presence integration — populates the player's
    /// Discord profile with a "Playing RimWorld Together" status, elapsed
    /// session timer, and a button linking to the Steam Workshop page so
    /// friends can install KMH from the player's profile in one click.</para>
    ///
    /// <para><b>How KMH differs from upstream:</b></para>
    /// <list type="bullet">
    ///   <item><b>Hard-wrapped in try/catch.</b> Upstream's <c>Initialize</c>
    ///   throws if the Discord client isn't installed, which kills the
    ///   <see cref="OnSessionStart"/> hook chain and breaks login. We
    ///   silently swallow failures here — Rich Presence is a UX nicety,
    ///   not a gameplay feature, and should never block the game.</item>
    ///   <item><b>Null-safe stop.</b> Upstream calls <c>RPClient.Dispose()</c>
    ///   without checking — if <see cref="StartPresence"/> failed,
    ///   <c>RPClient</c> is null and shutdown NREs. We null-guard.</item>
    ///   <item><b>Session-aware presence.</b> Upstream sets a static
    ///   "Playing Multiplayer" string and leaves it. KMH's
    ///   <see cref="RefreshFromSession"/> updates the presence live as
    ///   the player connects/disconnects, showing the server's IP+port
    ///   and the player's username — so friends scanning your Discord
    ///   profile know exactly where to join.</item>
    ///   <item><b>Optional player counts.</b> If
    ///   <see cref="SessionHandler.CurrentServerPlayers"/> is
    ///   set, we emit a Discord-native <c>Party</c> with current/max,
    ///   which shows in-profile as "3 of 32" — the same UX as native
    ///   Steam servers.</item>
    ///   <item><b>Steam Workshop button</b> retained — same URL upstream
    ///   ships, since KMH is published as a Workshop fork.</item>
    /// </list>
    /// </summary>
    public static class DiscordHandler
    {
        // KMH 26.5.22.1: Upstream's RPC application ID. Keep using it so
        // existing Discord Rich Presence works for users coming from
        // upstream RWT without re-authorising — the application ID is
        // public and tied to the presence asset images on Discord's side.
        private const string PresenceID = "1505021874868981854";

        private const string WorkshopUrl = "https://steamcommunity.com/sharedfiles/filedetails/?id=3005289691";

        private static DiscordRpcClient _client;
        private static bool _initFailed;

        // KMH 26.5.22.1: Cache the connect timestamp so the elapsed
        // "Playing for X minutes" counter survives presence refreshes
        // (otherwise every RefreshFromSession would reset the counter
        // to 0 and the player's friends would see "Playing for 0 min"
        // flicker continuously).
        private static Timestamps _sessionStartTimestamp;

        [OnSessionStart]
        private static void StartPresence()
        {
            // KMH 26.5.22.1: Guard the whole startup — if the user hasn't
            // installed Discord, or the Discord client is sandboxed, the
            // SDK throws inside Initialize. Failing silently here is
            // critical because [OnSessionStart] runs synchronously during
            // mod load — an exception here would cascade into every
            // downstream OnSessionStart handler.
            try
            {
                _client = new DiscordRpcClient(PresenceID)
                {
                    Logger = new ConsoleLogger { Level = LogLevel.Warning }
                };
                _client.Initialize();

                _sessionStartTimestamp = Timestamps.Now;
                _client.SetPresence(BuildPresence(
                    details: "On main menu",
                    state: "KMH Edition"));

                Printer.Message("Discord Rich Presence started", Printer.LogImportanceMode.Verbose);
            }
            catch (Exception ex)
            {
                _initFailed = true;
                // KMH 26.5.22.1: Verbose-only — the user without Discord
                // shouldn't see a console warning every session boot.
                Printer.Message($"Discord Rich Presence unavailable: {ex.Message}", Printer.LogImportanceMode.Verbose);

                // KMH 26.5.22.1: If `new DiscordRpcClient(...)` succeeded
                // but `Initialize()` threw, _client is non-null and holds
                // an open IPC pipe handle until process exit. StopPresence's
                // early-return on _initFailed would skip the disposal,
                // leaking the handle. Dispose here so it's released the
                // moment we know initialisation failed. Suppress any
                // dispose-time exception — we're already in a failure path.
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

        /// <summary>
        /// KMH 26.5.22.1: Update the presence details/state without
        /// re-initialising. Call this when the player connects to a
        /// server so friends see "Playing on &lt;ServerName&gt;" instead
        /// of the static main-menu text. Safe to call any time — silently
        /// no-ops if the SDK is unavailable.
        /// </summary>
        public static void SetState(string details, string state)
        {
            if (_initFailed) return;
            if (_client == null) return;

            try { _client.SetPresence(BuildPresence(details, state)); }
            catch { /* presence is best-effort */ }
        }

        /// <summary>
        /// KMH 26.5.22.1: Refresh presence from current session state.
        /// Called from <see cref="SessionHandler"/> on every
        /// network state change. Three states:
        /// <list type="bullet">
        /// <item><b>Disconnected:</b> "On main menu" / "KMH Edition"</item>
        /// <item><b>Connected (not yet ready):</b> "Connecting…" / server endpoint</item>
        /// <item><b>Connected + ready:</b> "Playing as &lt;username&gt;" / "&lt;ip:port&gt;"
        /// — plus Discord party indicator with the live player count if
        /// the server has reported one.</item>
        /// </list>
        /// All exceptions are swallowed — presence is purely cosmetic.
        /// </summary>
        public static void RefreshFromSession()
        {
            if (_initFailed) return;
            if (_client == null) return;

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
                    string user = string.IsNullOrEmpty(SessionHandler.Username)
                        ? "Connecting…"
                        : SessionHandler.Username;

                    details = SessionHandler.IsReadyToPlay
                        ? $"Playing as {user}"
                        : "Joining server…";

                    // KMH 26.5.22.1: Endpoint formatting — fall back to a
                    // placeholder if Network.Ip/Port aren't set yet (the
                    // very first connect frame can race the presence
                    // update). Truncate long IPs (IPv6) so they don't
                    // overflow Discord's 128-char state field.
                    string endpoint = string.IsNullOrEmpty(Network.Ip)
                        ? "Multiplayer"
                        : $"{Network.Ip}:{Network.Port}";
                    if (endpoint.Length > 60) endpoint = endpoint.Substring(0, 57) + "…";
                    state = endpoint;

                    // KMH 26.5.22.1: Show "X of Y" in Discord's native
                    // party widget if the server reported a population.
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

        /// <summary>
        /// KMH 26.5.22.1: Single helper that constructs a presence with
        /// our standard button + timestamp. Centralised so the multiple
        /// callers (StartPresence / SetState / RefreshFromSession) can't
        /// drift apart on the chrome (button label, timestamp policy).
        /// </summary>
        private static RichPresence BuildPresence(string details, string state)
        {
            return new RichPresence
            {
                Details = details ?? "Playing RimWorld Together",
                State = state ?? "KMH Edition",
                Timestamps = _sessionStartTimestamp ?? Timestamps.Now,
                Buttons = new[]
                {
                    new Button { Label = "Get the mod", Url = WorkshopUrl }
                }
            };
        }
    }
}
