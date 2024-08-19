using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using static Shared.CommonEnumerators;

namespace GameServer
{
    public static class Logger
    {
        // Semaphore for thread safety
        private static readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        // Dictionary mapping log modes to console colors
        private static readonly Dictionary<LogMode, ConsoleColor> colorDictionary = new Dictionary<LogMode, ConsoleColor>
        {
            { LogMode.Message, ConsoleColor.White },
            { LogMode.Warning, ConsoleColor.Yellow },
            { LogMode.Error, ConsoleColor.Red },
            { LogMode.Title, ConsoleColor.Green },
            { LogMode.Outsider, ConsoleColor.Magenta }
        };

        // Wrapper to write log in white color
        public static void Message(string message) => WriteToConsole(message, LogMode.Message);

        // Wrapper to write log in yellow color
        public static void Warning(string message) => WriteToConsole(message, LogMode.Warning);

        // Wrapper to write log in red color
        public static void Error(string message) => WriteToConsole(message, LogMode.Error);

        // Wrapper to write log in green color
        public static void Title(string message) => WriteToConsole(message, LogMode.Title);

        // Wrapper to write log in magenta color
        public static void Outsider(string message) => WriteToConsole(message, LogMode.Outsider);

        // Function to write to the console
        private static void WriteToConsole(string text, LogMode mode = LogMode.Message, bool writeToLogs = true)
        {
            semaphoreSlim.Wait();

            try
            {
                if (writeToLogs)
                {
                    WriteToLogs(text);
                }

                Console.ForegroundColor = colorDictionary[mode];
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] | {text}");
                Console.ResetColor();

                if (Master.discordConfig != null && Master.discordConfig.Enabled)
                {
                    DiscordManager.SendMessageToConsoleChannelBuffer(text);
                }
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        // Function to write contents to the log file
        private static void WriteToLogs(string toLog)
        {
            var logEntry = $"[{DateTime.Now:HH:mm:ss}] | {toLog}{Environment.NewLine}";
            var date = DateTime.Now.Date;
            var logFileName = $"{date:yyyy-MM-dd}.txt";
            var logFilePath = Path.Combine(Master.systemLogsPath, logFileName);

            semaphoreSlim.Wait();
            try
            {
                File.AppendAllText(logFilePath, logEntry);
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }
    }
}