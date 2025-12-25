using System;
using static Shared.CommonEnumerators;

namespace Shared.Files
{
    public class SettlementFile
    {
        public int Tile { get; set; } = -1;

        public string Username { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public Goodwill Goodwill { get; set; } = Goodwill.Neutral;
    }
}