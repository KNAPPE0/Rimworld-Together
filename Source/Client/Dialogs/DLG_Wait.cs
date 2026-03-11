using UnityEngine;
using Verse;

namespace GameClient.Dialogs
{
    public class DLG_Wait : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(360f, 140f);

        public static DLG_Base Instance { get; private set; } = null;

        public DLG_Wait(string description = null)
        {
            Instance = this;
            Title = "Wait";
            Description = string.IsNullOrEmpty(description) ? "Waiting..." : description;

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