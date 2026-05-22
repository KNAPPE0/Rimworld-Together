using Shared;
using System;
using System.IO;
using Shared.Misc;

namespace GameClient.Files
{
    public class PersistentSettings
    {
        public ServerSettings ServerSettings { get; set; } = new ServerSettings();

        public UserSettings UserSettings { get; set; } = new UserSettings();

        public static string FilePath { get; set; } = string.Empty;

        // KMH 26.5.20.1: Per-frame disk I/O bug fix.
        // Every Load() call did File.Exists + a JSON deserialise from disk.
        // Five dialogs called this *inside* their DoWindowContents() to read
        // the player's username — that's 5 disks reads + 5 JSON parses
        // **per frame at 60fps**, every frame, for the entire time any of
        // those dialogs was open.
        //
        // The settings file only changes via this class's Save(), so a
        // process-wide cache is safe. SetFilePath / Regenerate explicitly
        // invalidate it.
        private static PersistentSettings _cached;
        private static readonly object CacheLock = new object();

        public static void SetFilePath(string path)
        {
            FilePath = path;
            lock (CacheLock) { _cached = null; }
        }

        public void Save()
        {
            Serializer.SerializeToFile(FilePath, this);
            // Refresh the cache so the in-memory copy matches what's on disk.
            lock (CacheLock) { _cached = this; }
        }

        public static PersistentSettings Load()
        {
            // Fast path: cache hit. No lock needed because we only do an
            // unsynchronised reference read — at worst we observe the old
            // value during a Save, which is harmless.
            PersistentSettings hit = _cached;
            if (hit != null) return hit;

            lock (CacheLock)
            {
                // Double-checked.
                if (_cached != null) return _cached;

                if (!File.Exists(FilePath)) RegenerateLocked();
                try
                {
                    PersistentSettings value = Serializer.SerializeFromFile<PersistentSettings>(FilePath);
                    if (value == null)
                    {
                        Printer.Error($"Error while parsing existing persistent settings file, was somehow null, returning default value,");
                        value = new PersistentSettings();
                    }
                    _cached = value;
                    return value;
                }
                catch (Exception e)
                {
                    Printer.Error($"Error while parsing existing persistent settings file, returning default value\n{e}");
                    // Don't cache a temporary failure — we want the next call
                    // to retry the disk read in case it was transient.
                    return new PersistentSettings();
                }
            }
        }

        public static void Regenerate()
        {
            lock (CacheLock) { RegenerateLocked(); }
        }

        // Caller must hold CacheLock.
        private static void RegenerateLocked()
        {
            PersistentSettings settings = new PersistentSettings();
            Serializer.SerializeToFile(FilePath, settings);
            _cached = settings;
        }
    }

    public class ServerSettings
    {
        public string LatestIP { get; set; } = string.Empty;

        public int LatestPort { get; set; } = int.MaxValue;

        public void Set (string ip, int port)
        {
            LatestIP = ip;
            LatestPort = port;
        }

        public void Reset()
        {
            LatestIP = string.Empty;
            LatestPort = int.MaxValue;
        }
    }

    public class UserSettings
    {
        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public void Set(string username, string password)
        {
            Username = username;
            Password = password;
        }

        public void Reset()
        {
            Username = string.Empty;
            Password = string.Empty;
        }
    }
}
