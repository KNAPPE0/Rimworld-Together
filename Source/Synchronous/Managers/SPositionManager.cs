using GameClient;
using GameClient.Misc;
using RimWorld;
using Shared;
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
    public static class SPositionManager
    {
        [OnSynchronousStart]
        private static void SendAllPawnPositions()
        {
            PlayerPosition playerPosition = new PlayerPosition();

            foreach (Pawn pawn in SessionHandler.SynchronousMap.mapPawns.AllPawns)
            {
                if (pawn.Faction != SessionHandler.NeutralFaction)
                {
                    playerPosition.IDs.Add(pawn.ThingID);
                    playerPosition.Positions.Add(Converter.IntVector3ToString(pawn.PositionHeld));
                }
            }

            ClientNetwork.Instance.ClientListener.EnqueuePacket(PacketHeader.SPlayerPosition, playerPosition);
        }

        [HandlesPacket(PacketHeader.SPlayerPosition)]
        private static void Receive(byte[] bytes)
        {
            PlayerPosition data = Serializer.ConvertBytesToObject<PlayerPosition>(bytes);

            PatchHandler.ExecuteInBypass(delegate
            {
                for (int i = 0; i < data.IDs.Count; i++)
                {
                    Map map = SessionHandler.SynchronousMap;
                    Pawn pawn = Finder.GetPawnFromID(map, data.IDs[i]);
                    pawn.SetPositionDirect(Converter.StringToIntVec3(data.Positions[i]));
                }
            });
        }
    }
}
