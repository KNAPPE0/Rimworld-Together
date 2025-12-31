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
    public static class SWeatherManager
    {
        public static void Ask(WeatherDef def)
        {
            ClientNetwork.Instance.ClientListener.EnqueuePacket(PacketHeader.SPlayerWeather, new PlayerWeather(Find.CurrentMap.uniqueID, def.defName));
        }

        [HandlesPacket(PacketHeader.SPlayerWeather)]
        private static void Receive(byte[] bytes)
        {
            PlayerWeather data = Serializer.ConvertBytesToObject<PlayerWeather>(bytes);

            PatchHandler.ExecuteInBypass(delegate
            {
                WeatherDef def = DefDatabase<WeatherDef>.AllDefs.First(fetch => fetch.defName == data.WeatherDefName);
                Finder.GetMapFromID(data.MapID).weatherManager.TransitionTo(def);
            });
        }
    }
}
