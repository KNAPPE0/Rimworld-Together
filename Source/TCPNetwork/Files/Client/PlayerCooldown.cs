using Shared;

namespace TCPNetwork.Files.Client
{
    // Epoch-ms timestamps of each player's last rate-limited action.
    // -1 = no prior action (treated as elapsed, lets fresh players act).
    public class PlayerCooldown
    {
        public double EventProtectionTime { get; set; } = -1;
        public double AidProtectionTime { get; set; } = -1;
        public double RoadProtectionTime { get; set; } = -1;
        public double PollutionProtectionTime { get; set; } = -1;
        public double NPCProtectionTime { get; set; } = -1;

        public void SetEventTimer(double value, UserFile file)
        {
            EventProtectionTime = value;
            file.SaveUserFile();
        }

        public void SetAidTimer(double value, UserFile file)
        {
            AidProtectionTime = value;
            file.SaveUserFile();
        }

        public void SetRoadTimer(double value, UserFile file)
        {
            RoadProtectionTime = value;
            file.SaveUserFile();
        }

        public void SetPollutionTimer(double value, UserFile file)
        {
            PollutionProtectionTime = value;
            file.SaveUserFile();
        }

        public void SetNPCTimer(double value, UserFile file)
        {
            NPCProtectionTime = value;
            file.SaveUserFile();
        }

        public static bool CheckIfCanEvent(UserFile file, bool isEnabled, double baseTimer)
        {
            if (!isEnabled) return false;
            else if (!TimeConverter.CheckForEpochTimer(file.Cooldowns.EventProtectionTime, baseTimer * 1000)) return false;
            else return true;
        }

        public static bool CheckIfCanAid(UserFile file, bool isEnabled, double baseTimer)
        {
            if (!isEnabled) return false;
            else if (!TimeConverter.CheckForEpochTimer(file.Cooldowns.AidProtectionTime, baseTimer * 1000)) return false;
            else return true;
        }

        public static bool CheckIfCanRoad(UserFile file, bool isEnabled, double baseTimer)
        {
            if (!isEnabled) return false;
            else if (!TimeConverter.CheckForEpochTimer(file.Cooldowns.RoadProtectionTime, baseTimer * 1000)) return false;
            else return true;
        }

        public static bool CheckIfCanPollute(UserFile file, bool isEnabled, double baseTimer)
        {
            if (!isEnabled) return false;
            else if (!TimeConverter.CheckForEpochTimer(file.Cooldowns.PollutionProtectionTime, baseTimer * 1000)) return false;
            else return true;
        }

        public static bool CheckIfCanNPC(UserFile file, bool isEnabled, double baseTimer)
        {
            if (!isEnabled) return false;
            else if (!TimeConverter.CheckForEpochTimer(file.Cooldowns.NPCProtectionTime, baseTimer * 1000)) return false;
            else return true;
        }
    }
}
