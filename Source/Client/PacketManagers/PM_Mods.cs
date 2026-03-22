using GameClient.Dialogs;
using GameClient.Managers;
using GameClient.Misc;
using Shared;
using Shared.Files;
using Shared.Files.Configs.Mods;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using Verse;
using static Shared.CommonEnumerators;
using static Shared.Files.Configs.Mods.ModsConfigFile;

namespace GameClient.PacketManagers
{
    public class PM_Mods : PM_Base
    {
        [HandlesPacket(PacketHeader.ModManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_ModConfig data = null;

            try
            {
                data = Serializer.ConvertBytesToObject<PKT_ModConfig>(bytes);
            }
            catch (Exception e)
            {
                Printer.Warning($"[Mods] Failed to deserialize mod packet: {e}");
                return;
            }

            if (data == null)
                return;

            if (data._isOptionsProfileChunk || data._noOptionsProfileAvailable)
            {
                OptionsProfileSessionManager.ReceiveOptionsProfilePacket(data);
                return;
            }

            switch (data._stepMode)
            {
                case ModConfigStepMode.Ask:
                    MainThreadHandler.Instance.Enqueue(() =>
                    {
                        HandleAskToOpenModManager();
                    });
                    break;
            }
        }

        private static void HandleAskToOpenModManager()
        {
            SafeCloseWaitDialog();

            if (!SessionHandler.IsAdmin)
            {
                DLG_Base.PushNewDialog(new DLG_Message("Mod Manager", new[]
                {
                    "Admin only.",
                    "Only admins can edit the server mod manager."
                }));
                return;
            }

            OpenModManagerMenu();
        }

        public static void OpenModManagerMenu(bool isFirstEdit = false)
        {
            ModsConfigFile runningMods = ModManagerH.GetRunningModList();
            ModsConfigFile serverConfig = SessionHandler.CurrentModConfig ?? new ModsConfigFile();

            if (serverConfig.ModConfigs == null)
                serverConfig.ModConfigs = new List<ModConfig>();

            List<string> keyList = new List<string>();
            List<int> preselectedIndexes = new List<int>();

            foreach (ModConfig running in runningMods.ModConfigs)
            {
                if (running == null || string.IsNullOrWhiteSpace(running.FileName))
                    continue;

                keyList.Add(running.FileName);

                ModConfig existing = null;
                try
                {
                    existing = serverConfig.ModConfigs.FirstOrDefault(x =>
                        x != null &&
                        string.Equals(
                            NormalizeModName(x.FileName),
                            NormalizeModName(running.FileName),
                            StringComparison.OrdinalIgnoreCase));
                }
                catch
                {
                    existing = null;
                }

                preselectedIndexes.Add(existing != null ? (int)existing.Type : (int)ModType.Optional);
            }

            string[] keys = keyList.ToArray();
            string[] values = new[] { "Required", "Optional", "Forbidden" };
            int[] defaults = preselectedIndexes.ToArray();

            Action toDo = delegate
            {
                GameParameterManager.SendCurrentModConfigs(serverConfig.IsEnforced);

                if (isFirstEdit)
                    GameParameterManager.SetFirstTimeSetup();
            };

            string description = serverConfig.IsEnforced
                ? "Manage mods for the server. This server currently has enforced mod rules."
                : "Manage mods for the server.";

            DLG_ListingWithTuple dialog = new DLG_ListingWithTuple(
                "Mod Manager",
                description,
                keys,
                values,
                defaults,
                toDo);

            DLG_Base.PushNewDialog(dialog);
        }

        public static void ReceiveModConfigs(PKT_ServerGlobalData data)
        {
            SessionHandler.CurrentModConfig = data?._modConfigs ?? new ModsConfigFile();

            if (SessionHandler.CurrentModConfig.ModConfigs == null)
                SessionHandler.CurrentModConfig.ModConfigs = new List<ModConfig>();

            OptionsProfileSessionManager.OnServerEnforcementReceived();

            if (!SessionHandler.CurrentModConfig.IsEnforced)
                return;

            Printer.Warning("Receiving enforced mod configs from server", LogImportanceMode.Verbose);
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

        public static string NormalizeModName(string modName)
        {
            if (string.IsNullOrWhiteSpace(modName))
                return string.Empty;

            return modName.Replace("steam_", "").Trim();
        }
    }

    public class ModManagerH
    {
        public static ModsConfigFile GetRunningModList()
        {
            ModsConfigFile configFile = new ModsConfigFile();

            try
            {
                ModContentPack[] runningMods = LoadedModManager.RunningMods.ToArray();

                foreach (ModContentPack mod in runningMods)
                {
                    if (mod == null || string.IsNullOrWhiteSpace(mod.Name))
                        continue;

                    ModConfig newConfig = new ModConfig
                    {
                        FileName = PM_Mods.NormalizeModName(mod.Name)
                    };

                    configFile.ModConfigs.Add(newConfig);
                }
            }
            catch (Exception e)
            {
                Printer.Warning($"[Mods] Failed to gather running mods: {e}");
            }

            return configFile;
        }

        public static void ShowConflictingModsDialog(PKT_Login data)
        {
            List<string> lines = new List<string>();

            try
            {
                if (data != null && data._extraDetails != null)
                {
                    foreach (string str in data._extraDetails)
                    {
                        if (!string.IsNullOrWhiteSpace(str))
                            lines.Add(str);
                    }
                }
            }
            catch (Exception e)
            {
                Printer.Warning($"[Mods] Failed while reading conflict details: {e}");
            }

            if (lines.Count == 0)
            {
                lines.Add("The server reported a mod conflict, but no details were provided.");
                lines.Add("Try checking required, disallowed, and missing mods before reconnecting.");
            }

            SafeCloseWaitDialog();

            try
            {
                DLG_Base.PushNewDialog(new DLG_Listing(
                    "Mod Conflicts",
                    "Your current mod list does not match the server. Fix the items below, then reconnect.",
                    lines.ToArray()));
            }
            catch (Exception e)
            {
                Printer.Warning($"[Mods] Failed to open conflict listing dialog: {e}");

                DLG_Base.PushNewDialog(new DLG_Message(
                    "Mod Conflicts",
                    lines.ToArray()));
            }
        }

        public static ModsConfigFile SortModsIntoCategories(string[] modNames, int[] categoryIndexes)
        {
            ModsConfigFile configFile = new ModsConfigFile();

            if (modNames == null || categoryIndexes == null)
                return configFile;

            int count = Math.Min(modNames.Length, categoryIndexes.Length);

            for (int i = 0; i < count; i++)
            {
                string name = modNames[i];
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                int rawIndex = categoryIndexes[i];
                if (rawIndex < 0 || rawIndex > (int)ModType.Forbidden)
                    rawIndex = (int)ModType.Optional;

                ModConfig newConfig = new ModConfig
                {
                    FileName = PM_Mods.NormalizeModName(name),
                    Type = (ModType)rawIndex
                };

                configFile.ModConfigs.Add(newConfig);
            }

            return configFile;
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
    }
}