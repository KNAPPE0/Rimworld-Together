using Shared.Files.Configs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Files
{
    public class LeaderboardFile : BaseFile
    {
        public static string SavePath { get; set; } = string.Empty;
        
        public Dictionary<string, double> Scores { get; set; } = new Dictionary<string, double>();
    }
}
