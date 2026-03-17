using Shared.Files;

namespace TCPNetwork.Packets
{
    public class PKT_Map : PKT_Base
    {
        public int _mapTile { get; set; } = -1;

        public byte[] _rawData { get; set; } = null;

        public MapFile _mapFile { get; set; } = new MapFile();
    }
}