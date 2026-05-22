using Shared;

namespace TCPNetwork.Files.Client
{
    /// <summary>
    /// Per-player cooldown timestamps for rate-limited server actions.
    ///
    /// <para>Each <c>*ProtectionTime</c> field is the epoch (ms) when the
    /// last action of that kind completed; the corresponding
    /// <c>CheckIfCan*</c> static checks the elapsed time against the
    /// configured base timer. <c>-1</c> means "no prior action" — the
    /// epoch comparator treats this as "cooldown elapsed", so a fresh
    /// player can act immediately.</para>
    ///
    /// <para>KMH 26.5.22.1: Ported from upstream RWT (May 2026) — added
    /// Road, Pollution, and NPC cooldowns to mirror upstream's
    /// "Security checks for sites, settlements, roads and pollution"
    /// (May 9 commit). Kept KMH's epoch-double convention rather than
    /// upstream's DateTime — DateTime serialises to a JSON string and
    /// inflates UserFile size; epoch ms is 8 bytes and matches every
    /// other timer in the file.</para>
    /// </summary>
    public class PlayerCooldown
    {
        public double EventProtectionTime { get; set; } = -1;

        public double AidProtectionTime { get; set; } = -1;

        // KMH 26.5.22.1: Three new cooldowns ported from upstream so the
        // server can throttle high-frequency abuse vectors (clients
        // spamming road requests, pollution toggles, or NPC events).
        // Defaulting to -1 keeps existing UserFiles deserialisable —
        // older files without these fields land with the default and
        // every check immediately allows the first action.
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
