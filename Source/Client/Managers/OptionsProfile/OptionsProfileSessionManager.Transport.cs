using GameClient.Dialogs;
using GameClient.Dialogs.Default;
using GameClient.Misc;
using Shared;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using TCPNetwork;
using TCPNetwork.Packets;

namespace GameClient.Managers
{
    /// <summary>
    /// Network-facing surface: outbound profile request/publish + inbound
    /// chunk reassembly. Once a full profile is reassembled and verified
    /// it's handed off to the Apply partial.
    /// </summary>
    public static partial class OptionsProfileSessionManager
    {
        private static readonly Dictionary<string, IncomingProfileBuffer> IncomingProfiles =
            new Dictionary<string, IncomingProfileBuffer>(StringComparer.OrdinalIgnoreCase);

        private static int PendingManualRequest;

        private class IncomingProfileBuffer
        {
            public string Hash;
            public long UpdatedTicks;
            public int ChunkCount;
            public byte[][] Chunks;
            public int ReceivedCount;
        }

        public static void RequestServerOptionsProfile(bool isManual)
        {
            try
            {
                if (Network.ServerEndpoint == null) return;

                if (isManual)
                    Interlocked.Exchange(ref PendingManualRequest, 1);

                PKT_ModConfig req = new PKT_ModConfig
                {
                    _requestOptionsProfile = true,
                    _stepMode = PKT_ModConfig.ModConfigStepMode.Send
                };

                Network.ServerEndpoint.EnqueuePacket(PacketHeader.ModManager, req);
                Printer.Warning("[OptionsProfile] Requested profile from server.");
            }
            catch (Exception e)
            {
                Printer.Warning($"[OptionsProfile] RequestServerOptionsProfile failed: {e}");
            }
        }

        public static void PublishCurrentConfigProfileToServer()
        {
            try
            {
                if (!SessionHandler.IsAdmin)
                {
                    DLG_Base.PushNewDialog(new DLG_Message("Error", new[] { "Admin only." }));
                    return;
                }

                if (Network.ServerEndpoint == null)
                {
                    DLG_Base.PushNewDialog(new DLG_Message("Error", new[] { "Not connected." }));
                    return;
                }

                Directory.CreateDirectory(ConfigPath);

                byte[] zip = ConfigProfileUtility.CreateConfigZipBytes(ConfigPath, ConfigProfileUtility.DefaultExcludeFiles);
                string hash = ConfigProfileUtility.Sha256Hex(zip);
                List<byte[]> chunks = ConfigProfileUtility.SplitIntoChunks(zip, ChunkSizeBytes);
                long ticks = DateTime.UtcNow.Ticks;

                for (int i = 0; i < chunks.Count; i++)
                {
                    PKT_ModConfig part = new PKT_ModConfig
                    {
                        _uploadOptionsProfile = true,
                        _stepMode = PKT_ModConfig.ModConfigStepMode.Send,
                        _optionsProfileHash = hash,
                        _optionsProfileUpdatedUtcTicks = ticks,
                        _chunkIndex = i,
                        _chunkCount = chunks.Count,
                        _chunkBytes = chunks[i]
                    };

                    Network.ServerEndpoint.EnqueuePacket(PacketHeader.ModManager, part);
                }

                if (SessionHandler.CurrentModConfig != null)
                    SessionHandler.CurrentModConfig.IsEnforced = true;

                DLG_Base.PushNewDialog(new DLG_Message("Profile",
                    new[] { "Published server options profile.", "Enforcement is now active on the server.", $"Hash: {hash}" }));
            }
            catch (Exception e)
            {
                Printer.Warning($"[OptionsProfile] PublishCurrentConfigProfileToServer failed: {e}");
                DLG_Base.PushNewDialog(new DLG_Message("Error", new[] { "Failed to publish profile. Check logs." }));
            }
        }

