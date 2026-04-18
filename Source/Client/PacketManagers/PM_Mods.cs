using GameClient.Dialogs;
using GameClient.Managers;
using GameClient.Misc;
using Shared;
using Shared.Files.Configs.Mods;
using Shared.Misc;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;
using Verse;
using static Shared.Files.Configs.Mods.ModConfigFile;
using static Shared.Misc.Printer;
using static TCPNetwork.Packets.PKT_ModConfig;

namespace GameClient.PacketManagers
{
    public class PM_Mods : PM_Base
    {
        [HandlesPacket(PacketHeader.ModManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_ModConfig data = Serializer.ConvertBytesToObject<PKT_ModConfig>(bytes);

            // KMH: Route options profile chunks and responses
            if (data._isOptionsProfileChunk || data._noOptionsProfileAvailable)
            {
                OptionsProfileSessionManager.ReceiveOptionsProfilePacket(data);
                return;
            }

            switch (data._stepMode)
            {
                case ModConfigStepMode.Send:
                    SetValues(data._configFile.ModConfigs);
                    // KMH: Update enforcement state
                    if (data._configFile != null)
                    {
                        SessionHandler.CurrentModConfig = data._configFile;
                        if (data._configFile.IsEnforced)
                        {
                            Shared.Misc.Printer.Warning("[PM_Mods] Server enforcement is ENABLED - requesting profile");
                            OptionsProfileSessionManager.OnServerEnforcementReceived();
                        }
                    }
                    break;
            }
        }

        public static void OpenModManagerMenu() 
        { 
            if (SessionHandler.CurrentMods.Count > 0) DLG_Base.PushNewDialog(new DLG_ModConfig(SessionHandler.CurrentMods));
            else DLG_Base.PushNewDialog(new DLG_ModConfig(ModManagerH.GetRunningModList().ModConfigs));
        }

        public static void SetValues(List<ModConfig> mods) { SessionHandler.CurrentMods = mods; }
    }

    public class ModManagerH
    {
        public static ModConfigFile GetRunningModList()
        {
            ModConfigFile configFile = new ModConfigFile();

            ModContentPack[] runningMods = LoadedModManager.RunningMods.ToArray();
            foreach (ModContentPack mod in runningMods)
            {
                ModConfig newConfig = new ModConfig();
                newConfig.FileName = mod.Name.Replace("steam_", "");
                newConfig.Type = ModType.Required;

                configFile.ModConfigs.Add(newConfig);
            }

            return configFile;
        }

        public static void GetConflictingMods(PKT_Login data) { DLG_Base.PushNewDialog(new DLG_ModRejection(data._extraDetails)); }

        public static ModConfigFile SortModsIntoCategories(List<ModConfig> mods, List<int> categoryIndexes)
        {
            ModConfigFile configFile = new ModConfigFile();

            for (int i = 0; i < mods.Count; i++)
            {
                ModConfig newConfig = new ModConfig();
                newConfig.FileName = mods[i].FileName.Replace("steam_", "");
                newConfig.Type = (ModType)categoryIndexes[i];

                configFile.ModConfigs.Add(newConfig);
            }

            return configFile;
        }
    }
}
