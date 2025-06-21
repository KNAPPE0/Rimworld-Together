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

        // Current chat customization settings.
        public static ChatCustomizationData Settings { get; private set; } = new ChatCustomizationData();

        static ChatCustomizationManager()
        {
            LoadSettings();
        }

        // Load settings from disk, or create defaults if none exist.
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

        // Save current settings to disk.
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

    // Data container for chat customization options.
    [Serializable]
    public class ChatCustomizationData
    {
        // Font color in HTML hex (e.g., "#FFFFFF").
        public string FontColor = "#FFFFFF";

        // Background color in HTML hex (e.g., "#000000").
        public string BackgroundColor = "#000000";

        // Font size: "Tiny", "Small", or "Medium".
        public string FontSize = "Medium";

        // Width of the chat window.
        public float WindowWidth = 700f;

        // Height of the chat window.
        public float WindowHeight = 500f;
    }
}