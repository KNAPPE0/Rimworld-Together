using System;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs.Default
{
    // Modal "please wait" dialog with an animated dot ellipsis so the
    // player sees the client isn't hung during multi-second waits.
    public class DLG_Wait : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(360f, 140f);

        public static DLG_Base Instance { get; private set; } = null;

        private string _baseLabel;
        private string _dots = ".";
        private DateTime _lastTickUtc = DateTime.UtcNow;

        private const float TickIntervalSeconds = 0.4f;
        private const int MaxDots = 3;

        public DLG_Wait(string description = null)
        {
            Instance = this;
            Title = "Wait";
            _baseLabel = string.IsNullOrEmpty(description) ? "Waiting" : description.TrimEnd('.', ' ');
            Description = _baseLabel + _dots;

            closeOnAccept = false;
            closeOnCancel = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = DrawStandardHeader(inRect);
            if (y < 0f) return;

            AdvanceAnimation();

            Rect body = new Rect(0f, y, inRect.width, inRect.height - y);
            body = body.ContractedBy(ContentPad);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;

            Widgets.Label(body, Description ?? string.Empty);

            Text.Anchor = TextAnchor.UpperLeft;
        }

        // UTC clock so a day/DST rollover mid-wait doesn't skip the
        // animation tick.
        private void AdvanceAnimation()
        {
            DateTime now = DateTime.UtcNow;
            if ((now - _lastTickUtc).TotalSeconds < TickIntervalSeconds) return;

            _lastTickUtc = now;
            _dots = _dots.Length >= MaxDots ? "." : _dots + ".";
            Description = _baseLabel + _dots;
        }

        // For multi-stage waits — call to update the visible label
        // without restarting the animator.
        public void UpdateDescription(string newDescription)
        {
            if (string.IsNullOrEmpty(newDescription)) return;
            _baseLabel = newDescription.TrimEnd('.', ' ');
            Description = _baseLabel + _dots;
        }
    }
}
