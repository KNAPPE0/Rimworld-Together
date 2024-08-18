using System;
using static Shared.CommonEnumerators;

namespace Shared
{
    [Serializable]
    public class CreationOrder
    {
        // Data representing the object to be created
        public byte[] DataToCreate { get; set; } = Array.Empty<byte>(); // Initialized to an empty byte array

        // Type of creation (e.g., Human, Animal, Thing)
        public CreationType CreationType { get; set; } = CreationType.Human; // Default to a sensible type, like Human
    }
}