using System;
using System.Collections.Generic;
using static Shared.CommonEnumerators;

namespace Shared
{
    [Serializable]
    public class PlayerFactionData
    {
        // The mode of the faction manifest, determining the type of operation
        public FactionManifestMode ManifestMode { get; set; } = FactionManifestMode.Create; // Default to 'Create', adjust as necessary

        // A string to hold basic data related to the faction manifest
        public string ManifestDataString { get; set; } = string.Empty; // Default to an empty string

        // An integer to hold numeric data related to the faction manifest
        public int ManifestDataInt { get; set; } = 0; // Default to 0

        // A list to hold more complex data related to the faction manifest
        public List<string> ManifestComplexData { get; set; } = new List<string>(); // Initialize with an empty list

        // A secondary list to hold additional complex data related to the faction manifest
        public List<string> ManifestSecondaryComplexData { get; set; } = new List<string>(); // Initialize with an empty list
    }
}