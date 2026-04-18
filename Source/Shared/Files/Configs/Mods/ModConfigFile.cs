using System.Collections.Generic;

namespace Shared.Files.Configs.Mods
{
    public class ModConfigFile : BaseFile
    {
        public static string SavePath { get; set; } = string.Empty;

        public enum ModType { Required, Optional, Forbidden };

        public List<ModConfig> ModConfigs { get; set; } = new List<ModConfig>();

        // KMH: Options profile enforcement
        public bool IsEnforced { get; set; } = false;
    }
}