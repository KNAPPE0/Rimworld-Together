using GameServer.Core;
using GameServer.Hooks.TCPNetwork;
using GameServer.Managers;
using Shared;
using Shared.Misc;
using TCPNetwork.Files.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GameServer.Commands
{
    public class CMD_BanIP : CMD_Base
    {
        private static readonly string BannedIPsPath = Path.Combine(Master.ConfigsPath, "BannedIPs.txt");

        public CMD_BanIP()
        {
            Prefix = "banip";
            Description = "Bans an IP address and kicks all connections from it";
            ParameterCount = 1;
        }

        public override void Action()
        {
            string targetIP = CMD_Base.CommandParameters[0];

            List<string> bannedIPs = LoadBannedIPs();

            if (bannedIPs.Contains(targetIP))
            {
                Printer.Warning($"IP '{targetIP}' is already banned");
                return;
            }

            bannedIPs.Add(targetIP);
            SaveBannedIPs(bannedIPs);

            // Also ban any user files associated with this IP
            UserFile[] allUsers = UserManagerH.GetAllUserFiles();
            foreach (UserFile user in allUsers)
            {
                if (user.LatestIP == targetIP && !user.IsBanned)
                {
                    user.UpdateBan(true);
                    Printer.Warning($"Also banned account '{user.Username}' (matched IP)");
                }
            }

            // Kick all currently connected from this IP
            ServerClient[] matches = ServerNetwork.GetConnectedClients()
                .Where(c => c.CurrentIP == targetIP).ToArray();

            foreach (ServerClient client in matches)
            {
                string name = client.UserFile?.Username ?? "(unknown)";
                client.Listener.MarkForDisconnect();
                Printer.Warning($"Kicked '{name}' ({client.CurrentIP})");
            }

            Printer.Title($"Banned IP {targetIP} ({matches.Length} kicked, accounts flagged)");
        }

        public static List<string> LoadBannedIPs()
        {
            try
            {
                if (File.Exists(BannedIPsPath))
                    return File.ReadAllLines(BannedIPsPath)
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .Select(l => l.Trim())
                        .ToList();
            }
            catch (Exception e)
            {
                Printer.Error($"Failed to load banned IPs: {e.Message}");
            }
            return new List<string>();
        }

        public static void SaveBannedIPs(List<string> ips)
        {
            try
            {
                File.WriteAllLines(BannedIPsPath, ips);
            }
            catch (Exception e)
            {
                Printer.Error($"Failed to save banned IPs: {e.Message}");
            }
        }

        public static bool IsIPBanned(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return false;
            return LoadBannedIPs().Contains(ip);
        }
    }
}
