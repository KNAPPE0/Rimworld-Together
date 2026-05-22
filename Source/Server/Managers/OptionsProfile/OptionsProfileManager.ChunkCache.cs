using Shared.Misc;
using System;
using System.Collections.Generic;

namespace GameServer.Managers
{
    /// <summary>
    /// Cached chunked form of <c>CurrentProfile</c>. Rebuilt only when the
    /// profile changes — the previous code re-allocated chunks on every send.
    /// </summary>
    public static partial class OptionsProfileManager
    {
        private static readonly object ChunkCacheLock = new object();
        private static string CachedChunksHash;
        private static List<byte[]> CachedChunks;

        private static List<byte[]> GetCachedChunks(byte[] zipBytes, string hash)
        {
            lock (ChunkCacheLock)
            {
                if (CachedChunks != null && string.Equals(CachedChunksHash, hash, StringComparison.OrdinalIgnoreCase))
                    return CachedChunks;

                CachedChunks = ConfigProfileUtility.SplitIntoChunks(zipBytes, ChunkSizeBytes);
                CachedChunksHash = hash;
                return CachedChunks;
            }
        }

        private static void InvalidateChunkCache()
        {
            lock (ChunkCacheLock)
            {
                CachedChunks = null;
                CachedChunksHash = null;
            }
        }
    }
}
