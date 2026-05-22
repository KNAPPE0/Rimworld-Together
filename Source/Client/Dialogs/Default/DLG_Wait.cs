using System;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs.Default
{
    /// <summary>
    /// KMH 26.5.22.1: Modal "please wait" dialog with an animated ellipsis.
    ///
    /// <para>The previous implementation showed a static "Waiting…" label
    /// which gave users no visual signal that the client wasn't hung — a
    /// real problem during the longer treasury / map / save round-trips
    /// where the wait can be several seconds. Upstream RWT added a simple
    /// dot-cycle animation (May 2026); we keep KMH's parameterised
    /// constructor + <see cref="DLG_Base.DrawStandardHeader"/> chrome so
    /// the dialog still looks like every other KMH dialog, and layer the
    /// animation on top.</para>
    ///
    /// <para>The animation cycles "." → ".." → "..." → "." every 400 ms.
    /// We use <see cref="DateTime.UtcNow"/> so it survives across day
    /// boundaries (DateTime.Now compares to local clock and would briefly
    /// skip if the user changed timezone or DST kicked in while a
    /// long-running operation was in flight — yes, this matters for
    /// players running 12-hour sessions across the day/night line).</para>
    /// </summary>
    public class DLG_Wait : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(360f, 140f);

        public static DLG_Base Instance { get; private set; } = null;

        // KMH 26.5.22.1: Caller-supplied label text, separate from the
        // animated dot suffix so callers can update Description live
        // without fighting the animator (the animator only edits the
        // dots, never the label).
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

        /// <summary>
        /// KMH 26.5.22.1: Step the dot-cycle if enough time has elapsed.
        /// Safe to call every frame — internally throttled to
        /// <see cref="TickIntervalSeconds"/>.
        /// </summary>
        private void AdvanceAnimation()
        {
            DateTime now = DateTime.UtcNow;
            if ((now - _lastTickUtc).TotalSeconds < TickIntervalSeconds) return;

            _lastTickUtc = now;
            _dots = _dots.Length >= MaxDots ? "." : _dots + ".";
            Description = _baseLabel + _dots;
        }

        /// <summary>
        /// KMH 26.5.22.1: Update the visible label live while keeping the
        /// dot animation rolling. Useful for multi-step waits (e.g.
        /// "Downloading"  →  "Extracting"  →  "Validating") without
        /// closing and re-opening the dialog.
        /// </summary>
        public void UpdateDescription(string newDescription)
        {
            if (string.IsNullOrEmpty(newDescription)) return;
            _baseLabel = newDescription.TrimEnd('.', ' ');
            Description = _baseLabel + _dots;
        }
    }
}
