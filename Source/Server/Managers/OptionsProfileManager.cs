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
    public static class OptionsProfileManager
    {
        private const int ChunkSizeBytes = 256 * 1024;
        private const int SendDedupeWindowMs = 5000;

        private static readonly Dictionary<string, IncomingUploadBuffer> UploadBuffers =
            new Dictionary<string, IncomingUploadBuffer>(StringComparer.OrdinalIgnoreCase);

        private static readonly object SendGateLock = new object();
        private static readonly Dictionary<string, long> RecentlySent =
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        private static long LastPruneTicks;

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

                // Always send current enforcement state first.
                PKT_ModConfig statePacket = new PKT_ModConfig
                {
                    _stepMode = PKT_ModConfig.ModConfigStepMode.Send,
                    _configFile = GameServer.Core.Master.ModConfig
                };

                client.Listener.EnqueuePacket(PacketHeader.ModManager, statePacket);

                // If no profile exists, stop here.
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

            List<byte[]> chunks = ConfigProfileUtility.SplitIntoChunks(CurrentProfile.ZipBytes, ChunkSizeBytes);

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
            if (data._chunkIndex < 0 || data._chunkIndex >= data._chunkCount) return;

            string key = $"{username}|{hash}";

            if (!UploadBuffers.TryGetValue(key, out IncomingUploadBuffer buffer))
            {
                buffer = new IncomingUploadBuffer
                {
                    Key = key,
                    Hash = hash,
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

            if (buffer.ReceivedCount < buffer.ChunkCount)
                return;

            byte[] full = Combine(buffer.Chunks);
            UploadBuffers.Remove(key);

            string computed = ConfigProfileUtility.Sha256Hex(full);
            if (!string.Equals(computed, buffer.Hash, StringComparison.OrdinalIgnoreCase))
            {
                Printer.Warning($"[OptionsProfile] Upload hash mismatch by {username}. Expected={buffer.Hash} Got={computed}");
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

                // Force-send actual profile data right now.
                SendProfileToClient(client, forceSend: true);
            }
            catch (Exception e)
            {
                Printer.Warning($"[OptionsProfile] SendRequiredProfileForJoin failed: {e}");
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