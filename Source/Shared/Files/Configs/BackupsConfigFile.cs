using System;
using System.IO;

namespace Shared.Files.Configs
{
    public class BackupsConfigFile : BaseFile
    {
        public static string SavePath { get; set; } = string.Empty;

        public bool AutomaticBackups { get; set; } = true;

        public float IntervalHours { get; set; } = 24f;

        public bool AutomaticDeletion { get; set; } = true;

        public int Amount { get; set; } = 3;
    }
}
