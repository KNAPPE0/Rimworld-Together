using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs
{
    public class RT_Dialog_Base : Window
    {
        public static Window CurrentDialog { get; private set; } = null;
        public static Window PreviousDialog { get; private set; } = null;

        public static Vector2 DefaultButtonSize { get; private set; } = new(250f, 38f);
        public static Vector2 SmallButtonSize { get; private set; } = new(150f, 38f);
        public static Vector2 SmallerButtonSize { get; private set; } = new(137f, 38f);
        public static Vector2 TinyButtonSize { get; private set; } = new(47f, 25f);
        public static Vector2 SlimButtonSize { get; private set; } = new(100f, 38f);
        public static Vector2 LongButtonSize { get; private set; } = new Vector2(100f, 25f);

        protected const float HeaderGap = 6f;
        protected const float ContentPad = 10f;
        protected const float FooterPad = 10f;
        protected const float FooterGap = 8f;

        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public Action OnAccept { get; set; } = null;
        public Action OnCancel { get; set; } = null;

        public bool AcceptsInput => StartAcceptingInputAtFrame <= Time.frameCount;
        public int StartAcceptingInputAtFrame { get; set; }

        public Vector2 ScrollPosition = Vector2.zero;

        public RT_Dialog_Base()
        {
            forcePause = true;
            absorbInputAroundWindow = true;
            soundAppear = SoundDefOf.CommsWindow_Open;

            StartAcceptingInputAtFrame = Time.frameCount + 1;
        }

        public override void DoWindowContents(Rect inRect) { }

        public static void PushNewDialog(Window window)
        {
            PreviousDialog = CurrentDialog;
            Find.WindowStack.Add(window);
            CurrentDialog = window;
        }

        public enum RectLocation
        {
            TopLeft, TopCenter, TopRight,
            MiddleLeft, MiddleCenter, MiddleRight,
            BottomLeft, BottomCenter, BottomRight
        }

        public static Rect GetRectForLocation(Rect origin, Vector2 reference, RectLocation desiredLocation)
        {
            float xMin = origin.xMin;
            float xMax = origin.xMax;
            float yMin = origin.yMin;
            float yMax = origin.yMax;

            float midX = origin.xMin + origin.width / 2f;
            float midY = origin.yMin + origin.height / 2f;

            return desiredLocation switch
            {
                RectLocation.TopLeft => new Rect(new Vector2(xMin, yMin), reference),
                RectLocation.TopCenter => new Rect(new Vector2(midX - reference.x / 2f, yMin), reference),
                RectLocation.TopRight => new Rect(new Vector2(xMax - reference.x, yMin), reference),

                RectLocation.MiddleLeft => new Rect(new Vector2(xMin, midY - reference.y / 2f), reference),
                RectLocation.MiddleCenter => new Rect(new Vector2(midX - reference.x / 2f, midY - reference.y / 2f), reference),
                RectLocation.MiddleRight => new Rect(new Vector2(xMax - reference.x, midY - reference.y / 2f), reference),

                RectLocation.BottomLeft => new Rect(new Vector2(xMin, yMax - reference.y), reference),
                RectLocation.BottomCenter => new Rect(new Vector2(midX - reference.x / 2f, yMax - reference.y), reference),
                RectLocation.BottomRight => new Rect(new Vector2(xMax - reference.x, yMax - reference.y), reference),

                _ => throw new IndexOutOfRangeException()
            };
        }

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

        protected static Vector2 ClampButtonSize(Vector2 desired, float availableWidth, float minWidth = 90f)
        {
            float w = Mathf.Clamp(desired.x, minWidth, availableWidth);
            return new Vector2(w, desired.y);
        }
    }
}