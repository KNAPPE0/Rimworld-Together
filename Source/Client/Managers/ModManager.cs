using Shared;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace GameClient
{
    public static class ModManager
    {
        public static string[] GetRunningModList()
        {
            List<string> compactedMods = new List<string>();

            ModContentPack[] runningMods = LoadedModManager.RunningMods.ToArray();
            foreach (ModContentPack mod in runningMods) 
            {
                if (!string.IsNullOrEmpty(mod.PackageId))
                {
                    compactedMods.Add(mod.PackageId);
                }
            }
            return compactedMods.ToArray();
        }

        public static void GetConflictingMods(Packet packet)
        {
            if (packet == null)
            {
                Logger.Warning("Received null packet in GetConflictingMods.");
                return;
            }

            LoginData loginData = Serializer.ConvertBytesToObject<LoginData>(packet.Contents);
            if (loginData == null || loginData.ExtraDetails == null)
            {
                Logger.Warning("Failed to deserialize LoginData or ExtraDetails is null in GetConflictingMods.");
                return;
            }

            DialogManager.PushNewDialog(new RT_Dialog_Listing(
                "Mod Conflicts", 
                "The following mods are conflicting with the server", 
                loginData.ExtraDetails.ToArray()));
        }

        public static bool CheckIfMapHasConflictingMods(MapData mapData)
        {
            if (mapData?.MapMods == null)
            {
                Logger.Warning("MapData or MapMods is null in CheckIfMapHasConflictingMods.");
                return true;
            }

            string[] currentMods = GetRunningModList();

            foreach (string mod in mapData.MapMods)
            {
                if (!currentMods.Contains(mod)) 
                {
                    return true;
                }
            }

            foreach (string mod in currentMods)
            {
                if (!mapData.MapMods.Contains(mod)) 
                {
                    return true;
                }
            }

            return false;
        }
    }
}