using GameClient.Dialogs;
using GameClient.Misc;
using GameClient.PacketManagers;
using Verse;
using static RimWorld.PsychicRitualRoleDef;

namespace GameClient.Managers
{
    public static class DisconnectionManager
    {
        public static void HandleDisconnect()
        {
            if (Current.ProgramState != ProgramState.Entry && !SessionHandler.IsExiting)
            {
                DLG_Base.PushNewDialog(new DLG_YesNo("Connection lost. Save game?",
                    delegate { PM_Saves.ForceSave(); DisconnectToMenu(); }, delegate { DisconnectToMenu(); }));
            }
            else DisconnectToMenu();
        }

        public static void DisconnectToMenu()
        {
            DLG_Wait.Instance.Close();

            if (Current.ProgramState != ProgramState.Entry)
            {
                LongEventHandler.QueueLongEvent(delegate { }, "Entry", "", doAsynchronously: false, null);
            }
        }
    }
}
