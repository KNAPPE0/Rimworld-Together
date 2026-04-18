using System;
using System.IO;

namespace Shared.Files.Configs.Mods
{
    public class OptionsProfile : BaseFile
    {
        public static string SavePath { get; set; } = string.Empty;

        public string ProfileHash { get; set; } = string.Empty;

        public long UpdatedUtcTicks { get; set; } = 0;

        public byte[] ZipBytes { get; set; } = Array.Empty<byte>();

        public bool HasProfile => ZipBytes != null && ZipBytes.Length > 0;

        public void Save()
        {
            try { Serializer.SerializeToFile(SavePath, this); }
            catch (Exception e) { throw new Exception(e.ToString()); }
        }

        public static object Load<T>()
        {
            if (File.Exists(SavePath)) return Serializer.SerializeFromFile<T>(SavePath);
            else
            {
                OptionsProfile file = new OptionsProfile();
                Serializer.SerializeToFile(SavePath, file);
                return file;
            }
        }
    }
}