        public static void ReceiveOptionsProfilePacket(PKT_ModConfig data)
        {
            if (data == null) return;

            if (data._noOptionsProfileAvailable)
            {
                bool wasManual = Interlocked.Exchange(ref PendingManualRequest, 0) == 1;

                if (wasManual)
                {
                    MainThreadHandler.Instance.Enqueue(delegate
                    {
                        DLG_Base.PushNewDialog(new DLG_Message("Profile",
                            new[] { "This server does not currently have a published options profile." }));
                    });
                }

                return;
            }

            if (!data._isOptionsProfileChunk) return;
            if (string.IsNullOrEmpty(data._optionsProfileHash)) return;
            if (data._chunkCount <= 0) return;
            if (data._chunkIndex < 0 || data._chunkIndex >= data._chunkCount) return;

            if (!IncomingProfiles.TryGetValue(data._optionsProfileHash, out IncomingProfileBuffer buffer))
            {
                buffer = new IncomingProfileBuffer
                {
                    Hash = data._optionsProfileHash,
                    UpdatedTicks = data._optionsProfileUpdatedUtcTicks,
                    ChunkCount = data._chunkCount,
                    Chunks = new byte[data._chunkCount][],
                    ReceivedCount = 0
                };

                IncomingProfiles[data._optionsProfileHash] = buffer;
            }

            if (buffer.Chunks[data._chunkIndex] == null)
            {
                buffer.Chunks[data._chunkIndex] = data._chunkBytes ?? Array.Empty<byte>();
                buffer.ReceivedCount++;
            }

            if (buffer.ReceivedCount < buffer.ChunkCount)
                return;

            byte[] full = Combine(buffer.Chunks);
            IncomingProfiles.Remove(buffer.Hash);

            string computed = ConfigProfileUtility.Sha256Hex(full);
            if (!string.Equals(computed, buffer.Hash, StringComparison.OrdinalIgnoreCase))
            {
                MainThreadHandler.Instance.Enqueue(delegate
                {
                    DLG_Base.PushNewDialog(new DLG_Message("Error",
                        new[] { "Options profile download failed because the hash did not match." }));
                });
                return;
            }

            string profileFolder = GetProfileFolder(buffer.Hash);
            SafeDeleteDirectory(profileFolder);
            Directory.CreateDirectory(profileFolder);
            ConfigProfileUtility.ExtractZipBytesToFolder(full, profileFolder);

            bool alreadyActive = State != null
                && State.IsEnforcedActive
                && string.Equals(State.ActiveProfileHash, buffer.Hash, StringComparison.OrdinalIgnoreCase);

            if (alreadyActive)
            {
                Printer.Warning($"[OptionsProfile] Profile {buffer.Hash} already active. Reapplying to disk.");
                ReapplyProfileToDiskIfActive(softReload: true);
                return;
            }

            string pf = profileFolder;
            string h = buffer.Hash;
            long t = buffer.UpdatedTicks;

            MainThreadHandler.Instance.Enqueue(delegate
            {
                ApplyEnforcedProfile(pf, h, t);
            });
        }

        public static void HandleJoinRequirement(PKT_ModConfig data)
        {
            try
            {
                string requiredHash = data?._optionsProfileHash ?? string.Empty;
                string localHash = GetActiveHash();

                bool alreadyMatching =
                    IsEnforcementActive() &&
                    !string.IsNullOrWhiteSpace(localHash) &&
                    string.Equals(localHash, requiredHash, StringComparison.OrdinalIgnoreCase);

                if (alreadyMatching)
                {
                    Printer.Warning($"[OptionsProfile] Required profile already active locally. Hash={localHash}");
                    return;
                }

                MainThreadHandler.Instance.Enqueue(delegate
                {
                    DLG_Base.PushNewDialog(new DLG_Message(
                        "Server Config Required",
                        new[]
                        {
                            "This server requires its config profile before you can join.",
                            "The profile is being downloaded and will be verified.",
                            "After it is applied, the game will restart."
                        }));
                });

                RequestServerOptionsProfile(isManual: false);
            }
            catch (Exception e)
            {
                Printer.Warning($"[OptionsProfile] HandleJoinRequirement failed: {e}");
            }
        }
    }
}
