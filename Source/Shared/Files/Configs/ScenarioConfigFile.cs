using System;
using System.IO;

namespace Shared.Files.Configs
{
    public class ScenarioConfigFile : BaseFile
    {
        public static string SavePath { get; set; } = string.Empty;

        public bool IsEnforced { get; set; } = false;

        public string Name { get; set; } = string.Empty;
    }
}