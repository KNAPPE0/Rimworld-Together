using System;
using System.Collections.Generic;
using static Shared.CommonEnumerators;

namespace Shared
{
    [Serializable]
    public class LoginData
    {
        // Username for the login attempt
        public string Username { get; set; } = string.Empty; // Default to an empty string

        // Password for the login attempt
        public string Password { get; set; } = string.Empty; // Default to an empty string

        // Response after attempting to log in
        public LoginResponse TryResponse { get; set; } = LoginResponse.InvalidLogin; // Default to 'InvalidLogin'

        // Version of the client attempting to log in
        public string ClientVersion { get; set; } = string.Empty; // Default to an empty string

        // List of running mods on the client
        public List<string> RunningMods { get; set; } = new List<string>(); // Initialize with an empty list

        // Additional details related to the login attempt
        public List<string> ExtraDetails { get; set; } = new List<string>(); // Initialize with an empty list
    }
}