using HarmonyLib;
using RimWorld;
using Shared;
using System.IO;
using System.Reflection;
using Verse;
using static Shared.CommonEnumerators;
using static GameClient.DisconnectionManager;
using System.Xml;
using System.Xml.XPath;

namespace GameClient
{
    public static class SaveManager
    {
        public static string CustomSaveName => $"Server - {Network.ip} - {ClientValues.Username}";
        private static string SaveFilePath => Path.Combine(Master.savesFolderPath, CustomSaveName + ".rws");
        private static string TempSaveFilePath => SaveFilePath + ".mpsave";
        private static string ServerSaveFilePath => SaveFilePath + ".rws.temp";

        public static void ForceSave()
        {
            FieldInfo fticksSinceSave = AccessTools.Field(typeof(Autosaver), "ticksSinceSave");
            fticksSinceSave.SetValue(Current.Game.autosaver, 0);

            ClientValues.autosaveCurrentTicks = 0;

            GameDataSaveLoader.SaveGame(CustomSaveName);
        }

        public static void ReceiveSavePartFromServer(Packet packet)
        {
            var fileTransferData = Serializer.ConvertBytesToObject<FileTransferData>(packet.Contents);

            // If this is the first packet
            if (Network.Listener.DownloadManager == null)
            {
                Logger.Message("Receiving save from server");

                Network.Listener.SetDownloadManager(new DownloadManager());
                Network.Listener.DownloadManager.PrepareDownload(TempSaveFilePath, fileTransferData.FileParts);
            }

            Network.Listener.DownloadManager.WriteFilePart(fileTransferData.FileBytes);

            // If this is the last packet
            if (fileTransferData.IsLastPart)
            {
                Network.Listener.DownloadManager.FinishFileWrite();
                Network.Listener.SetDownloadManager(null);

                var fileBytes = File.ReadAllBytes(TempSaveFilePath);
                fileBytes = GZip.Decompress(fileBytes);

                File.WriteAllBytes(ServerSaveFilePath, fileBytes);
                File.Delete(TempSaveFilePath);

                if (fileTransferData.Instructions != (int)SaveMode.Strict && File.Exists(SaveFilePath))
                {
                    if (GetRealPlayTimeInteractingFromSave(ServerSaveFilePath) >= GetRealPlayTimeInteractingFromSave(SaveFilePath))
                    {
                        Logger.Message("Loading remote save");
                        File.Delete(SaveFilePath);
                        File.Move(ServerSaveFilePath, SaveFilePath);
                    }
                    else
                    {
                        Logger.Message("Loading local save");
                        File.Delete(ServerSaveFilePath);
                    }
                }
                else
                {
                    File.Delete(SaveFilePath);
                    File.Move(ServerSaveFilePath, SaveFilePath);
                }

                GameDataSaveLoader.LoadGame(CustomSaveName);
            }
            else
            {
                var rPacket = Packet.CreatePacketFromObject(nameof(PacketHandler.RequestSavePartPacket));
                Network.Listener.EnqueuePacket(rPacket);
            }
        }

        private static double GetRealPlayTimeInteractingFromSave(string filePath)
        {
            if (!File.Exists(filePath)) return 0;

            try
            {
                var doc = new XmlDocument();
                doc.Load(filePath);
                var nav = doc.CreateNavigator();

                return double.Parse(nav.SelectSingleNode("/savegame/game/info/realPlayTimeInteracting").Value);
            }
            catch
            {
                return 0;
            }
        }

        public static void SendSavePartToServer()
        {
            // If this is the first packet
            if (Network.Listener.UploadManager == null)
            {
                ClientValues.ToggleSendingSaveToServer(true);

                var saveBytes = File.ReadAllBytes(SaveFilePath);
                saveBytes = GZip.Compress(saveBytes);

                File.WriteAllBytes(TempSaveFilePath, saveBytes);
                Network.Listener.SetUploadManager(new UploadManager());
                Network.Listener.UploadManager.PrepareUpload(TempSaveFilePath);
            }

            // Create a new file part packet
            var fileTransferData = new FileTransferData
            {
                FileSize = Network.Listener.UploadManager.FileSize,
                FileParts = Network.Listener.UploadManager.FileParts,
                FileBytes = Network.Listener.UploadManager.ReadFilePart(),
                IsLastPart = Network.Listener.UploadManager.IsLastPart
            };

            // Set the instructions of the packet
            if (isIntentionalDisconnect && (intentionalDisconnectReason == DCReason.SaveQuitToMenu || intentionalDisconnectReason == DCReason.SaveQuitToOS))
            {
                fileTransferData.Instructions = (int)SaveMode.Disconnect;
            }
            else
            {
                fileTransferData.Instructions = (int)SaveMode.Autosave;
            }

            var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.ReceiveSavePartPacket), fileTransferData);
            Network.Listener.EnqueuePacket(packet);

            // If this is the last packet
            if (Network.Listener.UploadManager.IsLastPart)
            {
                ClientValues.ToggleSendingSaveToServer(false);
                Network.Listener.SetUploadManager(null);
                File.Delete(TempSaveFilePath);
            }
        }
    }
}