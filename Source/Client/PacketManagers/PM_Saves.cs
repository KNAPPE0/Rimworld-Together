using GameClient.Core;
using GameClient.Dialogs;
using GameClient.Hooks.TCPNetwork;
using GameClient.Misc;
using HarmonyLib;
using RimWorld;
using Shared;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using Verse;

namespace GameClient.PacketManagers
{
    public class PM_Saves : PM_Base
    {
        public static string LatestSavePath { get; set; } = string.Empty;

        public static string CustomSaveName => $"MP - {Network.Ip} - {Network.Port} - {SessionHandler.Username}";

        public static string SaveFilePath => Path.Combine(Master.SavesFolderPath, CustomSaveName + ".rws");

        public static string TempSaveFilePath => SaveFilePath + ".rws.temp";

        [HandlesPacket(PacketHeader.SaveManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_Save data = Serializer.ConvertBytesToObject<PKT_Save>(bytes);

            switch (data._stepMode)
            {
                case SaveStepMode.Receive:
                    OnSaveReceived(data);
                    break;
            }
        }

        public static void ForceSave()
        {
            Printer.Warning("Force saving", LogImportanceMode.Verbose);
            DLG_Base.PushNewDialog(new DLG_Wait("Saving your game"));

            Task.Run(delegate
            {
                Thread.Sleep(100);

                MainThreadHandler.Instance.Enqueue(delegate
                {
                    ResetAutosaveTicks();
                    GameDataSaveLoader.SaveGame(CustomSaveName);
                });
            });
        }

        private static void ResetAutosaveTicks()
        {
            FieldInfo fTicksSinceSave = AccessTools.Field(typeof(Autosaver), "ticksSinceSave");
            if (fTicksSinceSave != null && Current.Game != null && Current.Game.autosaver != null)
                fTicksSinceSave.SetValue(Current.Game.autosaver, 0);
        }

        public static void RequestResetSave()
        {
            PKT_Save data = new PKT_Save();
            data._stepMode = SaveStepMode.Reset;

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.SaveManager, data);
        }

        public static double GetRealPlayTimeInteractingFromSave(string filePath)
        {
            if (!File.Exists(filePath)) return 0;

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(filePath);
                XPathNavigator nav = doc.CreateNavigator();

                XPathNavigator node = nav.SelectSingleNode("/savegame/game/info/realPlayTimeInteracting");
                if (node == null) return 0;

                return double.Parse(node.Value);
            }
            catch
            {
                return 0;
            }
        }

        public static double GetRealPlayTimeFromSave(string filePath)
        {
            if (!File.Exists(filePath)) return 0;

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(filePath);
                XPathNavigator nav = doc.CreateNavigator();

                XPathNavigator node = nav.SelectSingleNode("/savegame/game/info/realPlayTime");
                if (node == null) return 0;

                return double.Parse(node.Value);
            }
            catch
            {
                return 0;
            }
        }

        public static Dictionary<string, string> GetAllSaveFiles()
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            foreach (string str in Directory.GetFiles(Master.SavesFolderPath))
            {
                if (Path.GetExtension(str) == ".rws")
                    result.Add(Path.GetFileNameWithoutExtension(str), str);
            }

            return result;
        }

        public static void OpenSaveUploaderMenu()
        {
            Dictionary<string, string> saves = PM_Saves.GetAllSaveFiles();

            DLG_ListingWithButton dialog = new DLG_ListingWithButton(
                "Save uploader",
                "Select a save to upload:",
                saves.Keys.ToArray(),
                delegate
                {
                    DLG_YesNo d2 = new DLG_YesNo("This feature is in beta and might fail, are you sure?", delegate
                    {
                        if (saves.TryGetValue(DLG_ListingWithButton.DialogButtonListingResultString, out string file))
                        {
                            byte[] data = File.ReadAllBytes(file);
                            File.WriteAllBytes(PM_Saves.SaveFilePath, data);
                            DLG_Base.PushNewDialog(new DLG_Wait("Waiting for save upload"));

                            PM_Saves.LatestSavePath = PM_Saves.SaveFilePath;
                            SessionHandler.IsExiting = true;
                            PM_Saves.SendSaveToServer();
                        }
                    });

                    DLG_Base.PushNewDialog(d2);
                });

            DLG_Base.PushNewDialog(dialog);
        }

        public static void SendSaveToServer()
        {
            Printer.Message("Sending save to server", LogImportanceMode.Verbose);

            byte[] saveBytes;
            if (string.IsNullOrEmpty(LatestSavePath))
                saveBytes = File.ReadAllBytes(SaveFilePath);
            else
                saveBytes = File.ReadAllBytes(LatestSavePath);

            PKT_Save data = new PKT_Save();
            data._stepMode = SaveStepMode.Receive;
            data._forceDisconnect = SessionHandler.IsExiting;
            data._fileBytes = GZip.CompressBytes(saveBytes);

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.SaveManager, data);
        }

        private static void OnSaveReceived(PKT_Save data)
        {
            Printer.Message("Receiving save from server", LogImportanceMode.Verbose);

            byte[] saveBytes = GZip.DecompressBytes(data._fileBytes);
            File.WriteAllBytes(TempSaveFilePath, saveBytes);

            if (data._forceUseSave || !File.Exists(SaveFilePath))
            {
                if (File.Exists(SaveFilePath))
                    File.Delete(SaveFilePath);

                File.Move(TempSaveFilePath, SaveFilePath);
            }
            else
            {
                double remoteTotal = GetRealPlayTimeFromSave(TempSaveFilePath);
                double localTotal = GetRealPlayTimeFromSave(SaveFilePath);

                if (remoteTotal <= 0 || localTotal <= 0)
                {
                    remoteTotal = GetRealPlayTimeInteractingFromSave(TempSaveFilePath);
                    localTotal = GetRealPlayTimeInteractingFromSave(SaveFilePath);
                }

                if (remoteTotal >= localTotal)
                {
                    Printer.Message("Loading remote save", LogImportanceMode.Verbose);

                    File.Delete(PM_Saves.SaveFilePath);
                    File.Move(PM_Saves.TempSaveFilePath, PM_Saves.SaveFilePath);
                }
                else
                {
                    Printer.Message("Loading local save", LogImportanceMode.Verbose);

                    File.Delete(PM_Saves.TempSaveFilePath);
                }
            }

            GameDataSaveLoader.LoadGame(PM_Saves.CustomSaveName);
        }
    }
}