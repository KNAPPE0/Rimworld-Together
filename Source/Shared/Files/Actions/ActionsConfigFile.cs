using System;
using System.IO;

namespace Shared.Files.Actions
{
    public class ActionsConfigFile : BaseFile
    {
        public static string SavePath { get; set; } = string.Empty;

        public bool EnableFactions { get; set; } = true;

        public bool EnableLeaderboard { get; set; } = true;

        public bool EnableTrading { get; set; } = true;

        public bool EnableCustomScenarios { get; set; } = true;

        public bool EnableNPCDestruction { get; set; } = false;

        public bool EnablePollutionSpread { get; set; } = true;

        public ActivityAction ActivityAction { get; set; } = new ActivityAction();

        public EventAction EventAction { get; set; } = new EventAction();

        public AidAction AidAction { get; set; } = new AidAction();

        public RoadsAction RoadsAction { get; set; } = new RoadsAction();

        public SiteAction SiteAction { get; set; } = new SiteAction();

        public override void Save()
        {
            try { Serializer.SerializeToFile(SavePath, this); }
            catch (Exception e) { throw new Exception(e.ToString()); }
        }

        public static object Load<T>()
        {
            if (File.Exists(SavePath)) return Serializer.SerializeFromFile<T>(SavePath);
            else
            {
                ActionsConfigFile file = new ActionsConfigFile();
                Serializer.SerializeToFile(SavePath, file);
                return file;
            }
        }
    }
}
