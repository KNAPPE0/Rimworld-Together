using System;
using static Shared.CommonEnumerators;

namespace Shared
{
    [Serializable]
    public class CommandData
    {
        // The mode/type of the command
        public CommandMode CommandMode { get; set; } = CommandMode.Op; // Default to 'Op', adjust as necessary

        // Details of the command to be executed
        public string CommandDetails { get; set; } = string.Empty; // Default to an empty string
    }
}