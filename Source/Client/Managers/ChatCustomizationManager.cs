using System;
using System.IO;
using UnityEngine;
using Verse;
using GameClient.Core;

namespace GameClient.Managers
{
    /// <summary>
    /// Which panel to open by default in ChatTab.
    /// </summary>
    public enum ChatPanel
    {
        Chat = 0,
        Leaderboard = 1,
        Settings = 2
    }

    /// <summary>
    /// Per-user chat-window customisation (font, colours, etc.).
    /// </summary>
    public static class ChatCustomizationManager
    {
        [Serializable]
        private class Data
        {
            public float   FontSize          = 16f;
            public Color   FontColor         = Color.white;
            public Color   BackgroundColor   = new Color(0f, 0f, 0f, 0.5f);
            public bool    AutoScrollDefault = true;
            public int     LeaderboardSize   = 10;
            public ChatPanel DefaultPanel    = ChatPanel.Chat;
        }

        private static readonly string _path = Path.Combine(
            Master.SavesFolderPath, "ChatCustomization.json");
        private static Data _cfg = new Data();

        static ChatCustomizationManager() => Load();

        public static float   FontSize        { get => _cfg.FontSize;          set => _cfg.FontSize = Mathf.Clamp(value, 12f, 40f); }
        public static Color   FontColor       { get => _cfg.FontColor;         set => _cfg.FontColor = value; }
        public static Color   BackgroundColor { get => _cfg.BackgroundColor;   set => _cfg.BackgroundColor = value; }
        public static bool    AutoScrollDefault { get => _cfg.AutoScrollDefault; set => _cfg.AutoScrollDefault = value; }
        public static int     LeaderboardSize   { get => _cfg.LeaderboardSize;   set => _cfg.LeaderboardSize = Mathf.Clamp(value, 5, 100); }
        public static ChatPanel DefaultPanel    { get => _cfg.DefaultPanel;      set => _cfg.DefaultPanel = value; }

        public static void Load()
        {
            try
            {
                if (File.Exists(_path))
                {
                    var json = File.ReadAllText(_path);
                    var tmp  = JsonUtility.FromJson<Data>(json);
                    if (tmp != null) _cfg = tmp;
                }
                else Save();
            }
            catch (Exception ex)
            {
                Log.Error($"[ChatCustomization] Load failed – using defaults: {ex}");
                _cfg = new Data();
            }
        }

        public static void Save()
        {
            try
            {
                File.WriteAllText(_path, JsonUtility.ToJson(_cfg, true));
            }
            catch (Exception ex)
            {
                Log.Error($"[ChatCustomization] Save failed: {ex}");
            }
        }

        public static string FontHex
        {
            get => "#" + ColorUtility.ToHtmlStringRGBA(FontColor);
            set
            {
                if (TryParseHex(value, out var c))
                {
                    FontColor = c;
                    Save();
                }
            }
        }

        public static string BgHex
        {
            get => "#" + ColorUtility.ToHtmlStringRGBA(BackgroundColor);
            set
            {
                if (TryParseHex(value, out var c))
                {
                    BackgroundColor = c;
                    Save();
                }
            }
        }

        public static bool TryParseHex(string s, out Color colour)
        {
            colour = Color.black;
            if (string.IsNullOrWhiteSpace(s)) return false;
            if (!s.StartsWith("#")) s = "#" + s;
            return ColorUtility.TryParseHtmlString(s, out colour);
        }
    }
}