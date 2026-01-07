using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Shared.Misc
{
    public static class ConfigProfileUtility
    {
        // Never enforce personal/user preferences, tutorial progress, caches, or mod list/load-order files.
        // Mod list enforcement should be handled by the server's required/optional/forbidden logic (and restart only when needed).
        public static readonly HashSet<string> DefaultExcludeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Vanilla / user preference files
            "KeyPrefs.xml",
            "Prefs.xml",
            "Resolution.xml",
            "UILayout.xml",

            // Vanilla knowledge/tutorial progress
            "Knowledge.xml",

            // Version marker / misc
            "LastPlayedVersion.txt",

            // Mod list + backups (do NOT enforce via profile)
            "ModsConfig.xml",
            "backup_ModsConfig.xml",

            // Common caches (do NOT enforce via profile)
            "TrueTerrainColorsCache.xml",
        };

        // Additional broad filters (keeps bandwidth down + avoids enforcing generated files)
        public static readonly string[] DefaultExcludeNamePrefixes =
        {
            "backup_"
        };

        public static readonly string[] DefaultExcludeNameSuffixes =
        {
            "Cache.xml",
            "Cache.json"
        };

        public static byte[] CreateConfigZipBytes(
            string configFolderPath,
            HashSet<string> excludeFiles = null,
            string[] excludePrefixes = null,
            string[] excludeSuffixes = null)
        {
            if (string.IsNullOrEmpty(configFolderPath)) throw new ArgumentException(nameof(configFolderPath));
            if (!Directory.Exists(configFolderPath)) throw new DirectoryNotFoundException(configFolderPath);

            excludeFiles ??= DefaultExcludeFiles;
            excludePrefixes ??= DefaultExcludeNamePrefixes;
            excludeSuffixes ??= DefaultExcludeNameSuffixes;

            using (MemoryStream ms = new MemoryStream())
            {
                using (ZipArchive zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
                {
                    string root = NormalizeDir(configFolderPath);

                    foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                    {
                        string nameOnly = Path.GetFileName(file);

                        if (ShouldExcludeByName(nameOnly, excludeFiles, excludePrefixes, excludeSuffixes))
                            continue;

                        FileInfo info = new FileInfo(file);

                        // Safety: avoid giant files (usually caches/log dumps)
                        if (info.Length > 25_000_000) continue;

                        // Preserve relative structure inside Config\
                        string entryName = MakeRelativePath(root, file).Replace("\\", "/");
                        zip.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
                    }
                }

                return ms.ToArray();
            }
        }

        public static void ExtractZipBytesToFolder(byte[] zipBytes, string destinationFolder)
        {
            if (zipBytes == null || zipBytes.Length == 0) throw new ArgumentException(nameof(zipBytes));
            if (string.IsNullOrEmpty(destinationFolder)) throw new ArgumentException(nameof(destinationFolder));

            Directory.CreateDirectory(destinationFolder);

            // Zip-slip protection root
            string destRoot = Path.GetFullPath(destinationFolder);
            if (!destRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
                destRoot += Path.DirectorySeparatorChar;

            using (MemoryStream ms = new MemoryStream(zipBytes))
            using (ZipArchive zip = new ZipArchive(ms, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    // Normalize separators coming from zip entries
                    string entryPath = entry.FullName.Replace('/', Path.DirectorySeparatorChar);

                    // Directory entry
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        string dirPath = Path.Combine(destinationFolder, entryPath);
                        string fullDir = Path.GetFullPath(dirPath);

                        if (!fullDir.StartsWith(destRoot, StringComparison.OrdinalIgnoreCase))
                            continue;

                        Directory.CreateDirectory(fullDir);
                        continue;
                    }

                    string outPath = Path.Combine(destinationFolder, entryPath);
                    string fullOutPath = Path.GetFullPath(outPath);

                    // Prevent writing outside destinationFolder (zip slip)
                    if (!fullOutPath.StartsWith(destRoot, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string outDir = Path.GetDirectoryName(fullOutPath);
                    if (!string.IsNullOrEmpty(outDir))
                        Directory.CreateDirectory(outDir);

                    entry.ExtractToFile(fullOutPath, overwrite: true);
                }
            }
        }

        public static string Sha256Hex(byte[] bytes)
        {
            if (bytes == null) return string.Empty;

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }

        public static List<byte[]> SplitIntoChunks(byte[] bytes, int chunkSizeBytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (chunkSizeBytes <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSizeBytes));

            List<byte[]> chunks = new List<byte[]>();
            int offset = 0;

            while (offset < bytes.Length)
            {
                int remaining = bytes.Length - offset;
                int take = Math.Min(chunkSizeBytes, remaining);

                byte[] chunk = new byte[take];
                Buffer.BlockCopy(bytes, offset, chunk, 0, take);
                chunks.Add(chunk);

                offset += take;
            }

            if (chunks.Count == 0) chunks.Add(Array.Empty<byte>());
            return chunks;
        }

        private static bool ShouldExcludeByName(
            string nameOnly,
            HashSet<string> excludeFiles,
            string[] excludePrefixes,
            string[] excludeSuffixes)
        {
            if (string.IsNullOrEmpty(nameOnly)) return true;

            if (excludeFiles != null && excludeFiles.Contains(nameOnly))
                return true;

            if (excludePrefixes != null)
            {
                for (int i = 0; i < excludePrefixes.Length; i++)
                {
                    string p = excludePrefixes[i];
                    if (!string.IsNullOrEmpty(p) && nameOnly.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            if (excludeSuffixes != null)
            {
                for (int i = 0; i < excludeSuffixes.Length; i++)
                {
                    string s = excludeSuffixes[i];
                    if (!string.IsNullOrEmpty(s) && nameOnly.EndsWith(s, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        private static string NormalizeDir(string path)
        {
            string p = Path.GetFullPath(path);
            if (p.EndsWith(Path.DirectorySeparatorChar.ToString()))
                return p.Substring(0, p.Length - 1);
            return p;
        }

        private static string MakeRelativePath(string rootDir, string fullPath)
        {
            string root = NormalizeDir(rootDir) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(fullPath);

            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return Path.GetFileName(fullPath);

            return full.Substring(root.Length);
        }
    }
}