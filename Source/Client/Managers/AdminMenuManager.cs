using GameClient.Dialogs;
using GameClient.PacketManagers;

namespace GameClient.Managers
{
    public static class AdminMenuManager
    {
        private static string DialogTitle { get; set; } = "Admin menu";
         
        private static string DialogDescription { get; set; } = "Choose which action to execute";

        private static string[] MenuButtons { get; set; } = new string[]
        {
            "Mod Manager",
            "Event Manager",
            "Difficulty Manager",
            "Save Manager (BETA)"
        };

        public static void ShowAdminMenu()
        {
            DLG_ScrollButtons d1 = new DLG_ScrollButtons(DialogTitle, DialogDescription,
                MenuButtons, delegate { OpenSpecificMenu(); }, null);

            DLG_Base.PushNewDialog(d1);
        }

        public static void OpenSpecificMenu()
        {
            switch (DLG_ScrollButtons.SelectedScrollButton)
            {
                case 0:
                    PM_Mods.OpenModManagerMenu();
                    break;

                case 1:
                    PM_Events.ShowEventTweakerMenu();
                    break;

                case 2:
                    DifficultyManager.OpenDifficultyMenu();
                    break;

                case 3:
                    PM_Saves.OpenSaveUploaderMenu();
                    break;
            }
        }
    }
}