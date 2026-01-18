using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using MessagePack;


namespace TCPNetwork.Packets.ServerBrowser
{
    [MessagePackObject]
    public struct ServerAuth(ulong id, ulong secret) : IEquatable<ServerAuth>
    {
        public const int PacketSize = sizeof(ulong) + sizeof(ulong); 
        [Key(0)] public ulong _secret = secret; 
        [Key(1)] public ulong _id = id; 

        public override int GetHashCode()
        {
            return _secret.GetHashCode() ^ _id.GetHashCode();
        }

        public bool Equals(ServerAuth other)
        {
            return _secret == other._secret && _id == other._id;
        }

        public void CopyInto(Span<byte> buffer)
        {
            if (buffer.Length < PacketSize)
            {
                throw new Exception($"Not enough bytes for packet header {PacketSize} bytes in buffer {buffer.Length}");
            }
 
            BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(0, 8), _id);
            BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(8, 8), _secret);
        }

        public override bool Equals(object obj)
        {
            return obj is ServerAuth other && Equals(other);
        }

        public static bool operator ==(ServerAuth left, ServerAuth right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ServerAuth left, ServerAuth right)
        {
            return !left.Equals(right);
        }

        public byte[] Serialize()
        {
            Span<byte> bytes = new byte[PacketSize];
            BinaryPrimitives.WriteUInt64LittleEndian(bytes, _id);
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.Slice(sizeof(ulong)), _secret);
            return bytes.ToArray();
        }

        public static ServerAuth Deserialize(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length != PacketSize)
            {
                throw new Exception($"Tried reading more than {PacketSize}, got size {bytes.Length}");
            }
            ServerAuth auth = new ServerAuth();
            auth._id = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(0, sizeof(ulong)));
            auth._secret = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(sizeof(ulong)));
            return auth;
        }
    }
}