using System;
using System.IO;
using UnityEngine;
using Verse;
using GameClient.Core;

namespace GameClient.Managers
{
    public enum ChatPanel { Chat, Leaderboard }

    [Serializable]
    public class ChatCustomizationData
    {
        // Default font size (in pixels)
        public float FontSize = 14f;

        // Default font color
        public Color FontColor = Color.white;

        // Background tint color (RGBA)
        public Color BackgroundColor = new Color(0f, 0f, 0f, 0.5f);

        // Auto‐scroll chat by default
        public bool AutoScrollDefault = true;

        // Initial panel to open
        public ChatPanel DefaultPanel = ChatPanel.Chat;
    }

    public static class ChatCustomizationManager
    {
        public static ChatCustomizationData Settings { get; private set; }

        private static string ConfigPath =>
            Path.Combine(Master.SavesFolderPath, "ChatCustomization.json");

        static ChatCustomizationManager()
        {
            Load();
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    Settings = JsonUtility.FromJson<ChatCustomizationData>(json);
                }
                else
                {
                    Settings = new ChatCustomizationData();
                    Save();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[ChatCustomizationManager] Failed to load settings: {ex.Message}");
                Settings = new ChatCustomizationData();
            }
        }

        public static void Save()
        {
            try
            {
                string json = JsonUtility.ToJson(Settings, true);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                Log.Error($"[ChatCustomizationManager] Failed to save settings: {ex.Message}");
            }
        }
    }
}
