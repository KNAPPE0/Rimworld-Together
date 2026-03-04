using GameClient.Core.Configs;
using GameClient.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace GameClient.Misc
{
    public static class DiagnosticsHandler
    {
        public static void DrawDiagnosticsUI()
        {
            float defaultMargin = 8.0f;
            float lineHeight = defaultMargin;
            string toDisplay = $"Latency: {Math.Abs(KeepAliveManager.CurrentPing)} ms";

            Vector2 size = Text.CalcSize(toDisplay);
            Vector2 position = new Vector2(UI.screenWidth - size.x - defaultMargin, lineHeight);
            Widgets.Label(new Rect(position, size), toDisplay);
        }
    }
}
