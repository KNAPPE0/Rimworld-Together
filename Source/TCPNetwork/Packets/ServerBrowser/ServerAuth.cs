using System;
using System.Runtime.CompilerServices;
using MessagePack;


namespace TCPNetwork.Packets.ServerBrowser
{
    [MessagePackObject]
    public struct ServerAuth(ulong id, ulong secret) : IEquatable<ServerAuth>
    {
        public const int PacketSize = sizeof(ulong) + sizeof(ulong) + sizeof(short); 
        [Key(0)] public ulong _secret = secret; 
        [Key(1)] public ulong _id = id; 
        [IgnoreMember] public long LastTimeSinceHeartbeat;

        public override int GetHashCode()
        {
            return _secret.GetHashCode() ^ _id.GetHashCode();
        }

        public bool Equals(ServerAuth other)
        {
            return _secret == other._secret && _id == other._id;
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
    }
}