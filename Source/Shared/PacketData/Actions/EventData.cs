using System;
using static Shared.CommonEnumerators;

namespace Shared
{
    [Serializable]
    public class EventData
    {
        // The current step mode of the event
        public EventStepMode EventStepMode { get; set; } = EventStepMode.Send; // Default to 'Send', adjust as necessary

        // Tiles representing the source and destination of the event
        public int FromTile { get; set; } = 0; // Tile where the event originates
        public int ToTile { get; set; } = 0;   // Tile where the event is directed

        // Unique identifier for the event
        public int EventId { get; set; } = 0; // Default to 0, represents the event's unique ID
    }
}