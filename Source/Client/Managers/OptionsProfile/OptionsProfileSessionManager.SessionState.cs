using Shared;
using System.Collections.Generic;
using System.IO;

namespace GameClient.Managers
{
    /// <summary>
    /// Persistent session state describing the active enforced profile and the
    /// list of files copied into Config/ for it. Read on Bootstrap, rewritten
    /// after every Apply / Restore.
    /// </summary>
    public static partial class OptionsProfileSessionManager
    {
        private static SessionState State;

        private class SessionState
        {
            public bool IsEnforcedActive { get; set; }
            public string ActiveProfileHash { get; set; } = string.Empty;
            public long ActiveProfileUpdatedUtcTicks { get; set; }
            public long LastAppliedUtcTicks { get; set; }
            public int LastAppliedFileCount { get; set; }
            public List<string> ManagedFiles { get; set; } = new List<string>();
        }

        private static void LoadOrCreateState()
        {
            try
            {
                if (File.Exists(StatePath))
                    State = Serializer.SerializeFromFile<SessionState>(StatePath);
                else
                {
                    State = new SessionState();
                    Serializer.SerializeToFile(StatePath, State);
                }
            }
            catch
            {
                State = new SessionState();
                try { Serializer.SerializeToFile(StatePath, State); } catch { }
            }
        }

        private static void SaveState()
        {
            try
            {
                Serializer.SerializeToFile(StatePath, State);
            }
            catch { }
        }

        private static void ClearSessionState()
        {
            if (State == null)
                State = new SessionState();

            State.IsEnforcedActive = false;
            State.ActiveProfileHash = string.Empty;
            State.ActiveProfileUpdatedUtcTicks = 0;
            State.LastAppliedUtcTicks = 0;
            State.LastAppliedFileCount = 0;
            State.ManagedFiles = new List<string>();

            SaveState();
            EnforcementGuard.ClearMarker();

            try
            {
                if (File.Exists(ActiveMarkerPath))
                    File.Delete(ActiveMarkerPath);
            }
            catch { }
        }
    }
}
