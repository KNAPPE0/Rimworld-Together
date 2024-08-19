using System;

namespace Shared
{
    [Serializable]
    public class Packet
    {
        public string Header { get; private set; }
        public byte[] Contents { get; private set; }
        public bool RequiresMainThread { get; private set; }

        public Packet(string header, byte[] Contents, bool requiresMainThread)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            Contents = Contents ?? Array.Empty<byte>();
            RequiresMainThread = requiresMainThread;
        }

        public static Packet CreatePacketFromObject(string header, object? objectToUse = null, bool requiresMainThread = true)
        {
            if (header == null)
                throw new ArgumentNullException(nameof(header));

            // Updated: Using modern serialization method
            byte[] Contents = objectToUse != null 
                ? System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(objectToUse) 
                : Array.Empty<byte>();

            return new Packet(header, Contents, requiresMainThread);
        }
    }
}