using GameClient.Dialogs;
using GameClient.Files;
using GameClient.Hooks.TCPNetwork;
using GameClient.Managers;
using GameClient.Misc;
using Shared;
using Shared.Misc;
using System;
using System.Collections.Generic;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using UnityEngine;
using Verse;
using static Shared.CommonEnumerators;

namespace GameClient.PacketManagers
{
    public class PM_Login : PM_Base
    {
        [HandlesPacket(PacketHeader.LoginManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_Login data = null;

            try
            {
                data = Serializer.ConvertBytesToObject<PKT_Login>(bytes);
            }
            catch (Exception e)
            {
                Printer.Warning($"[Login] Failed to deserialize login packet: {e}");
                return;
            }

            if (data == null)
                return;

            MainThreadHandler.Instance.Enqueue(() =>
            {
                SafeCloseWaitDialog();

                switch (data._tryResponse)
                {
                    case LoginResponse.Invalid:
                        DLG_Base.PushNewDialog(new DLG_Message("ERROR", new string[]
                        {
                            "Login details are invalid!",
                            "Please try again or reset your account!"
                        }));
                        break;

                    case LoginResponse.Ban:
                        DLG_Base.PushNewDialog(new DLG_Message("ERROR", new string[]
                        {
                            "You are banned from this server!"
                        }));
                        break;

                    case LoginResponse.Duplicate:
                        DLG_Base.PushNewDialog(new DLG_Message("ERROR", new string[]
                        {
                            "You connected from another place!"
                        }));
                        break;

                    case LoginResponse.Mods:
                        ModManagerH.ShowConflictingModsDialog(data);
                        break;

                    case LoginResponse.Full:
                        DLG_Base.PushNewDialog(new DLG_Message("ERROR", new string[]
                        {
                            "Server is full!"
                        }));
                        break;

                    case LoginResponse.Whitelist:
                        DLG_Base.PushNewDialog(new DLG_Message("ERROR", new string[]
                        {
                            "Server is whitelisted!"
                        }));
                        break;

                    case LoginResponse.Version:
                        DLG_Base.PushNewDialog(new DLG_Message("ERROR", new string[]
                        {
                            data._extraDetails != null && data._extraDetails.Count > 0
                                ? $"Mod version mismatch! Expected version '{data._extraDetails[0]}'"
                                : "Mod version mismatch!"
                        }));
                        break;

                    case LoginResponse.NoWorld:
                        DLG_Base.PushNewDialog(new DLG_Message("ERROR", new string[]
                        {
                            "Server is currently being set up!",
                            "Join again later!"
                        }));
                        break;
                }
            });
        }

        private static void SafeCloseWaitDialog()
        {
            try
            {
                if (DLG_Wait.Instance != null)
                    DLG_Wait.Instance.Close();
            }
            catch
            {
            }
        }

        public static void UseLoginData()
        {
            if (SessionHandler.CurrentNetworkState != ClientNetworkState.Connected)
                return;

            PKT_Login data = new PKT_Login();

            if (Input.GetKey(KeyCode.LeftShift))
            {
                data._username = "Test";
                data._password = "1234";
            }
            else
            {
                PersistentSettings settings = PersistentSettings.Load();
                data._username = settings.UserSettings.Username;
                data._password = settings.UserSettings.Password;
            }

            SessionHandler.Username = data._username;
            data._runningMods = ModManagerH.GetRunningModList();

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.LoginManager, data);
        }

        public static void PromptCreateAccount(bool isQuickConnect)
        {
            Action toDo = delegate
            {
                bool isInvalid = false;

                if (!StringChecker.CheckIfStringValid(DLG_Inputs.DialogInputResults[0]))
                    isInvalid = true;
                else if (!StringChecker.CheckIfStringValid(DLG_Inputs.DialogInputResults[1]))
                    isInvalid = true;

                if (isInvalid)
                {
                    DLG_Base.PushNewDialog(new DLG_Message("ERROR",
                        new string[] { "Your login details contains illegal characters", "Please try again" }));
                }
                else
                {
                    PersistentSettings settings = PersistentSettings.Load();
                    settings.UserSettings.Set(
                        DLG_Inputs.DialogInputResults[0],
                        Hasher.GetHashFromString(DLG_Inputs.DialogInputResults[1]));
                    settings.Save();

                    if (isQuickConnect)
                        QuickConnectUser();
                    else
                        ConnectionManager.ShowConnectDialogs();
                }
            };

            Action toDo2 = delegate
            {
                DLG_Base.PushNewDialog(new DLG_Inputs(
                    "Account Setup",
                    new string[] { "Username", "Password" },
                    new bool[] { false, true },
                    toDo));
            };

            DLG_Base.PushNewDialog(new DLG_Message(
                "Account Setup",
                new string[] { "Please create or log into your account" },
                toDo2));
        }

        public static void QuickConnectUser()
        {
            PersistentSettings settings = PersistentSettings.Load();
            TCPNetwork.Network.Ip = settings.ServerSettings.LatestIP;
            TCPNetwork.Network.Port = settings.ServerSettings.LatestPort;

            if (StringChecker.CheckIfStringValid(TCPNetwork.Network.Ip) &&
                StringChecker.CheckIfStringValid(TCPNetwork.Network.Port.ToString()))
            {
                LoginManagerH.ShowQuickConnectFloatMenu();
            }
            else
            {
                DLG_Base.PushNewDialog(new DLG_Message("ERROR", new string[]
                {
                    "You must join a server first to use this feature!"
                }));
            }
        }
    }

    public class LoginManagerH
    {
        public static bool CheckIfLoginIsValid()
        {
            PersistentSettings settings = PersistentSettings.Load();
            if (!StringChecker.CheckIfStringValid(settings.UserSettings.Username)) return false;
            if (!StringChecker.CheckIfStringValid(settings.UserSettings.Password)) return false;
            return true;
        }

        public static void ShowQuickConnectFloatMenu()
        {
            List<Tuple<string, int>> quickConnectTuples = new List<Tuple<string, int>>()
            {
                Tuple.Create($"Join latest server > {TCPNetwork.Network.Ip}:{TCPNetwork.Network.Port}", 0),
            };

            FloatMenuOption tuple1 = new FloatMenuOption(quickConnectTuples[0].Item1, delegate
            {
                DLG_Base.PushNewDialog(new DLG_Wait("Trying to connect to server"));
                ClientNetwork _ = new ClientNetwork();
            });

            Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>() { tuple1 }));
        }
    }
}