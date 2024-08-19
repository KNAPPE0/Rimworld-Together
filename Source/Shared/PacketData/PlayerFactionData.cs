using System;
using System.Collections.Generic;
using static Shared.CommonEnumerators;

namespace Shared
{
    [Serializable]
    public class PlayerFactionData
    {
        // The primary property using PascalCase
        public FactionManifestMode ManifestMode { get; set; } = FactionManifestMode.Create;

        // A secondary property using camelCase for backward compatibility
        public FactionManifestMode manifestMode
        {
            get => ManifestMode;
            set => ManifestMode = value;
        }

        // A string to hold basic data related to the faction manifest
        public string ManifestDataString { get; set; } = string.Empty;

        // Secondary property with camelCase
        public string manifestDataString
        {
            get => ManifestDataString;
            set => ManifestDataString = value;
        }

        // An integer to hold numeric data related to the faction manifest
        public int ManifestDataInt { get; set; } = 0;

        // Secondary property with camelCase
        public int manifestDataInt
        {
            get => ManifestDataInt;
            set => ManifestDataInt = value;
        }

        // A list to hold more complex data related to the faction manifest
        public List<string> ManifestComplexData { get; set; } = new List<string>();

        // Secondary property with camelCase
        public List<string> manifestComplexData
        {
            get => ManifestComplexData;
            set => ManifestComplexData = value;
        }

        // A secondary list to hold additional complex data related to the faction manifest
        public List<string> ManifestSecondaryComplexData { get; set; } = new List<string>();

        // Secondary property with camelCase
        public List<string> manifestSecondaryComplexData
        {
            get => ManifestSecondaryComplexData;
            set => ManifestSecondaryComplexData = value;
        }
    }
}