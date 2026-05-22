using Shared;
using Shared.Files.Configs.Mods;
using Shared.Misc;
using System;
using System.Collections.Generic;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static Shared.Misc.Printer;

namespace GameServer.Managers
{
    /// <summary>
    /// Admin-only profile upload receiver. Buffers chunks under
    /// per-(user,hash) keys, validates the assembled SHA, then promotes
    /// the assembled bytes into <c>CurrentProfile</c> and broadcasts.
    ///
    /// Hardened against:
    ///   * runaway chunkCount / chunkBytes — capped before allocation
    ///   * abandoned uploads — buffers expire after MaxIncompleteUploadAgeMs
    ///   * concurrent packet threads — UploadBuffersLock guards the dictionary
    /// </summary>
    public static partial class OptionsProfileManager
    {
        // Hard caps on what an admin upload may declare. Anything beyond this
        // is refused before allocating buffers, to prevent OOM / DoS via crafted packets.
        private const int MaxUploadChunks = 4096;                      // 4096 * 256KB = 1 GB max profile
        private const int MaxUploadChunkBytes = ChunkSizeBytes * 2;    // tolerate a little slack
        private const int MaxIncompleteUploadAgeMs = 60_000;           // expire stale upload buffers after 1 min

        private static readonly object UploadBuffersLock = new object();
        private static readonly Dictionary<string, IncomingUploadBuffer> UploadBuffers =
            new Dictionary<string, IncomingUploadBuffer>(StringComparer.OrdinalIgnoreCase);

        private static long LastUploadPruneTicks;

        private class IncomingUploadBuffer
        {
            public string Key;
            public string Hash;
            public long UpdatedTicks;
            public long StartedTicks;
            public int ChunkCount;
            public byte[][] Chunks;
            public int ReceivedCount;
        }

        public static void HandleAdminUploadChunk(ServerClient client, PKT_ModConfig data)
        {
            if (client == null || data == null) return;

            if (client.UserFile == null || !client.UserFile.IsAdmin)
            {
                Printer.Warning($"[OptionsProfile] Non-admin '{client.UserFile?.Username}' tried to upload a profile.");
                return;
            }

            string username = client.UserFile.Username ?? "UNKNOWN";
            string hash = data._optionsProfileHash ?? string.Empty;

            if (string.IsNullOrWhiteSpace(hash)) return;
            if (data._chunkCount <= 0) return;
            if (data._chunkCount > MaxUploadChunks)
            {
                Printer.Warning($"[OptionsProfile] Refusing upload from '{username}': chunkCount={data._chunkCount} exceeds cap {MaxUploadChunks}.");
                return;
            }
            if (data._chunkIndex < 0 || data._chunkIndex >= data._chunkCount) return;
            if (data._chunkBytes != null && data._chunkBytes.Length > MaxUploadChunkBytes)
            {
                Printer.Warning($"[OptionsProfile] Refusing upload from '{username}': chunk size={data._chunkBytes.Length} exceeds cap {MaxUploadChunkBytes}.");
                return;
            }

            string key = $"{username}|{hash}";
            byte[] full = null;
            IncomingUploadBuffer completed = null;
            long nowTicks = DateTime.UtcNow.Ticks;

            lock (UploadBuffersLock)
            {
                PruneStaleUploadsLocked(nowTicks);

                if (!UploadBuffers.TryGetValue(key, out IncomingUploadBuffer buffer))
                {
                    buffer = new IncomingUploadBuffer
                    {
                        Key = key,
                        Hash = hash,
                        UpdatedTicks = data._optionsProfileUpdatedUtcTicks,
                        StartedTicks = nowTicks,
                        ChunkCount = data._chunkCount,
                        Chunks = new byte[data._chunkCount][],
                        ReceivedCount = 0
                    };

                    UploadBuffers[key] = buffer;
                    Printer.Warning($"[OptionsProfile] Upload started by {username}. Chunks={buffer.ChunkCount} Hash={buffer.Hash}", LogImportanceMode.Verbose);
                }

                if (buffer.ChunkCount != data._chunkCount)
                {
                    Printer.Warning($"[OptionsProfile] Upload chunk count mismatch from '{username}'. Resetting buffer.");
                    UploadBuffers.Remove(key);
                    return;
                }

                if (buffer.Chunks[data._chunkIndex] == null)
                {
                    buffer.Chunks[data._chunkIndex] = data._chunkBytes ?? Array.Empty<byte>();
                    buffer.ReceivedCount++;
                }

                if (buffer.ReceivedCount < buffer.ChunkCount)
                    return;

                full = Combine(buffer.Chunks);
                completed = buffer;
                UploadBuffers.Remove(key);
            }

            string computed = ConfigProfileUtility.Sha256Hex(full);
            if (!string.Equals(computed, completed.Hash, StringComparison.OrdinalIgnoreCase))
            {
                Printer.Warning($"[OptionsProfile] Upload hash mismatch by {username}. Expected={completed.Hash} Got={computed}");
                return;
            }

            CurrentProfile = new OptionsProfile
            {
                ProfileHash = completed.Hash,
                UpdatedUtcTicks = completed.UpdatedTicks > 0 ? completed.UpdatedTicks : DateTime.UtcNow.Ticks,
                ZipBytes = full
            };

            InvalidateChunkCache();

            try
            {
                CurrentProfile.Save();
                Printer.Warning($"[OptionsProfile] Profile updated by {username}. Hash={CurrentProfile.ProfileHash} Size={CurrentProfile.ZipBytes.Length}");

                if (GameServer.Core.Master.ModConfig != null)
                {
                    GameServer.Core.Master.ModConfig.IsEnforced = true;
                    ModConfigFile.Save(ModConfigFile.SavePath, GameServer.Core.Master.ModConfig);
                    Printer.Title("[OptionsProfile] Enforcement ENABLED.");
                }

                lock (SendGateLock)
                {
                    RecentlySent.Clear();
                    LastPruneTicks = DateTime.UtcNow.Ticks;
                }

                foreach (ServerClient sc in GameServer.Hooks.TCPNetwork.ServerNetwork.GetConnectedClients())
                {
                    try { TryPushProfile(sc); }
                    catch { }
                }
            }
            catch (Exception e)
            {
                Printer.Warning($"[OptionsProfile] Failed to save profile: {e}");
            }
        }

        // Caller must hold UploadBuffersLock.
        private static void PruneStaleUploadsLocked(long nowTicks)
        {
            if (LastUploadPruneTicks != 0)
            {
                double ms = (nowTicks - LastUploadPruneTicks) / (double)TimeSpan.TicksPerMillisecond;
                if (ms < 10_000) return;
            }

            LastUploadPruneTicks = nowTicks;

            long maxAgeTicks = TimeSpan.FromMilliseconds(MaxIncompleteUploadAgeMs).Ticks;
            List<string> remove = null;

            foreach (var kv in UploadBuffers)
            {
                if (nowTicks - kv.Value.StartedTicks > maxAgeTicks)
                {
                    remove ??= new List<string>();
                    remove.Add(kv.Key);
                }
            }

            if (remove != null)
            {
                for (int i = 0; i < remove.Count; i++)
                {
                    Printer.Warning($"[OptionsProfile] Expired stale upload buffer: {remove[i]}", LogImportanceMode.Verbose);
                    UploadBuffers.Remove(remove[i]);
                }
            }
        }

        private static byte[] Combine(byte[][] parts)
        {
            int total = 0;
            for (int i = 0; i < parts.Length; i++)
                total += parts[i]?.Length ?? 0;

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
