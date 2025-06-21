using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameServer.Core;
using GameServer.Files;
using GameServer.Misc;
using GameServer.TCP;
using Shared;
using Shared.Packets.Data;
using static Shared.CommonEnumerators;

namespace GameServer.Managers
{
    public static class WealthManager
    {
        private static readonly string WealthFilePath = Path.Combine(
            string.IsNullOrEmpty(Master.ConfigsPath) ? "." : Master.ConfigsPath,
            "Wealth.json"
        );

        // In‐memory: UID → current wealth
        private static Dictionary<string, double> _wealthData = new(StringComparer.OrdinalIgnoreCase);

        // Triggered when wealth changes so embeds or other listeners can update
        public static event Action OnWealthChanged;

        static WealthManager()
        {
            LoadWealthData();
        }

        private static void LoadWealthData()
        {
            try
            {
                if (!File.Exists(WealthFilePath))
                {
                    _wealthData = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                    SaveWealthData();
                    return;
                }

                _wealthData = Serializer.SerializeFromFile<Dictionary<string, double>>(WealthFilePath)
                              ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Printer.Error($"[WealthManager] Failed to load Wealth.json: {ex.Message}");
                _wealthData = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void SaveWealthData()
        {
            try
            {
                Serializer.SerializeToFile(WealthFilePath, _wealthData);
            }
            catch (Exception ex)
            {
                Printer.Error($"[WealthManager] Failed to save Wealth.json: {ex.Message}");
            }
        }
        public static void SetPlayerWealth(string uid, double amount)
        {
            _wealthData[uid] = amount;
            SaveWealthData();
            OnWealthChanged?.Invoke();
        }
        public static double GetPlayerWealth(string uid)
        {
            return _wealthData.TryGetValue(uid, out var w) ? w : 0d;
        }
        public static string FormatLeaderboard(int topCount)
        {
            if (_wealthData == null || _wealthData.Count == 0)
                return "No wealth data available.";

            var topList = _wealthData
                .OrderByDescending(pair => pair.Value)
                .Take(topCount)
                .ToList();

            var lines = new List<string>
            {
                $"Top {topList.Count} richest players:"
            };

            for (int i = 0; i < topList.Count; i++)
            {
                var (uid, amount) = topList[i];
                var userFile = UserManagerH.GetUserFileFromName(uid);
                string displayName = userFile?.Label ?? uid;
                lines.Add($"{i + 1}. {displayName} ({uid}) – ${amount:N2}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        public static string FormatLeaderboardUsernames(int topCount)
        {
            if (_wealthData == null || _wealthData.Count == 0)
                return "No wealth data available.";

            var topList = _wealthData
                .OrderByDescending(pair => pair.Value)
                .Take(topCount)
                .ToList();

            var lines = new List<string>
            {
                $"Top {topList.Count} richest players:"
            };

            for (int i = 0; i < topList.Count; i++)
            {
                var (uid, amount) = topList[i];
                var userFile = UserManagerH.GetUserFileFromName(uid);
                string displayName = userFile?.Label ?? uid;
                lines.Add($"{i + 1}. {displayName} – ${amount:N2}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        [HandlesPacket(PacketHeader.WealthManager)]
        private static void ParsePacket(ServerClient client, byte[] bytes)
        {
            try
            {
                var data = Serializer.ConvertBytesToObject<WealthData>(bytes);
                SetPlayerWealth(data._uid, data._wealth);
            }
            catch (Exception ex)
            {
                Printer.Error($"[WealthManager] Error parsing incoming packet: {ex.Message}");
            }
        }
    } 
    // TODO: GET RID OF WEALTH & it's managers/files AND TURN IT INTO STATS!
}