using GameClient.Hooks.TCPNetwork;
using GameClient.Misc;
using Shared;
using Synchronous.Misc;
using Synchronous.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TCPNetwork;
using Verse;

namespace Synchronous.Managers
{
    public static class SDestroyManager
    {
        public static void Ask(Thing thing)
        {
            PlayerDestroy destroy = new PlayerDestroy();
            destroy.ThingID = thing.ThingID;

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.SPlayerDestroy, destroy);
        }

        [HandlesPacket(PacketHeader.SPlayerDestroy)]
        private static void Receive(byte[] bytes)
        {
            PlayerDestroy destroy = Serializer.ConvertBytesToObject<PlayerDestroy>(bytes);

            PatchHandler.ExecuteInBypass(delegate
            {
                Thing thing = Finder.GetThingFromID(SessionHandler.SynchronousMap, destroy.ThingID);
                thing.Destroy();
            });
        }
    }
}
