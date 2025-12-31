using HarmonyLib;
using RimWorld;
using Shared;
using System.IO;
using System.Reflection;
using Verse;
using static Shared.CommonEnumerators;
using static GameClient.Managers.DisconnectionManager;
using System.Xml;
using System.Xml.XPath;
using System;
using GameClient.Core;
using GameClient.Misc;
using System.Collections.Generic;
using GameClient.Dialogs;
using System.Linq;
using TCPNetwork.Packets;
using System.Threading.Tasks;
using System.Threading;
using Shared.Misc;

namespace GameClient.Managers
{
    public static class SaveManager
    {
        public static string LatestSavePath { get; set; } = string.Empty;

        public static string CustomSaveName => $"MP - {ClientNetwork.Ip} - {ClientNetwork.Port} - {SessionHandler.Username}";

        public static string SaveFilePath => Path.Combine(Master.SavesFolderPath, CustomSaveName + ".rws");

        public static string TempSaveFilePath => SaveFilePath + ".rws.temp";

        [HandlesPacket(PacketHeader.SaveManager)]
        private static void ParsePacket(byte[] bytes)
        {
            var read = SaveData.ParseSavePacket(bytes, out SaveDataHeader header);
            var saveFileBytes = bytes.AsSpan().Slice(read);
            if (header._stepMode == SaveStepMode.Receive) OnSaveReceived(header, saveFileBytes.ToArray());
            else if (header._stepMode == SaveStepMode.Send)
            {
                LatestSavePath = SaveFilePath;
                SendSaveToServer();
            }
        }

        public static void ForceSave()
        {
            Printer.Warning("Force saving", LogImportanceMode.Verbose);
            RT_Dialog_Base.PushNewDialog(new RT_Dialog_Wait("Saving your game"));

            Task.Run(delegate
            {
                Thread.Sleep(100);

                MainThreadHandler.Instance.Enqueue(delegate
                {
                    FieldInfo FticksSinceSave = AccessTools.Field(typeof(Autosaver), "ticksSinceSave");
                    FticksSinceSave.SetValue(Current.Game.autosaver, 0);
                    GameDataSaveLoader.SaveGame(CustomSaveName);
                });
            });
        }

        public static void RequestResetSave()
        {
            SaveData data = new SaveData();
            data._header._stepMode = SaveStepMode.Reset;

            ClientNetwork.Instance.ClientListener.EnqueuePacket(PacketHeader.SaveManager, data.SerializeSavePacket());
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
            catch { return 0; }
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
            catch { return 0; }
        }

        public static Dictionary<string, string> GetAllSaveFiles()
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            foreach (string file in Directory.GetFiles(Master.SavesFolderPath))
            {
                if (Path.GetExtension(file) == ".rws")
                    result.Add(Path.GetFileNameWithoutExtension(file), file);
            }
            return result;
        }

        public static void OpenSaveUploaderMenu()
        {
            Dictionary<string, string> saves = SaveManager.GetAllSaveFiles();
            RT_Dialog_ListingWithButton dialog = new RT_Dialog_ListingWithButton("Save uploader",
                "Select a save to upload:",
                saves.Keys.ToArray(),
                delegate
                {
                    RT_Dialog_YesNo D2 = new RT_Dialog_YesNo("This feature is in beta and might fail, are you sure?", delegate
                    {
                        if (saves.TryGetValue(RT_Dialog_ListingWithButton.DialogButtonListingResultString, out string file))
                        {
                            byte[] data = File.ReadAllBytes(file);
                            File.WriteAllBytes(SaveManager.SaveFilePath, data);
                            RT_Dialog_Base.PushNewDialog(new RT_Dialog_Wait("Waiting for save upload"));

                            SaveManager.LatestSavePath = SaveManager.SaveFilePath;
                            SessionHandler.IsExiting = true;
                            SaveManager.SendSaveToServer();
                        }
                    });

                    RT_Dialog_Base.PushNewDialog(D2);
                });

            RT_Dialog_Base.PushNewDialog(dialog);
        }

        public static void SendSaveToServer()
        {
            byte[] saveBytes;
            if (string.IsNullOrEmpty(LatestSavePath)) saveBytes = File.ReadAllBytes(SaveFilePath);
            else saveBytes = File.ReadAllBytes(LatestSavePath);

            SaveData data = new SaveData();
            data._header = new SaveDataHeader(SaveStepMode.Receive, SessionHandler.IsExiting);
            data._fileBytes = GZip.CompressBytes(saveBytes);

            ClientNetwork.Instance.ClientListener.EnqueuePacket(PacketHeader.SaveManager, data.SerializeSavePacket());
        }

        private static void OnSaveReceived(SaveDataHeader header, byte[] save)
        {
            Printer.Message($"Receiving save from server", LogImportanceMode.Verbose);
            var saveBytes = GZip.DecompressBytes(save);
            File.WriteAllBytes(TempSaveFilePath, saveBytes);
            File.Delete(CommonValues.DefaultSaveFormat);

            if (header._forceUseSave || !File.Exists(SaveFilePath))
            {
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

                    File.Delete(SaveManager.SaveFilePath);
                    File.Move(SaveManager.TempSaveFilePath, SaveManager.SaveFilePath);
                }

                else
                {
                    Printer.Message("Loading local save", LogImportanceMode.Verbose);

                    File.Delete(SaveManager.TempSaveFilePath);
                }
            }

            GameDataSaveLoader.LoadGame(SaveManager.CustomSaveName);
        }
    }
}