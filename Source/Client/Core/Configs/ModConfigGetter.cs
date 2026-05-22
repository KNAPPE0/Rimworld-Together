using Verse;
using static Shared.Misc.Printer;

namespace GameClient.Core.Configs
{
    public class ModConfigGetter : Verse.ModSettings
    {
        // Persistent settings (survive restart).
        public static bool BypassModCompatibilityCheck;
        public static bool HasSeenKMHWelcome;
        public static string LocalServerDownloadUrl = string.Empty;
        public static LogImportanceMode CurrentVerboseMode;

        // Session-only — reset every restart so the user re-confirms
        // "I accept the risk" after each launch.
        public static bool BypassModCheckThisSession;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref CurrentVerboseMode, nameof(CurrentVerboseMode));
            Scribe_Values.Look(ref BypassModCompatibilityCheck, nameof(BypassModCompatibilityCheck));
            Scribe_Values.Look(ref HasSeenKMHWelcome, nameof(HasSeenKMHWelcome), defaultValue: false);
            Scribe_Values.Look(ref LocalServerDownloadUrl, nameof(LocalServerDownloadUrl), defaultValue: string.Empty);
            base.ExposeData();
        }
    }
}
