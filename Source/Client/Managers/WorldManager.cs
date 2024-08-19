using RimWorld;
using Shared;
using static Shared.CommonEnumerators;
using Verse;

namespace GameClient
{
    public static class WorldManager
    {
        // Handles parsing the world packet from the server
        public static void ParseWorldPacket(Packet packet)
        {
            WorldData worldData = Serializer.ConvertBytesToObject<WorldData>(packet.Contents);

            switch (worldData.WorldStepMode)
            {
                case WorldStepMode.Required:
                    OnRequireWorld();
                    break;

                case WorldStepMode.Existing:
                    OnExistingWorld(worldData);
                    break;
                default:
                    Log.Error("Unhandled WorldStepMode in ParseWorldPacket.");
                    break;
            }
        }

        // Called when the server requires the client to generate the world
        public static void OnRequireWorld()
        {
            DialogManager.PopWaitDialog(); // Removes any waiting dialog

            ClientValues.ToggleGenerateWorld(true); // Enables world generation on the client

            // Setting up the scenario selection page and linking it to the starting site selection
            Page scenarioPage = new Page_SelectScenario
            {
                next = new Page_SelectStartingSite()
            };
            DialogManager.PushNewDialog(scenarioPage); // Pushes the new scenario page to the dialog stack

            // Inform the player that they are the first to join and must set up the world
            RT_Dialog_OK_Loop d1 = new RT_Dialog_OK_Loop(new string[]
            {
                "You are the first person joining the server!",
                "Configure the world that everyone will play on"
            });
            DialogManager.PushNewDialog(d1); // Pushes the dialog to notify the player
        }

        // Called when the server sends the existing world data
        public static void OnExistingWorld(WorldData worldData)
        {
            DialogManager.PopWaitDialog(); // Removes any waiting dialog

            // Sets up the world data from the server on the client
            WorldGeneratorManager.SetValuesFromServer(worldData);

            // Navigates to the scenario selection page
            DialogManager.PushNewDialog(new Page_SelectScenario());
        }
    }
}