using Shared.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using static Shared.Misc.Printer;

namespace Shared
{
    public abstract class CMD_Base
    {
        public string Prefix { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public int ParameterCount { get; set; } = 0;

        public bool IsChatCommand { get; set; } = false;

        /// <summary>
        /// KMH: Optional category override — leave empty to auto-classify by
        /// prefix substring. Categorisation is purely cosmetic (drives `!help`
        /// grouping); it does not affect what commands are actually available.
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>Resolves a display category, falling back to substring matching on Prefix.</summary>
        public string ResolvedCategory
        {
            get
            {
                if (!string.IsNullOrEmpty(Category)) return Category;
                string p = (Prefix ?? string.Empty).ToLowerInvariant();
                if (p.Contains("ban") || p.Contains("kick") || p.Contains("op") || p.Contains("whitelist") || p.Contains("pardon") || p.Contains("reset")) return "Moderation";
                if (p.Contains("treasury") || p.Contains("market") || p.Contains("quest") || p.Contains("guild") || p.Contains("housepool") || p.Contains("site")) return "Economy";
                if (p.Contains("event") || p.Contains("storyteller") || p.Contains("scenario") || p.Contains("difficulty")) return "Events";
                if (p.Contains("config") || p.Contains("enforce") || p.Contains("backup") || p.Contains("save")) return "Config";
                if (p.Contains("debug") || p.Contains("dev") || p.Contains("gc") || p.Contains("clear") || p.Contains("test")) return "Admin";
                return "Server";
            }
        }

        public abstract void Action();

        public static string[] CommandParameters { get; set; } = null;

        public static List<CMD_Base> Commands { get; set; } = new List<CMD_Base>();

        public static List<CMD_Base> ChatCommands { get; set; } = new List<CMD_Base>();

        private static Semaphore Semaphore { get; set; } = new Semaphore(1, 1);

        public static void GetAllCommands()
        {
            foreach (Type type in Assembly.GetCallingAssembly().GetTypes().Where(fetch => fetch.IsSubclassOf(typeof(CMD_Base))))
            {
                CMD_Base command = (CMD_Base)Activator.CreateInstance(type);
                if (command.IsChatCommand) ChatCommands.Add(command);
                else
                {
                    Commands.Add(command);
                    Printer.Warning($"Added command '{type.Name}'", LogImportanceMode.Extreme);
                }
            }
        }

        public static void ListenForCommands()
        {
            if (CheckIfConsoleIsInteractive())
            {
                while (true)
                {
                    ParseCommand(Console.ReadLine());
                }
            }

            else Thread.Sleep(1);
        }

        private static bool CheckIfConsoleIsInteractive()
        {
            try { return Console.In.Peek() != -1 ? true : false; }
            catch 
            { 
                Printer.Warning($"Couldn't find interactive console, disabling commands");
                return false;
            }
        }

        // KMH: Capture buffer for Discord command output relay
        private static readonly object CaptureLock = new object();
        private static List<string> CaptureBuffer { get; set; } = null;
        private static bool IsCapturing { get; set; } = false;

        /// <summary>True when a Discord command is executing and output is being captured.</summary>
        public static bool IsCommandCapturing => IsCapturing;

        /// <summary>Runs a command and returns captured Printer output lines.</summary>
        public static string[] ExecuteCommand(string input, bool fromDiscord = false)
        {
            if (string.IsNullOrWhiteSpace(input)) return Array.Empty<string>();

            if (!fromDiscord)
            {
                ParseCommand(input);
                return Array.Empty<string>();
            }

            // Capture mode: intercept Printer output during command execution
            lock (CaptureLock)
            {
                CaptureBuffer = new List<string>();
                IsCapturing = true;
            }

            try
            {
                ParseCommand(input);
            }
            finally
            {
                lock (CaptureLock) { IsCapturing = false; }
            }

            string[] result;
            lock (CaptureLock)
            {
                result = CaptureBuffer.ToArray();
                CaptureBuffer = null;
            }
            return result;
        }

        /// <summary>Called by Printer hooks to capture output when running Discord commands.</summary>
        public static void TryCaptureOutput(string text)
        {
            lock (CaptureLock)
            {
                if (IsCapturing && CaptureBuffer != null && !string.IsNullOrEmpty(text))
                    CaptureBuffer.Add(text);
            }
        }

        private static void ParseCommand(string input)
        {
            Semaphore.WaitOne();

            try
            {
                int parameterCount = input.Split(' ').Length - 1;
                string parsedPrefix = input.Split(' ')[0].ToLower();

                // KMH: Previously when no args were typed (input == "help"),
                // input.Replace("help ", "") was a no-op (no trailing space to
                // remove) and Split(' ') returned ["help"]. Commands that
                // checked CommandParameters[0] then mis-read the prefix word
                // as the first argument (e.g. `help help` → unknown category).
                // Now we explicitly produce an empty array when there are no args.
                CommandParameters = parameterCount == 0
                    ? Array.Empty<string>()
                    : input.Substring(parsedPrefix.Length + 1).Split(' ');

                CMD_Base toFetch = Commands.FirstOrDefault(x => x.Prefix == parsedPrefix);
                if (toFetch == null) Printer.Warning($"Command '{parsedPrefix}' was not found");
                else
                {
                    if (toFetch.ParameterCount == parameterCount || toFetch.ParameterCount == 0 || toFetch.ParameterCount == -1) toFetch.Action();
                    else Printer.Warning($"Wrong parameter count for '{toFetch.Prefix}'");
                }
            }
            catch (Exception ex) { Printer.Error(ex); }

            Semaphore.Release();
        }
    }
}
