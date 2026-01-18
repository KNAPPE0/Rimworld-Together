using System;
using System.Buffers.Binary;

namespace TCPNetwork.Packets.ServerBrowser;

public record struct Telemetry
{
    public const int PacketSize = sizeof(int);
    public int _playerCount;

    public Telemetry(int playerCount)
    {
        _playerCount = playerCount;
    }

    public static Telemetry FromSpan(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < PacketSize)
        {
            throw new ArgumentException("Buffer too small", nameof(buffer));
        }
        var playerCount = BinaryPrimitives.ReadInt32LittleEndian(buffer);
        return new Telemetry(playerCount);
    }

    public void SerializeInto(Span<byte> buffer)
    {
        if (buffer.Length < PacketSize)
        {
            throw new ArgumentException("Buffer too small", nameof(buffer));
        }
        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(0, 4), _playerCount);
    }
}