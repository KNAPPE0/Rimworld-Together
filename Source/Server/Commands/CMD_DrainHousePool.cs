using GameServer.Managers;
using Shared;
using Shared.Misc;

namespace GameServer.Commands
{
    /// <summary>
    /// Admin sink: transfers the marketplace's accumulated tax pool into a
    /// named user's treasury. Use this to redistribute the house cut
    /// (e.g. fund a community event, top up a guild).
    ///
    /// Usage: <c>drainhousepool &lt;username&gt;</c>
    /// </summary>
    public class CMD_DrainHousePool : CMD_Base
    {
        public CMD_DrainHousePool()
        {
            Prefix = "drainhousepool";
            Description = "Drain the marketplace house silver pool into a user's treasury. Usage: drainhousepool <username>";
            ParameterCount = 1;
        }

        public override void Action()
        {
            string toUsername = CMD_Base.CommandParameters[0];
            int drained = MarketplaceManager.DrainHousePool(toUsername);
            if (drained <= 0) Printer.Warning("House pool is empty.");
            else Printer.Title($"Drained {drained} silver from house pool into {toUsername}'s treasury.");
        }
    }
}
