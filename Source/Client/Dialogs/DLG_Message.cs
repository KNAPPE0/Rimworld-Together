using System;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs
{
    public class DLG_Message : DLG_Base
    {
        public override Vector2 InitialSize => new Vector2(520f, 220f);

        private string CurrentMessage { get; set; }
        private string[] Messages { get; set; }
        private int Index { get; set; } = 0;
        private Vector2 _msgScroll = Vector2.zero;

        public DLG_Message(string title, string[] messages, Action onConfirm = null)
        {
            Title = string.IsNullOrEmpty(title) ? "Message" : title;
            Messages = messages ?? Array.Empty<string>();
            OnAccept = onConfirm;

            if (Messages.Length == 0)
                Messages = new[] { string.Empty };

            CurrentMessage = Messages[Index];

            closeOnAccept = false;
            closeOnCancel = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = DrawStandardHeader(inRect);
            if (y < 0f) return;

            float footerH = DefaultButtonSize.y + FooterPad * 2f;
            Rect contentOuter = new Rect(0f, y, inRect.width, inRect.height - y - footerH).ContractedBy(ContentPad);

            Widgets.DrawMenuSection(contentOuter);
            Rect inner = contentOuter.ContractedBy(10f);

            Text.Font = GameFont.Small;
            string msg = CurrentMessage ?? string.Empty;

            float msgH = Text.CalcHeight(msg, inner.width);
            Rect viewRect = new Rect(0f, 0f, inner.width - GenUI.ScrollBarWidth, Mathf.Max(msgH, inner.height));

            Widgets.BeginScrollView(inner, ref _msgScroll, viewRect);
            try
            {
                Widgets.Label(new Rect(0f, 0f, viewRect.width, msgH), msg);
            }
            finally
            {
                Widgets.EndScrollView();
            }

            Rect footer = new Rect(0f, inRect.height - footerH, inRect.width, footerH);
            float okW = Mathf.Min(DefaultButtonSize.x, inRect.width - (FooterPad * 2f));
            Rect okBtn = new Rect((inRect.width - okW) / 2f, footer.y + FooterPad, okW, DefaultButtonSize.y);

            if (Widgets.ButtonText(okBtn, "OK"))
            {
                if (Index < Messages.Length - 1)
                {
                    Index++;
                    CurrentMessage = Messages[Index];
                    _msgScroll = Vector2.zero;
                }
                else
                {
                    OnAccept?.Invoke();
                    Close();
                }
            }
        }
    }
}