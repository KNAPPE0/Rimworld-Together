using GameClient.Dialogs;
using GameClient.Managers;
using GameClient.Misc;
using Shared;
using Shared.Files.Configs.Mods;
using Shared.Misc;
using System.Collections.Generic;
using System.Linq;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;
using Verse;
using static Shared.Files.Configs.Mods.ModConfigFile;
using static TCPNetwork.Packets.PKT_ModConfig;

namespace GameClient.PacketManagers
{
    public class PM_Mods : PM_Base
    {
        [HandlesPacket(PacketHeader.ModManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_ModConfig data = Serializer.ConvertBytesToObject<PKT_ModConfig>(bytes);
            if (data == null) return;

            if (data._optionsProfileRequiredForJoin)
            {
                SessionHandler.CurrentModConfig = data._configFile;
                SetValues(data._configFile?.ModConfigs);

                OptionsProfileSessionManager.HandleJoinRequirement(data);
                return;
            }

            if (data._isOptionsProfileChunk || data._noOptionsProfileAvailable)
            {
                OptionsProfileSessionManager.ReceiveOptionsProfilePacket(data);
                return;
            }

            if (data._stepMode == ModConfigStepMode.Send && data._configFile != null)
            {
                SessionHandler.CurrentModConfig = data._configFile;
                SetValues(data._configFile.ModConfigs);

                bool serverEnforced = data._configFile.IsEnforced;
                bool localEnforced = OptionsProfileSessionManager.IsEnforcementActive();
                string localHash = OptionsProfileSessionManager.GetActiveHash();

                if (serverEnforced)
                {
                    Printer.Warning($"[PM_Mods] Server enforcement is ON. LocalActive={localEnforced} LocalHash={localHash}");

                    if (!localEnforced)
                        OptionsProfileSessionManager.RequestServerOptionsProfile(isManual: false);
                }
            }
        }

        public static void OpenModManagerMenu()
        {
            if (SessionHandler.CurrentMods != null && SessionHandler.CurrentMods.Count > 0)
                DLG_Base.PushNewDialog(new DLG_ModConfig(SessionHandler.CurrentMods));
            else
                DLG_Base.PushNewDialog(new DLG_ModConfig(ModManagerH.GetRunningModList().ModConfigs));
        }

        public static void SetValues(List<ModConfig> mods)
        {
            SessionHandler.CurrentMods = mods;
        }
    }

    public class ModManagerH
    {
        public static ModConfigFile GetRunningModList()
        {
            ModConfigFile configFile = new ModConfigFile();

            ModContentPack[] runningMods = LoadedModManager.RunningMods.ToArray();
            foreach (ModContentPack mod in runningMods)
            {
                ModConfig newConfig = new ModConfig
                {
                    FileName = mod.Name.Replace("steam_", ""),
                    Type = ModType.Required
                };

                configFile.ModConfigs.Add(newConfig);
            }

            return configFile;
        }

        public static void GetConflictingMods(PKT_Login data)
        {
            DLG_Base.PushNewDialog(new DLG_ModRejection(data._extraDetails));
        }

        public static ModConfigFile SortModsIntoCategories(List<ModConfig> mods, List<int> categoryIndexes)
        {
            ModConfigFile configFile = new ModConfigFile();

            for (int i = 0; i < mods.Count; i++)
            {
                ModConfig newConfig = new ModConfig
                {
                    FileName = mods[i].FileName.Replace("steam_", ""),
                    Type = (ModType)categoryIndexes[i]
                };

                configFile.ModConfigs.Add(newConfig);
            }

            return configFile;
        }
    }
}