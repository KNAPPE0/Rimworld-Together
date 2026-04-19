using Shared;
using Shared.Files;
using Shared.Files.Synchronous;

namespace TCPNetwork.Packets
{
    public class PKT_Synchronous : PKT_Base
    {
        public enum Type { Visit, Raid }

        public enum StepMode { Ask, Accept, Reject, Start, Action }

        public StepMode CurrentStepMode { get; set; } = StepMode.Ask;

        public Type CurrentType { get; set; } = Type.Visit;

        public ActionType CurrentActionType { get; set; } = ActionType.SPlayerDraft;

        public int FromTile { get; set; } = -1;

        public int ToTile { get; set; } = -1;

        public string Username { get; set; } = string.Empty;

        public PartyFile Party { get; set; } = null;

        public enum ActionType
        {
            SPlayerDraft,
            SPlayerWeather,
            SPlayerMentalState,
            SPlayerGameSpeed,
            SPlayerJob,
            SPlayerHediff,
            SPlayerDestroy
        }
    }
}