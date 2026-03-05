using System;
using System.IO;

namespace Shared.Files.Configs
{
    public class StorytellerConfigFile : BaseFile
    {
        public static string SavePath { get; set; } = string.Empty;

        public bool IsEnforced { get; set; } = false;

        public string DefName { get; set; } = string.Empty;
    }
}