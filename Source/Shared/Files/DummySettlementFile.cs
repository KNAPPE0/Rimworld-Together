using System;
using static Shared.CommonEnumerators;

namespace Shared
{
    [Serializable]
    public class DummySettlementFile
    {
        public int tile;
        public string owner = string.Empty; // Initialize with an empty string to avoid null issues
        public Goodwill goodwill;
    }
}