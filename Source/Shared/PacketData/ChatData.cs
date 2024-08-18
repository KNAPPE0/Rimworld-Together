using System;
using static Shared.CommonEnumerators;

namespace Shared
{
    [Serializable]
    public class ChatData
    {
        // Color associated with the user in the chat
        public UserColor UserColor { get; set; } = UserColor.Normal; // Default to 'Normal', adjust as necessary

        // Color associated with the message in the chat
        public MessageColor MessageColor { get; set; } = MessageColor.Normal; // Default to 'Normal', adjust as necessary

        // Username of the person sending the message
        public string Username { get; set; } = string.Empty; // Default to an empty string

        // The content of the message
        public string Message { get; set; } = string.Empty; // Default to an empty string
    }
}