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

        private static readonly Dictionary<string, IncomingUploadBuffer> UploadBuffers =
            new Dictionary<string, IncomingUploadBuffer>(StringComparer.OrdinalIgnoreCase);

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

        public static void HandleClientRequest(ServerClient client)
        {
            if (client == null) return;

            if (CurrentProfile == null || !CurrentProfile.HasProfile || string.IsNullOrEmpty(CurrentProfile.ProfileHash))
            {
                Printer.Warning($"[OptionsProfile] {client.UserFile?.Username} requested profile, but none is available.", LogImportanceMode.Verbose);

                ModConfigData none = new ModConfigData
                {
                    _isOptionsProfileChunk = true,
                    _noOptionsProfileAvailable = true
                };

                client.Listener.EnqueuePacket(PacketHeader.ModManager, none);
                return;
            }

            List<byte[]> chunks = ConfigProfileUtility.SplitIntoChunks(CurrentProfile.ZipBytes, ChunkSizeBytes);

            Printer.Warning($"[OptionsProfile] Sending profile to {client.UserFile?.Username}. Chunks={chunks.Count} Hash={CurrentProfile.ProfileHash}", LogImportanceMode.Verbose);

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