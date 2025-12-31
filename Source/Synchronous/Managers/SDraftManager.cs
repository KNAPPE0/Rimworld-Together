using GameClient;
using GameClient.Core;
using GameClient.Misc;
using RimWorld;
using Shared;
using Shared.Misc;
using Synchronous.Misc;
using Synchronous.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Synchronous.Managers
{
    public static class SDraftManager
    {
        private static List<PlayerDraft> PlayerDrafts { get; set; } = new List<PlayerDraft>();

        [OnSessionStart]
        private static void Initialize() { PlayerDrafts = new List<PlayerDraft>(); }

        [OnUpdate]
        private static void Check()
        {
            if (PlayerDrafts.Count > 0)
            {
                ClientNetwork.Instance.ClientListener.EnqueuePacket(PacketHeader.SPlayerDraft, Serializer.ConvertObjectToBytes(PlayerDrafts));

                PlayerDrafts.Clear();
            }
        }

        public static void Ask(Pawn pawn, bool mode) { PlayerDrafts.Add(new PlayerDraft(pawn.Map.uniqueID, pawn.ThingID, mode)); }

        [HandlesPacket(PacketHeader.SPlayerDraft)]
        private static void Receive(byte[] bytes)
        {
            Printer.Warning(1);

            PlayerDraft[] drafts = Serializer.ConvertBytesToObject<PlayerDraft[]>(bytes);

            PatchHandler.ExecuteInBypass(delegate
            {
                foreach (PlayerDraft playerDraft in drafts)
                {
                    Map map = Finder.GetMapFromID(playerDraft.MapID);
                    Pawn pawn = Finder.GetPawnFromID(map, playerDraft.PawnID);

                    pawn.drafter ??= new Pawn_DraftController(pawn);
                    pawn.drafter.Drafted = playerDraft.DraftValue;
                }
            });
        }
    }
}
