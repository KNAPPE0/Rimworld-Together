using GameClient.Dialogs;
using RimWorld;
using RimWorld.Planet;
using Shared;
using Shared.Files;
using System;
using System.Reflection;
using Verse;
using static Shared.CommonEnumerators;
using static UnityEngine.GraphicsBuffer;

namespace GameClient.Managers
{
    public static class DifficultyManager
    {
        public static void OpenDifficultyManagerMenu()
        {
            string description = "Do you want to enforce the current difficulty?";
            Action actionYes = delegate
            {
                GameParameterManager.SendCurrentStoryteller(true);
                GameParameterManager.SendCurrentDifficulty(true);
            };

            DLG_Base.PushNewDialog(new DLG_YesNo(description, actionYes));
        }
    }
}
