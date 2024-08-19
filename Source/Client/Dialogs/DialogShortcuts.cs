using System.Linq;
using System;
using Shared;
using Verse;

namespace GameClient
{
    public static class DialogShortcuts
    {
        public static void ShowLoginOrRegisterDialogs()
        {
            var registerDialog = new RT_Dialog_3Input(
                "New User",
                "Username",
                "Password",
                "Confirm Password",
                ParseRegisterUser,
                () => DialogManager.PushNewDialog(DialogManager.dialog2Button),
                false, true, true);

            var loginDialog = new RT_Dialog_2Input(
                "Existing User",
                "Username",
                "Password",
                ParseLoginUser,
                () => DialogManager.PushNewDialog(DialogManager.dialog2Button),
                false, true);

            var selectDialog = new RT_Dialog_2Button(
                "Login Select",
                "Choose your login type",
                "New User",
                "Existing User",
                () => DialogManager.PushNewDialog(registerDialog),
                () =>
                {
                    DialogManager.PushNewDialog(loginDialog);
                    var details = PreferenceManager.LoadLoginData();
                    DialogManager.dialog2Input.inputOneResult = details[0];
                    DialogManager.dialog2Input.inputTwoResult = details[1];
                },
                () =>
                {
                    ClientValues.SetIntentionalDisconnect(true, DisconnectionManager.DCReason.QuitToMenu);
                    Network.Listener.SetDisconnectFlag(true);  // Use a method to set the flag
                });

            DialogManager.PushNewDialog(selectDialog);
        }

        public static void ShowConnectDialogs()
        {
            var connectDialog = new RT_Dialog_2Input(
                "Connection Details", "IP", "Port",
                () => ParseConnectionDetails(false),
                null);

            var details = PreferenceManager.LoadConnectionData();
            DialogManager.dialog2Input.inputOneResult = details[0];
            DialogManager.dialog2Input.inputTwoResult = details[1];

            DialogManager.PushNewDialog(connectDialog);
        }

        public static void ParseConnectionDetails(bool throughBrowser)
        {
            bool isInvalid = false;
            string[] answerSplit = null;

            if (throughBrowser)
            {
                answerSplit = ClientValues.serverBrowserContainer[DialogManager.dialogButtonListingResultInt].Split('|');

                if (string.IsNullOrWhiteSpace(answerSplit[0]) || string.IsNullOrWhiteSpace(answerSplit[1]) ||
                    answerSplit[1].Length > 5 || !answerSplit[1].All(Char.IsDigit))
                {
                    isInvalid = true;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(DialogManager.dialog2ResultOne) || string.IsNullOrWhiteSpace(DialogManager.dialog2ResultTwo) ||
                    DialogManager.dialog2ResultTwo.Length > 5 || !DialogManager.dialog2ResultTwo.All(Char.IsDigit))
                {
                    isInvalid = true;
                }
            }

            if (!isInvalid)
            {
                if (throughBrowser)
                {
                    Network.ip = answerSplit[0];
                    Network.port = answerSplit[1];
                    PreferenceManager.SaveConnectionData(answerSplit[0], answerSplit[1]);
                }
                else
                {
                    Network.ip = DialogManager.dialog2ResultOne;
                    Network.port = DialogManager.dialog2ResultTwo;
                    PreferenceManager.SaveConnectionData(DialogManager.dialog2ResultOne, DialogManager.dialog2ResultTwo);
                }

                DialogManager.PushNewDialog(new RT_Dialog_Wait("Trying to connect to server"));
                Network.StartConnection();
            }
            else
            {
                var errorDialog = new RT_Dialog_Error("Server details are invalid! Please try again!");
                DialogManager.PushNewDialog(errorDialog);
            }
        }

        public static void ParseLoginUser()
        {
            bool isInvalid = false;

            if (string.IsNullOrWhiteSpace(DialogManager.dialog2ResultOne) || DialogManager.dialog2ResultOne.Any(Char.IsWhiteSpace) ||
                string.IsNullOrWhiteSpace(DialogManager.dialog2ResultTwo))
            {
                isInvalid = true;
            }

            if (!isInvalid)
            {
                var loginData = new LoginData
                {
                    Username = DialogManager.dialog2ResultOne,
                    Password = Hasher.GetHashFromString(DialogManager.dialog2ResultTwo),
                    ClientVersion = CommonValues.ExecutableVersion,
                    RunningMods = ModManager.GetRunningModList().ToList()
                };

                ClientValues.Username = loginData.Username;
                PreferenceManager.SaveLoginData(DialogManager.dialog2ResultOne, DialogManager.dialog2ResultTwo);

                var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.LoginClientPacket), loginData);
                Network.Listener.EnqueuePacket(packet);

                DialogManager.PushNewDialog(new RT_Dialog_Wait("Waiting for login response"));
            }
            else
            {
                var errorDialog = new RT_Dialog_Error("Login details are invalid! Please try again!",
                    () => DialogManager.PushNewDialog(DialogManager.previousDialog));

                DialogManager.PushNewDialog(errorDialog);
            }
        }

        public static void ParseRegisterUser()
        {
            bool isInvalid = false;

            if (string.IsNullOrWhiteSpace(DialogManager.dialog3ResultOne) || DialogManager.dialog3ResultOne.Any(Char.IsWhiteSpace) ||
                string.IsNullOrWhiteSpace(DialogManager.dialog3ResultTwo) || string.IsNullOrWhiteSpace(DialogManager.dialog3ResultThree) ||
                DialogManager.dialog3ResultTwo != DialogManager.dialog3ResultThree)
            {
                isInvalid = true;
            }

            if (!isInvalid)
            {
                var loginData = new LoginData
                {
                    Username = DialogManager.dialog3ResultOne,
                    Password = Hasher.GetHashFromString(DialogManager.dialog3ResultTwo),
                    ClientVersion = CommonValues.ExecutableVersion,
                    RunningMods = ModManager.GetRunningModList().ToList()
                };

                ClientValues.Username = loginData.Username;
                PreferenceManager.SaveLoginData(DialogManager.dialog3ResultOne, DialogManager.dialog3ResultTwo);

                var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.RegisterClientPacket), loginData);
                Network.Listener.EnqueuePacket(packet);

                DialogManager.PushNewDialog(new RT_Dialog_Wait("Waiting for register response"));
            }
            else
            {
                var errorDialog = new RT_Dialog_Error("Register details are invalid! Please try again!",
                    () => DialogManager.PushNewDialog(DialogManager.previousDialog));

                DialogManager.PushNewDialog(errorDialog);
            }
        }
    }
}