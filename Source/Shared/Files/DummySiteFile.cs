using System;
using static Shared.CommonEnumerators;

namespace Shared
{
    [Serializable]
    public class DummySiteFile
    {
        public int tile;
        public string owner = string.Empty; // Initialize with an empty string to avoid null issues
        public Goodwill goodwill;

        public int type;
        public bool fromFaction;
    }
}