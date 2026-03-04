using System;
using Verse;
using static Shared.CommonEnumerators;

namespace GameClient.Core.Configs
{
    public class ModConfigGetter : Verse.ModSettings
    {
        public static bool MuteChatSoundBool;

        public static bool RejectTransfersBool;

        public static bool RejectSiteRewardsBool;

        public static VerboseMode CurrentVerboseMode;

        public static EnforcedSimulatedLag CurrentSimulatedLag;

        public static bool ShowDiagnosticsBool;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref MuteChatSoundBool, nameof(MuteChatSoundBool));
            Scribe_Values.Look(ref RejectTransfersBool, nameof(RejectTransfersBool));
            Scribe_Values.Look(ref RejectSiteRewardsBool, nameof(RejectSiteRewardsBool));
            Scribe_Values.Look(ref CurrentVerboseMode, nameof(CurrentVerboseMode));
            Scribe_Values.Look(ref CurrentSimulatedLag, nameof(CurrentSimulatedLag));
            Scribe_Values.Look(ref ShowDiagnosticsBool, nameof(ShowDiagnosticsBool));

            base.ExposeData();
        }
    }
}
