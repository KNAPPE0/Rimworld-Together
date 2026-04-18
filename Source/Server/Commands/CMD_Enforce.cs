using GameServer.Core;
using GameServer.Hooks.TCPNetwork;
using GameServer.Managers;
using Shared;
using Shared.Files.Configs.Mods;
using Shared.Misc;
using TCPNetwork.Files.Client;

namespace GameServer.Commands
{
    public class CMD_Enforce : CMD_Base
    {
        public CMD_Enforce()
        {
            Prefix = "enforce";
            Description = "Toggles config profile enforcement on/off, or shows status";
            ParameterCount = -1;
        }

        public override void Action()
        {
            string arg = CMD_Base.CommandParameters != null && CMD_Base.CommandParameters.Length > 0
                ? CMD_Base.CommandParameters[0].ToLower() : "";

            if (arg == "on" || arg == "enable")
            {
                if (OptionsProfileManager.CurrentProfile == null || !OptionsProfileManager.CurrentProfile.HasProfile)
                {
                    Printer.Warning("No profile published yet. An admin must publish a config profile first.");
                    return;
                }

                Master.ModConfig.IsEnforced = true;
                ModConfigFile.Save(ModConfigFile.SavePath, Master.ModConfig);
                Printer.Title("Config enforcement ENABLED. Joining players will receive the server profile.");

                // Push to all connected clients
                foreach (ServerClient sc in ServerNetwork.GetConnectedClients())
                {
                    try { OptionsProfileManager.TryPushProfile(sc); } catch { }
                }
            }
            else if (arg == "off" || arg == "disable")
            {
                Master.ModConfig.IsEnforced = false;
                ModConfigFile.Save(ModConfigFile.SavePath, Master.ModConfig);
                Printer.Title("Config enforcement DISABLED.");
            }
            else
            {
                bool enforced = Master.ModConfig?.IsEnforced ?? false;
                bool hasProfile = OptionsProfileManager.CurrentProfile?.HasProfile ?? false;
                string hash = OptionsProfileManager.CurrentProfile?.ProfileHash ?? "none";

                Printer.Title("=== Config Enforcement Status ===");
                string eStatus = enforced ? "ON" : "OFF"; Printer.Warning($"Enforcement: {eStatus}");
                string pStatus = hasProfile ? "YES" : "NO"; Printer.Warning($"Profile available: {pStatus}");
                Printer.Warning($"Profile hash: {hash}");
                Printer.Title("Use 'enforce on' or 'enforce off' to toggle.");
            }
        }
    }
}
