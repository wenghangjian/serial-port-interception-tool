namespace SerialMitmProxy.Core.Models;

public sealed record Frame(
    Guid Id,
    Direction Direction,
    byte[] Payload,
    DateTimeOffset TimestampUtc,
    bool IsSynthetic = false,
    bool IsModified = false)
{
    public static Frame Create(
        Direction direction,
        ReadOnlyMemory<byte> payload,
        DateTimeOffset? timestampUtc = null,
        bool isSynthetic = false,
        bool isModified = false)
    {
        return new Frame(Guid.NewGuid(), direction, payload.ToArray(), timestampUtc ?? DateTimeOffset.UtcNow, isSynthetic, isModified);
    }
}

public sealed record DecodedFrame(byte[] Payload, DateTimeOffset TimestampUtc);
