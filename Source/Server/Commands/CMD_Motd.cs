using GameServer.Core;
using Shared;
using Shared.Files.Configs;
using Shared.Misc;

namespace GameServer.Commands
{
    public class CMD_Motd : CMD_Base
    {
        public CMD_Motd()
        {
            Prefix = "motd";
            Description = "View or set the Message of the Day. Usage: motd [new message]";
            ParameterCount = -1;
        }

        public override void Action()
        {
            if (CMD_Base.CommandParameters == null || CMD_Base.CommandParameters.Length == 0)
            {
                bool enabled = Master.ChatConfig?.EnableMoTD ?? false;
                string msg = Master.ChatConfig?.MessageOfTheDay ?? "(none)";
                string mStatus = enabled ? "ON" : "OFF"; Printer.Title($"MOTD ({mStatus}): {msg}");
                Printer.Title("Usage: motd <message> | motd on | motd off");
                return;
            }

            string arg = CMD_Base.CommandParameters[0].ToLower();

            if (arg == "on")
            {
                Master.ChatConfig.EnableMoTD = true;
                ChatConfigFile.Save(ChatConfigFile.SavePath, Master.ChatConfig);
                Printer.Title("MOTD enabled.");
                return;
            }

            if (arg == "off")
            {
                Master.ChatConfig.EnableMoTD = false;
                ChatConfigFile.Save(ChatConfigFile.SavePath, Master.ChatConfig);
                Printer.Title("MOTD disabled.");
                return;
            }

            // Set new MOTD
            string newMotd = string.Join(" ", CMD_Base.CommandParameters);
            Master.ChatConfig.MessageOfTheDay = newMotd;
            Master.ChatConfig.EnableMoTD = true;
            ChatConfigFile.Save(ChatConfigFile.SavePath, Master.ChatConfig);
            Printer.Title($"MOTD set: {newMotd}");
        }
    }
}
