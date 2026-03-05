using System;
using System.Collections.Generic;
using System.IO;

namespace Shared.Files.Configs.Mods
{
    public class ModsConfigFile : BaseFile
    {
        public static string SavePath { get; set; } = string.Empty;

        public enum ModType { Required, Optional, Forbidden };

        public bool IsEnforced { get; set; } = false;

        public List<ModConfig> ModConfigs { get; set; } = new List<ModConfig>();
    }
}