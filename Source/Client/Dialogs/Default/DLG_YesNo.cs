using System;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs.Default
{
    public class DLG_YesNo : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(480f, 220f);

        private string YesText { get; set; }
        private string NoText { get; set; }
        private Color YesColor { get; set; }
        private Color NoColor { get; set; }

        private Vector2 _descScroll = Vector2.zero;

        public DLG_YesNo(
            string description,
            Action actionYes,
            Action actionNo = null,
            string yText = "Yes",
            string nText = "No",
            Color? yesColor = null,
            Color? noColor = null)
        {
            Title = "Confirm";
            Description = description;
            OnAccept = actionYes;
            OnCancel = actionNo;

            YesText = string.IsNullOrWhiteSpace(yText) ? "Yes" : yText;
            NoText = string.IsNullOrWhiteSpace(nText) ? "No" : nText;

            YesColor = yesColor ?? Color.white;
            NoColor = noColor ?? Color.white;

            closeOnAccept = false;
            closeOnCancel = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = DrawStandardHeader(inRect);
            if (y < 0f) return;

            float footerH = SmallButtonSize.y + FooterPad * 2f;
            Rect content = new Rect(0f, y, inRect.width, inRect.height - y - footerH).ContractedBy(ContentPad);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            float descH = Text.CalcHeight(Description ?? string.Empty, content.width);
            Rect scrollOuter = content;
            Rect scrollView = new Rect(0f, 0f, scrollOuter.width - GenUI.ScrollBarWidth, Mathf.Max(descH, scrollOuter.height));

            Widgets.BeginScrollView(scrollOuter, ref _descScroll, scrollView);
            try
            {
                Widgets.Label(new Rect(0f, 0f, scrollView.width, descH), Description ?? string.Empty);
            }
            finally
            {
                Widgets.EndScrollView();
            }

            Rect footer = new Rect(0f, inRect.height - footerH, inRect.width, footerH);
            float btnAvailHalf = (footer.width - (FooterPad * 3f)) / 2f;
            Vector2 btnSize = ClampButtonSize(SmallButtonSize, btnAvailHalf);

            Rect leftBtn = new Rect(FooterPad, footer.y + FooterPad, btnSize.x, btnSize.y);
            Rect rightBtn = new Rect(footer.xMax - FooterPad - btnSize.x, footer.y + FooterPad, btnSize.x, btnSize.y);

            GUI.color = YesColor;
            if (Widgets.ButtonText(leftBtn, YesText))
            {
                OnAccept?.Invoke();
                Close();
            }

            GUI.color = NoColor;
            if (Widgets.ButtonText(rightBtn, NoText))
            {
                OnCancel?.Invoke();
                Close();
            }

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}
