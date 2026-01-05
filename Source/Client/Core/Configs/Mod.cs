using GameClient.Managers;
using UnityEngine;
using Verse;

namespace GameClient.Core.Configs
{
    public class Mod : Verse.Mod
    {
        public Mod(ModContentPack content) : base(content)
        {
            OptionsProfileSessionManager.Bootstrap();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
        }
    }
}