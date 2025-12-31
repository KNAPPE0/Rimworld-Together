using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synchronous.Objects
{
    public class PlayerWeather
    {
        public PlayerWeather(int mapID, string weatherDefName)
        {
            MapID = mapID;
            WeatherDefName = weatherDefName;
        }

        public int MapID { get; set; } = 0;

        public string WeatherDefName { get; set; } = string.Empty;
    }
}
