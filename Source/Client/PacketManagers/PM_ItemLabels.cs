using GameClient.Misc;
using RimWorld;
using Shared;
using Shared.Misc;
using System;
using System.Collections.Generic;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;
using Verse;
using static Shared.Misc.Printer;

namespace GameClient.PacketManagers
{
    /// <summary>
    /// Pushes a snapshot of (defName → label) for every loaded
    /// <c>ThingDef</c> to the server right after the session starts. Mods
    /// the server doesn't have local data for (Vanilla Expanded, etc.) get
    /// their friendly names so Discord output and the leaderboard render
    /// "Plasteel" / "Megasloth Wool" instead of the raw defName.
    ///
    /// Server-only handler is mute; this PM exists on both sides because
    /// PM_Base reflection caches by <see cref="HandlesPacket"/> attributes.
    /// </summary>
    public class PM_ItemLabels : PM_Base
    {
        [HandlesPacket(PacketHeader.ItemLabelManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            // Client-side: server never sends to us. Drop silently.
        }

        [OnSessionStart]
        private static void PushLabelsAfterSessionStart()
        {
            // The session-start hook fires before the marketplace dialog
            // could be opened, so the cache is warm when it matters.
            try
            {
                MainThreadHandler.Instance.Enqueue(SendSnapshot);
            }
            catch (Exception e) { Printer.Warning($"[ItemLabels] Push schedule failed: {e}"); }
        }

        private static void SendSnapshot()
        {
            try
            {
                var defs = DefDatabase<ThingDef>.AllDefsListForReading;
                if (defs == null) return;

                Dictionary<string, string> labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (ThingDef d in defs)
                {
                    if (d == null) continue;
                    if (d.category != ThingCategory.Item) continue;
                    if (string.IsNullOrEmpty(d.defName) || string.IsNullOrEmpty(d.label)) continue;
                    // Capitalise to match the in-game convention.
                    labels[d.defName] = d.label.CapitalizeFirst();
                }

                if (labels.Count == 0) return;

                Network.ServerEndpoint?.EnqueuePacket(PacketHeader.ItemLabelManager,
                    new PKT_ItemLabels { LabelsByDefName = labels });
            }
            catch (Exception e) { Printer.Warning($"[ItemLabels] Snapshot send failed: {e}"); }
        }
    }
}
