using System;
using System.Runtime.InteropServices;

namespace GameServer
{
    public class QuickEdit
    {
        private const uint ENABLE_QUICK_EDIT = 0x0040; // Flag for enabling Quick Edit mode
        private const int STD_INPUT_HANDLE = -10; // Standard input device handle

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        /// <summary>
        /// Disables the Quick Edit mode in the console, preventing the console from freezing when selecting text.
        /// </summary>
        /// <returns>True if Quick Edit mode was successfully disabled; otherwise, false.</returns>
        public bool DisableQuickEdit()
        {
            IntPtr consoleHandle = GetStdHandle(STD_INPUT_HANDLE);

            if (!GetConsoleMode(consoleHandle, out uint consoleMode))
            {
                // Unable to retrieve the current console mode.
                return false;
            }

            // Remove the Quick Edit mode from the current console mode.
            consoleMode &= ~ENABLE_QUICK_EDIT;

            if (!SetConsoleMode(consoleHandle, consoleMode))
            {
                // Unable to set the new console mode.
                return false;
            }

            return true;
        }
    }
}