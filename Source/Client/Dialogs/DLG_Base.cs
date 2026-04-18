using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs
{
    public class DLG_Base : Window
    {
        public static Window CurrentDialog { get; private set; } = null;

        public static Window PreviousDialog { get; private set; } = null;

        public static Vector2 DefaultButtonSize { get; private set; } = new(250f, 38f);

        public static Vector2 SmallButtonSize { get; private set; } = new(150f, 38f);

        public static Vector2 SmallerButtonSize { get; private set; } = new(137f, 38f);

        public static Vector2 TinyButtonSize { get; private set; } = new(47f, 25f);

        public static Vector2 SlimButtonSize { get; private set; } = new(100f, 38f);

        public static Vector2 LongButtonSize { get; private set; } = new Vector2(100f, 25f);

        public static Vector2 KnobButtonSize { get; private set; } = new Vector2(30f, 30f);

        public static float DefaultMargin { get; private set; } = 8.0f;

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public Action OnAccept { get; set; } = null;

        public Action OnCancel { get; set; } = null;

        public bool AcceptsInput => StartAcceptingInputAtFrame <= Time.frameCount;

        public int StartAcceptingInputAtFrame { get; set; }

        public Vector2 ScrollPosition = Vector2.zero;

        public DLG_Base() 
        { 
            forcePause = true;
            closeOnAccept = false;
            closeOnCancel = false;
            preventCameraMotion = true;
            absorbInputAroundWindow = true;
            soundAppear = SoundDefOf.CommsWindow_Open;
        }

        public override void DoWindowContents(Rect inRect) { }

        public static void PushNewDialog(Window window)
        {
            PreviousDialog = CurrentDialog;
            Find.WindowStack.Add(window);
            CurrentDialog = window;
        }

        public enum RectLocation { TopLeft, TopCenter, TopRight, MiddleLeft, MiddleCenter, MiddleRight, BottomLeft, BottomCenter, BottomRight }

        public static Rect GetRectForLocation(Rect origin, Vector2 reference, RectLocation desiredLocation)
        {
            return desiredLocation switch
            {
                RectLocation.TopLeft => new Rect(new Vector2(origin.xMin, origin.yMin), reference),
                RectLocation.TopCenter => new Rect(new Vector2(origin.width / 2 - reference.x / 2, origin.yMin), reference),
                RectLocation.TopRight => new Rect(new Vector2(origin.xMax - reference.x, origin.yMin), reference),
                RectLocation.MiddleLeft => new Rect(new Vector2(origin.xMin, origin.height / 2 - reference.y / 2), reference),
                RectLocation.MiddleCenter => new Rect(new Vector2(origin.width / 2 - reference.x / 2, origin.height / 2 - reference.y / 2), reference),
                RectLocation.MiddleRight => new Rect(new Vector2(origin.xMax - reference.x, origin.height / 2 - reference.y / 2), reference),
                RectLocation.BottomLeft => new Rect(new Vector2(origin.xMin, origin.yMax - reference.y), reference),
                RectLocation.BottomCenter => new Rect(new Vector2(origin.width / 2 - reference.x / 2, origin.yMax - reference.y), reference),
                RectLocation.BottomRight => new Rect(new Vector2(origin.xMax - reference.x, origin.yMax - reference.y), reference),
                _ => throw new IndexOutOfRangeException()
            };
        }

        public static float GetRectMiddle(Rect rect) { return rect.width / 2; }

        // KMH UI helper constants
        protected const float HeaderGap = 6f;
        protected const float ContentPad = 10f;
        protected const float FooterPad = 10f;
        protected const float FooterGap = 8f;

        // KMH UI helper: draws a standard title header and returns the Y offset below it
        protected float DrawStandardHeader(Rect inRect, string titleOverride = null, bool drawTopBorder = false, bool drawBottomBorder = false, bool closeX = false)
        {
            if (drawTopBorder)
                Widgets.DrawLineHorizontal(inRect.x, inRect.y - 1f, inRect.width);

            if (drawBottomBorder)
                Widgets.DrawLineHorizontal(inRect.x, inRect.yMax + 1f, inRect.width);

            if (closeX && Widgets.CloseButtonFor(inRect))
            {
                Close();
                return -1f;
            }

            string title = string.IsNullOrWhiteSpace(titleOverride) ? (Title ?? string.Empty) : titleOverride;

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;

            float titleH = Mathf.Max(30f, Text.CalcHeight(title, inRect.width));
            Rect titleRect = new Rect(inRect.x, inRect.y, inRect.width, titleH);
            Widgets.Label(titleRect, title);

            float y = titleRect.yMax + HeaderGap;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            Widgets.DrawLineHorizontal(inRect.x, y - 2f, inRect.width);
            y += HeaderGap;

            return y - inRect.yMin;
        }

        // KMH UI helper: clamps button size to available width
        protected static Vector2 ClampButtonSize(Vector2 desired, float availableWidth, float minWidth = 90f)
        {
            float w = Mathf.Clamp(desired.x, minWidth, availableWidth);
            return new Vector2(w, desired.y);
        }
    }
}
