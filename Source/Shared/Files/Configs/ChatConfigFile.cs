using System;
using System.IO;

namespace Shared.Files.Configs
{
    public class ChatConfigFile : BaseFile
    {
        public static string SavePath { get; set; } = string.Empty;

        public bool EnableMoTD { get; set; } = false;

        public string MessageOfTheDay { get; set; } = "Remember to drink water";

        public bool LoginNotifications { get; set; } = false;

        public bool DisconnectNotifications { get; set; } = false;
    }
}