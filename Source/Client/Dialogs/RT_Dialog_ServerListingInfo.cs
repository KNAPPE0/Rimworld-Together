using GameClient.Hooks.TCPNetwork;
using HarmonyLib;
using System.Reflection;
using TCPNetwork;
using TCPNetwork.Packets.ServerBrowser;
using UnityEngine;
using Verse;

namespace GameClient.Dialogs
{
    public class RT_Dialog_ServerListingInfo : RT_Dialog_Base
    {
        public override Vector2 InitialSize => new Vector2(640f, 320f);

        private ServerInfo ServerInfo { get; set; }

        public RT_Dialog_ServerListingInfo(ServerInfo info)
        {
            ServerInfo = info;
            Title = info?._name ?? "Server Info";

            closeOnAccept = false;
            closeOnCancel = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (ServerInfo == null)
            {
                Close();
                return;
            }

            float y = DrawStandardHeader(inRect, closeX: true);
            if (y < 0f) return;

            float footerH = DefaultButtonSize.y + FooterPad * 2f;

            Rect contentOuter = new Rect(0f, y, inRect.width, inRect.height - y - footerH).ContractedBy(ContentPad);
            Widgets.DrawMenuSection(contentOuter);

            Rect inner = contentOuter.ContractedBy(10f);

            Rect left = new Rect(inner.x, inner.y, inner.width * 0.66f, inner.height);
            Rect right = new Rect(left.xMax + 10f, inner.y, inner.xMax - (left.xMax + 10f), inner.height);

            Text.Font = GameFont.Small;

            string desc = ServerInfo._description ?? string.Empty;
            Widgets.Label(left, desc);

            string pop = $"Population: {ServerInfo._currentPlayerCount}/{ServerInfo._maximumPlayerCount}";
            string ip = $"IP: {ServerInfo._ip}";
            string port = $"Port: {ServerInfo._port}";
            string ver = $"Version: {ServerInfo._version}";

            float lineH = 22f;
            float cy = right.y;

            Widgets.Label(new Rect(right.x, cy, right.width, lineH), pop); cy += lineH;
            Widgets.Label(new Rect(right.x, cy, right.width, lineH), ip); cy += lineH;
            Widgets.Label(new Rect(right.x, cy, right.width, lineH), port); cy += lineH;
            Widgets.Label(new Rect(right.x, cy, right.width, lineH), ver);

            Rect footer = new Rect(0f, inRect.height - footerH, inRect.width, footerH);

            float btnAvailHalf = (footer.width - (FooterPad * 3f)) / 2f;
            Vector2 btnSize = ClampButtonSize(DefaultButtonSize, btnAvailHalf);

            Rect closeBtn = new Rect(FooterPad, footer.y + FooterPad, btnSize.x, btnSize.y);
            Rect connectBtn = new Rect(footer.xMax - FooterPad - btnSize.x, footer.y + FooterPad, btnSize.x, btnSize.y);

            if (Widgets.ButtonText(closeBtn, "Close"))
                Close();

            if (Widgets.ButtonText(connectBtn, "Connect"))
                ConnectToServer();
        }

        private void ConnectToServer()
        {
            Network.Ip = ServerInfo._ip;
            Network.Port = ServerInfo._port;
            RT_Dialog_Base.PushNewDialog(new RT_Dialog_Wait("Trying to connect to server"));
            _ = new ClientNetwork();
            Close();
        }
    }
}