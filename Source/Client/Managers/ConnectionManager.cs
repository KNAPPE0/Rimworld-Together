using System.Linq;
using GameClient.Dialogs;
using GameClient.Hooks.TCPNetwork;
using GameClient.Misc;
using Shared;
using TCPNetwork;

namespace GameClient.Managers
{
    public static class ConnectionManager
    {
        public static void ShowWelcomeDialogs() 
        {
            DLG_Base.PushNewDialog(new DLG_YesNo("Choose a login method:",
                delegate { DLG_Base.PushNewDialog(new DLG_ServerListing()); },
                delegate { ShowConnectDialogs(); },
                "Server Browser",
                "Login"
                ));
        }

        public static void ShowConnectDialogs()
        {
            DLG_Base.PushNewDialog(new DLG_Inputs("Connection Details", new string[] { "IP", "Port" }, new bool[] { false, false },
                delegate { ParseConnectionDetails(); }));
        }

        public static void ParseConnectionDetails()
        {
            bool isInvalid = false;

            if (!StringChecker.CheckIfStringValid(DLG_Inputs.DialogInputResults[0])) isInvalid = true;
            if (!StringChecker.CheckIfStringValid(DLG_Inputs.DialogInputResults[1])) isInvalid = true;
            if (!DLG_Inputs.DialogInputResults[1].All(char.IsDigit)) isInvalid = true;
            if (DLG_Inputs.DialogInputResults[1].Count() > 5) isInvalid = true;

            if (isInvalid) DLG_Base.PushNewDialog(new DLG_Message("ERROR", new string[] { "Server details are invalid! Please try again!" }));
            else
            {
                Network.Ip = DLG_Inputs.DialogInputResults[0];
                Network.Port = int.Parse(DLG_Inputs.DialogInputResults[1]);

                DLG_Base.PushNewDialog(new DLG_Wait("Trying to connect to server"));
                ClientNetwork _ = new ClientNetwork();
            }
        }
    }
}