using System;
using System.IO;
using MessagePack;
using Newtonsoft.Json;
using Shared.Misc;

namespace Shared.Files.Configs
{
    public class DifficultyConfigFile : BaseFile
    {
        public static string SavePath { get; set; } = string.Empty;

        public bool IsEnforced { get; set; } = false;

        public string ScribeData { get; set; } = string.Empty;
    }
}
