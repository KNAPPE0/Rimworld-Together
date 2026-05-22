using GameServer.PacketManager;
using Shared;

namespace GameServer.Commands.Chat
{
    public class CMD_Help : CMD_Base
    {
        public CMD_Help()
        {
            Prefix = "/help";
            Description = "List every chat command you can use here (and admin commands too if you're an admin)";
            IsChatCommand = true;
        }

        public override void Action()
        {
            if (PM_Chat.TargetClient == null) return;

            // Always list chat commands available to everyone.
            // For admins, ALSO list the server-console admin commands so they
            // can see what's available even though those are run from the
            // server console, not from chat.
            List<string> lines = new List<string>();
            lines.Add("=== Chat commands (everyone) ===");

            List<CMD_Base> chat = new List<CMD_Base>(CMD_Base.ChatCommands);
            chat.Sort((a, b) => string.Compare(a?.Prefix, b?.Prefix, System.StringComparison.OrdinalIgnoreCase));
            foreach (CMD_Base command in chat)
            {
                if (command == null || string.IsNullOrEmpty(command.Prefix)) continue;
                lines.Add($"{command.Prefix}  —  {command.Description}");
            }

            bool isAdmin = PM_Chat.TargetClient.UserFile?.IsAdmin == true;
            if (isAdmin)
            {
                lines.Add(string.Empty);
                lines.Add("=== Admin commands (server console — you can run these because you're an admin) ===");

                List<CMD_Base> admin = new List<CMD_Base>(CMD_Base.Commands);
                admin.Sort((a, b) =>
                {
                    int catCmp = string.Compare(a?.ResolvedCategory, b?.ResolvedCategory, System.StringComparison.OrdinalIgnoreCase);
                    if (catCmp != 0) return catCmp;
                    return string.Compare(a?.Prefix, b?.Prefix, System.StringComparison.OrdinalIgnoreCase);
                });
                string lastCategory = string.Empty;
                foreach (CMD_Base command in admin)
                {
                    if (command == null || string.IsNullOrEmpty(command.Prefix)) continue;
                    string cat = command.ResolvedCategory ?? "Server";
                    if (cat != lastCategory)
                    {
                        lines.Add(string.Empty);
                        lines.Add($"-- {cat} --");
                        lastCategory = cat;
                    }
                    lines.Add($"{command.Prefix}  —  {command.Description}");
                }
            }

            foreach (string str in lines)
                PM_Chat.SendConsoleMessage(PM_Chat.TargetClient, str);
        }
    }
}
