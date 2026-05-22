using Shared;
using Shared.Files.Configs.Mods;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.IO;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static Shared.Misc.Printer;

namespace GameServer.Managers
{
    /// <summary>
    /// Server-side options-profile state holder + outbound send + dedupe gate.
    ///
    /// Admin upload flow lives in <c>OptionsProfile/OptionsProfileManager.Upload.cs</c>.
    /// Profile chunk caching lives in <c>OptionsProfile/OptionsProfileManager.ChunkCache.cs</c>.
    /// </summary>
    public static partial class OptionsProfileManager
    {
        private const int ChunkSizeBytes = 256 * 1024;
        private const int SendDedupeWindowMs = 5000;

        private static readonly object SendGateLock = new object();
        private static readonly Dictionary<string, long> RecentlySent =
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        private static long LastPruneTicks;

        public static OptionsProfile CurrentProfile { get; private set; }

        public static void Initialize()
        {
            try
            {
                if (string.IsNullOrEmpty(OptionsProfile.SavePath))
                    OptionsProfile.SavePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ServerOptionsProfile.dat");

                CurrentProfile = (OptionsProfile)OptionsProfile.Load<OptionsProfile>();
                if (CurrentProfile == null)
                    CurrentProfile = new OptionsProfile();

                Printer.Warning($"[OptionsProfile] Loaded. HasProfile={CurrentProfile.HasProfile}", LogImportanceMode.Verbose);
            }
            catch (Exception e)
            {
                Printer.Warning($"[OptionsProfile] Failed to initialize: {e}");
                CurrentProfile = new OptionsProfile();
            }
        }

        public static void TryPushProfile(ServerClient client)
        {
            try
            {
                if (client == null) return;

                PKT_ModConfig statePacket = new PKT_ModConfig
                {
                    _stepMode = PKT_ModConfig.ModConfigStepMode.Send,
                    _configFile = GameServer.Core.Master.ModConfig
                };

                client.Listener.EnqueuePacket(PacketHeader.ModManager, statePacket);

                if (CurrentProfile == null || !CurrentProfile.HasProfile || string.IsNullOrEmpty(CurrentProfile.ProfileHash))
                    return;

                HandleClientRequest(client);
            }
            catch (Exception e)
            {
                Printer.Warning($"[OptionsProfile] TryPushProfile failed: {e}");
            }
        }

        public static void HandleClientRequest(ServerClient client)
        {
            SendProfileToClient(client, forceSend: false);
        }

        private static void SendProfileToClient(ServerClient client, bool forceSend)
        {
            if (client == null) return;

            string username = client.UserFile?.Username;
            if (string.IsNullOrWhiteSpace(username)) return;

            if (CurrentProfile == null || !CurrentProfile.HasProfile || string.IsNullOrEmpty(CurrentProfile.ProfileHash))
            {
                PKT_ModConfig none = new PKT_ModConfig
                {
                    _isOptionsProfileChunk = true,
                    _noOptionsProfileAvailable = true
                };

                client.Listener.EnqueuePacket(PacketHeader.ModManager, none);
                return;
            }

            if (!forceSend && WasRecentlySent(client, CurrentProfile.ProfileHash))
                return;

            List<byte[]> chunks = GetCachedChunks(CurrentProfile.ZipBytes, CurrentProfile.ProfileHash);

            Printer.Warning($"[OptionsProfile] Sending profile to {username}. Chunks={chunks.Count} Hash={CurrentProfile.ProfileHash}");

            for (int i = 0; i < chunks.Count; i++)
            {
                PKT_ModConfig packet = new PKT_ModConfig
                {
                    _isOptionsProfileChunk = true,
                    _optionsProfileHash = CurrentProfile.ProfileHash,
                    _optionsProfileUpdatedUtcTicks = CurrentProfile.UpdatedUtcTicks,
                    _chunkIndex = i,
                    _chunkCount = chunks.Count,
                    _chunkBytes = chunks[i],
                    _forceSendOptionsProfile = forceSend
                };

                client.Listener.EnqueuePacket(PacketHeader.ModManager, packet);
            }
        }

        public static bool IsClientMissingRequiredProfile(PKT_Login loginData)
        {
            try
            {
                if (GameServer.Core.Master.ModConfig == null || !GameServer.Core.Master.ModConfig.IsEnforced)
                    return false;

                if (CurrentProfile == null || !CurrentProfile.HasProfile || string.IsNullOrWhiteSpace(CurrentProfile.ProfileHash))
                    return false;

                if (loginData == null)
                    return true;

                if (!loginData._hasActiveOptionsProfile)
                    return true;

                return !string.Equals(
                    loginData._activeOptionsProfileHash ?? string.Empty,
                    CurrentProfile.ProfileHash ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return true;
            }
        }

        public static void SendRequiredProfileForJoin(ServerClient client)
        {
            if (client == null) return;

            try
            {
                PKT_ModConfig gatePacket = new PKT_ModConfig
                {
                    _stepMode = PKT_ModConfig.ModConfigStepMode.Send,
                    _configFile = GameServer.Core.Master.ModConfig,
                    _optionsProfileRequiredForJoin = true,
                    _optionsProfileHash = CurrentProfile?.ProfileHash ?? string.Empty,
                    _optionsProfileUpdatedUtcTicks = CurrentProfile?.UpdatedUtcTicks ?? 0,
                    _forceSendOptionsProfile = true
                };

                client.Listener.EnqueuePacket(PacketHeader.ModManager, gatePacket);

                SendProfileToClient(client, forceSend: true);
            }
            catch (Exception e)
            {
                Printer.Warning($"[OptionsProfile] SendRequiredProfileForJoin failed: {e}");
            }
        }

        // Per-client send dedupe — prevents the server hammering a single
        // client with repeated full-profile pushes inside a short window.
        private static bool WasRecentlySent(ServerClient client, string hash)
        {
            try
            {
                string id = client?.UserFile?.Username;
                if (string.IsNullOrWhiteSpace(id))
                    id = client?.CurrentIP;

                if (string.IsNullOrWhiteSpace(id))
                    return false;

                string key = id + "|" + hash;
                long nowTicks = DateTime.UtcNow.Ticks;

                lock (SendGateLock)
                {
                    PruneIfNeeded(nowTicks);

                    if (RecentlySent.TryGetValue(key, out long lastTicks))
                    {
                        double ms = (nowTicks - lastTicks) / (double)TimeSpan.TicksPerMillisecond;
                        if (ms <= SendDedupeWindowMs)
                            return true;
                    }

                    RecentlySent[key] = nowTicks;
                }
            }
            catch { }

            return false;
        }

        // Caller must hold SendGateLock.
        private static void PruneIfNeeded(long nowTicks)
        {
            try
            {
                if (LastPruneTicks != 0)
                {
                    double ms = (nowTicks - LastPruneTicks) / (double)TimeSpan.TicksPerMillisecond;
                    if (ms < 10000) return;
                }

                LastPruneTicks = nowTicks;

                long cutoff = nowTicks - TimeSpan.FromMinutes(1).Ticks;
                List<string> remove = null;

                foreach (var kv in RecentlySent)
                {
                    if (kv.Value < cutoff)
                    {
                        remove ??= new List<string>();
                        remove.Add(kv.Key);
                    }
                }

                if (remove != null)
                {
                    for (int i = 0; i < remove.Count; i++)
                        RecentlySent.Remove(remove[i]);
                }
            }
            catch { }
        }
    }
}
