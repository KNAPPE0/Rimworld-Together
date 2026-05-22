using Verse;
using static Shared.Misc.Printer;

namespace GameClient.Core.Configs
{
    public class ModConfigGetter : Verse.ModSettings
    {
        /// <summary>Persistent setting from the mod-options menu — survives restart.</summary>
        public static bool BypassModCompatibilityCheck;

        /// <summary>
        /// KMH 26.5.22.1: Session-only mod-check bypass. Set when the
        /// player clicks "Continue anyway" inside <c>DLG_Compatibility</c>
        /// after the compatibility scan flagged mods. Not scribed, so it
        /// resets on every RimWorld restart — the player has to make the
        /// "I accept the risk" choice each session.
        /// </summary>
        public static bool BypassModCheckThisSession;

        public static LogImportanceMode CurrentVerboseMode;

        /// <summary>
        /// KMH 26.5.22.1: Optional download URL for the "Host Local
        /// Server" feature. KMH ships with this EMPTY by default —
        /// the upstream RWT server doesn't speak KMH's packet
        /// protocol (treasury / marketplace / quest / guild-hall
        /// headers), so accidentally downloading vanilla RWT's release
        /// would produce a broken host.
        ///
        /// <para>The KMH community can publish a server zip at a known
        /// URL (e.g. a KMH GitHub release) and set this field once;
        /// the main-menu "Host Local Server" entry stays hidden until
        /// then. Players who want to self-host can also paste a URL
        /// here manually.</para>
        ///
        /// Format: direct .zip URL — the LocalServerHandler downloads
        /// and unzips it, then looks for <c>GameServer.exe</c> at the
        /// root or one folder deep.
        /// </summary>
        public static string LocalServerDownloadUrl = string.Empty;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref CurrentVerboseMode, nameof(CurrentVerboseMode));
            Scribe_Values.Look(ref BypassModCompatibilityCheck, nameof(BypassModCompatibilityCheck));
            Scribe_Values.Look(ref LocalServerDownloadUrl, nameof(LocalServerDownloadUrl), defaultValue: string.Empty);
            // NOTE: BypassModCheckThisSession is intentionally NOT scribed —
            // it's session-only by design.

            base.ExposeData();
        }
    }
}
