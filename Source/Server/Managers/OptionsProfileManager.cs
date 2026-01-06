using Shared;
using Shared.Files.Configs.Mods;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.IO;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static Shared.CommonEnumerators;

namespace GameServer.Managers
{
    public static class OptionsProfileManager
    {
        private const int ChunkSizeBytes = 256 * 1024;

        // Upload buffers are keyed by "username|hash" so concurrent admin uploads won't collide.
        private static readonly Dictionary<string, IncomingUploadBuffer> UploadBuffers =
            new Dictionary<string, IncomingUploadBuffer>(StringComparer.OrdinalIgnoreCase);

        // Prevent duplicate sends per connection/profile hash.
        // Keyed by stable client identity (username if available, otherwise IP) + hash.
        private static readonly object SendGateLock = new object();
        private static readonly Dictionary<string, long> RecentlySent =
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        // Small TTL to prevent spam (login pipeline can trigger multiple ModManager calls).
        private const int SendDedupeWindowMs = 5000;
        private static long LastPruneTicks = 0;

        private class IncomingUploadBuffer
        {
            public string Key;
            public string Hash;
            public long UpdatedTicks;
            public int ChunkCount;
            public byte[][] Chunks;
            public int ReceivedCount;
        }

        public static OptionsProfile CurrentProfile { get; private set; }

        public static void Initialize()
        {
            try
            {
                if (string.IsNullOrEmpty(OptionsProfile.SavePath))
                {
                    OptionsProfile.SavePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ServerOptionsProfile.dat");
                }

                CurrentProfile = (OptionsProfile)OptionsProfile.Load<OptionsProfile>();
                Printer.Warning($"[OptionsProfile] Loaded. HasProfile={CurrentProfile.HasProfile}", LogImportanceMode.Verbose);
            }
            catch (Exception e)
            {
                Printer.Warning($"[OptionsProfile] Failed to initialize: {e}");
                CurrentProfile = new OptionsProfile();
            }
        }

        /// <summary>
        /// Safe helper to push the current profile to a client if one exists.
        /// This will NOT send pre-login (no username yet), and will dedupe repeated triggers.
        /// </summary>
        public static void TryPushProfile(ServerClient client)
        {
            try
            {
                if (client == null) return;
                if (CurrentProfile == null) return;
                if (!CurrentProfile.HasProfile) return;
                if (string.IsNullOrEmpty(CurrentProfile.ProfileHash)) return;

                HandleClientRequest(client);
            }
            catch { }
        }

        /// <summary>
        /// Called when a client requests the profile (or when the server wants to push it).
        /// </summary>
        public static void HandleClientRequest(ServerClient client)
        {
            if (client == null) return;

            // Don't send during pre-login. This is what causes:
            // "Sending profile to ." because Username isn't set yet.
            // The login pipeline will call again once LoginManager sets UserFile.Username.
            string username = client.UserFile?.Username;
            if (string.IsNullOrWhiteSpace(username))
            {
                // If you ever want pre-login sends, change this to use IP as identity and SEND,
                // but keep the log label non-empty. For now: skip for correctness + dedupe.
                return;
            }

            if (CurrentProfile == null || !CurrentProfile.HasProfile || string.IsNullOrEmpty(CurrentProfile.ProfileHash))
            {
                Printer.Warning($"[OptionsProfile] {SafeClientLabel(client)} requested profile, but none is available.", LogImportanceMode.Verbose);

                ModConfigData none = new ModConfigData
                {
                    _isOptionsProfileChunk = true,
                    _noOptionsProfileAvailable = true
                };

                client.Listener.EnqueuePacket(PacketHeader.ModManager, none);
                return;
            }

            if (WasRecentlySent(client, CurrentProfile.ProfileHash))
                return;

            List<byte[]> chunks = ConfigProfileUtility.SplitIntoChunks(CurrentProfile.ZipBytes, ChunkSizeBytes);

            Printer.Warning(
                $"[OptionsProfile] Sending profile to {username}. Chunks={chunks.Count} Hash={CurrentProfile.ProfileHash}",
                LogImportanceMode.Verbose);

            for (int i = 0; i < chunks.Count; i++)
            {
                ModConfigData packet = new ModConfigData
                {
                    _isOptionsProfileChunk = true,
                    _optionsProfileHash = CurrentProfile.ProfileHash,
                    _optionsProfileUpdatedUtcTicks = CurrentProfile.UpdatedUtcTicks,
                    _chunkIndex = i,
                    _chunkCount = chunks.Count,
                    _chunkBytes = chunks[i]
                };

                client.Listener.EnqueuePacket(PacketHeader.ModManager, packet);
            }
        }

