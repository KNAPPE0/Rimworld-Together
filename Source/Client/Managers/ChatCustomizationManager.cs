using System;
using System.IO;
using UnityEngine;
using Verse;
using GameClient.Core;

namespace GameClient.Managers
{
    // Manages loading and saving of chat customization settings.
    public static class ChatCustomizationManager
    {
        private static readonly string ConfigFilename = "RimworldTogether.ChatCustomization.json";

        private static string ConfigPath
        {
            get
            {
                var dir = GenFilePaths.ConfigFolderPath;
                return Path.Combine(dir, ConfigFilename);
            }
        }

        /// <summary>
        /// Current chat customization settings.
        /// </summary>
        public static ChatCustomizationData Settings { get; private set; } = new ChatCustomizationData();

        static ChatCustomizationManager()
        {
            LoadSettings();
        }

        /// <summary>
        /// Load settings from disk, or create defaults if none exist.
        /// </summary>
        public static void LoadSettings()
        {
            try
            {
                var directory = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    Settings = JsonUtility.FromJson<ChatCustomizationData>(json) ?? new ChatCustomizationData();
                }
                else
                {
                    Settings = new ChatCustomizationData();
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[ChatCustomizationManager] Failed to load settings: {ex.Message}");
                Settings = new ChatCustomizationData();
            }
        }

        /// <summary>
        /// Save current settings to disk.
        /// </summary>
        public static void SaveSettings()
        {
            try
            {
                var directory = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var json = JsonUtility.ToJson(Settings, true);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                Log.Error($"[ChatCustomizationManager] Failed to save settings: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Data container for chat customization options.
    /// </summary>
    [Serializable]
    public class ChatCustomizationData
    {
        /// <summary>
        /// Font color in HTML hex (e.g., "#FFFFFF").
        /// </summary>
        public string FontColor = "#FFFFFF";

        /// <summary>
        /// Background color in HTML hex (e.g., "#000000").
        /// </summary>
        public string BackgroundColor = "#000000";

        /// <summary>
        /// Font size: "Tiny", "Small", or "Medium".
        /// </summary>
        public string FontSize = "Medium";

        /// <summary>
        /// Width of the chat window.
        /// </summary>
        public float WindowWidth = 700f;

        /// <summary>
        /// Height of the chat window.
        /// </summary>
        public float WindowHeight = 500f;
    }
}