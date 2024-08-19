using Verse;

namespace GameClient
{
    public static class DialogManager
    {
        // Dialog instances (nullable or initialized to default)
        public static RT_Dialog_Wait? dialogWait;
        public static RT_Dialog_YesNo? dialogYesNo;
        public static RT_Dialog_2Button? dialog2Button;
        public static RT_Dialog_3Button? dialog3Button;

        public static RT_Dialog_OK? dialogOK;
        public static RT_Dialog_OK_Loop? dialogOKLoop;

        public static RT_Dialog_Error? dialogError;
        public static RT_Dialog_Error_Loop? dialogErrorLoop;

        public static RT_Dialog_1Input? dialog1Input;
        public static string dialog1ResultOne = string.Empty;

        public static RT_Dialog_2Input? dialog2Input;
        public static string dialog2ResultOne = string.Empty;
        public static string dialog2ResultTwo = string.Empty;

        public static RT_Dialog_3Input? dialog3Input;
        public static string dialog3ResultOne = string.Empty;
        public static string dialog3ResultTwo = string.Empty;
        public static string dialog3ResultThree = string.Empty;

        public static RT_Dialog_ScrollButtons? dialogScrollButtons;
        public static int selectedScrollButton;

        public static RT_Dialog_TransferMenu? dialogTransferMenu;
        public static RT_Dialog_ItemListing? dialogItemListing;
        public static RT_Dialog_Listing? dialogListing;

        public static RT_Dialog_ListingWithButton? dialogButtonListing;
        public static int dialogButtonListingResultInt;
        public static string dialogButtonListingResultString = string.Empty;

        public static RT_Dialog_MarketListing? dialogMarketListing;
        public static int dialogMarketListingResult;

        // Current and previous dialogs (nullable)
        public static Window? currentDialog;
        public static Window? previousDialog;

        /// <summary>
        /// Pushes a new dialog window to the stack.
        /// </summary>
        /// <param name="window">The window to push.</param>
        public static void PushNewDialog(Window window)
        {
            if (ClientValues.isReadyToPlay || Current.ProgramState == ProgramState.Entry)
            {
                previousDialog = currentDialog;
                currentDialog = window;

                Find.WindowStack.Add(window);
            }
        }

        /// <summary>
        /// Closes the specified dialog window.
        /// </summary>
        /// <param name="window">The window to close.</param>
        public static void PopDialog(Window window)
        {
            window?.Close();
        }

        /// <summary>
        /// Closes the waiting dialog if it is open.
        /// </summary>
        public static void PopWaitDialog()
        {
            dialogWait?.Close();
        }
    }
}