        public static void HandleAdminUploadChunk(ServerClient client, ModConfigData data)
        {
            if (client == null || data == null) return;

            string username = client.UserFile?.Username ?? "UNKNOWN";
            string key = $"{username}|{data._optionsProfileHash}";

            if (string.IsNullOrEmpty(data._optionsProfileHash) || data._chunkCount <= 0) return;
            if (data._chunkIndex < 0 || data._chunkIndex >= data._chunkCount) return;

            if (!UploadBuffers.TryGetValue(key, out IncomingUploadBuffer buffer))
            {
                buffer = new IncomingUploadBuffer
                {
                    Key = key,
                    Hash = data._optionsProfileHash,
                    UpdatedTicks = data._optionsProfileUpdatedUtcTicks,
                    ChunkCount = data._chunkCount,
                    Chunks = new byte[data._chunkCount][],
                    ReceivedCount = 0
                };

                UploadBuffers[key] = buffer;

                Printer.Warning($"[OptionsProfile] Upload started by {username}. Chunks={buffer.ChunkCount} Hash={buffer.Hash}", LogImportanceMode.Verbose);
            }

            if (buffer.Chunks[data._chunkIndex] == null)
            {
                buffer.Chunks[data._chunkIndex] = data._chunkBytes ?? Array.Empty<byte>();
                buffer.ReceivedCount++;
            }

            if (buffer.ReceivedCount >= buffer.ChunkCount)
            {
                byte[] full = Combine(buffer.Chunks);
                UploadBuffers.Remove(key);

                string computed = ConfigProfileUtility.Sha256Hex(full);
                if (!string.Equals(computed, buffer.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    Printer.Warning($"[OptionsProfile] Upload failed (hash mismatch) by {username}. Expected={buffer.Hash} Got={computed}");
                    return;
                }

                CurrentProfile = new OptionsProfile
                {
                    ProfileHash = buffer.Hash,
                    UpdatedUtcTicks = buffer.UpdatedTicks > 0 ? buffer.UpdatedTicks : DateTime.UtcNow.Ticks,
                    ZipBytes = full
                };

                try
                {
                    CurrentProfile.Save();
                    Printer.Warning($"[OptionsProfile] Updated by {username}. Hash={CurrentProfile.ProfileHash} Size={CurrentProfile.ZipBytes.Length} bytes");
                }
                catch (Exception e)
                {
                    Printer.Warning($"[OptionsProfile] Failed to save: {e}");
                }

                try
                {
                    lock (SendGateLock)
                    {
                        RecentlySent.Clear();
                        LastPruneTicks = DateTime.UtcNow.Ticks;
                    }
                }
                catch { }
            }
        }

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
                    PruneIfNeeded_NoThrow(nowTicks);

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

        private static void PruneIfNeeded_NoThrow(long nowTicks)
        {
            try
            {
                if (LastPruneTicks != 0)
                {
                    double since = (nowTicks - LastPruneTicks) / (double)TimeSpan.TicksPerMillisecond;
                    if (since < 10000)
                        return;
                }

                LastPruneTicks = nowTicks;

                long cutoff = nowTicks - (TimeSpan.TicksPerSecond * 60);

                if (RecentlySent.Count == 0)
                    return;

                List<string> toRemove = null;

                foreach (var kv in RecentlySent)
                {
                    if (kv.Value < cutoff)
                    {
                        toRemove ??= new List<string>(8);
                        toRemove.Add(kv.Key);
                    }
                }

                if (toRemove != null)
                {
                    for (int i = 0; i < toRemove.Count; i++)
                        RecentlySent.Remove(toRemove[i]);
                }
            }
            catch { }
        }

        private static string SafeClientLabel(ServerClient client)
        {
            try
            {
                string u = client?.UserFile?.Username;
                if (!string.IsNullOrWhiteSpace(u)) return u;

                string ip = client?.CurrentIP;
                if (!string.IsNullOrWhiteSpace(ip)) return ip;

                return "(unknown)";
            }
            catch
            {
                return "(unknown)";
            }
        }

        private static byte[] Combine(byte[][] parts)
        {
            int total = 0;
            for (int i = 0; i < parts.Length; i++) total += parts[i]?.Length ?? 0;

            byte[] all = new byte[total];
            int offset = 0;

            for (int i = 0; i < parts.Length; i++)
            {
                byte[] p = parts[i] ?? Array.Empty<byte>();
                Buffer.BlockCopy(p, 0, all, offset, p.Length);
                offset += p.Length;
            }

            return all;
        }
    }
}
