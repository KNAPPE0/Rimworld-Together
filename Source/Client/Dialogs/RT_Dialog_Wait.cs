using UnityEngine;
using Verse;

namespace GameClient.Dialogs
{
    public class RT_Dialog_Wait : RT_Dialog_Base
    {
        public override Vector2 InitialSize => new Vector2(360f, 140f);

        public static RT_Dialog_Base Instance { get; private set; } = null;

        public RT_Dialog_Wait(string description = "[MISSING MESSAGE]")
        {
            Instance = this;
            Title = "Wait";
            Description = description;

            closeOnAccept = false;
            closeOnCancel = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = DrawStandardHeader(inRect);
            if (y < 0f) return;

            Rect body = new Rect(0f, y, inRect.width, inRect.height - y);
            body = body.ContractedBy(ContentPad);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;

            Widgets.Label(body, Description ?? string.Empty);

            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}