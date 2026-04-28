using SerialMitmProxy.Core.Abstractions;
using SerialMitmProxy.Core.Models;

namespace SerialMitmProxy.Core.Decoders;

public sealed class TimeSliceDecoder : IFrameDecoder
{
    private readonly TimeSpan _idleThreshold;
    private readonly List<byte> _buffer = new();
    private DateTimeOffset? _lastSeenAt;

    public TimeSliceDecoder(TimeSpan idleThreshold)
    {
        if (idleThreshold <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(idleThreshold));
        }

        _idleThreshold = idleThreshold;
    }

    public IReadOnlyList<DecodedFrame> Decode(ReadOnlyMemory<byte> chunk, DateTimeOffset timestampUtc)
    {
        var output = new List<DecodedFrame>();

        if (_buffer.Count > 0 && _lastSeenAt.HasValue && timestampUtc - _lastSeenAt.Value >= _idleThreshold)
        {
            output.Add(new DecodedFrame(_buffer.ToArray(), _lastSeenAt.Value));
            _buffer.Clear();
        }

        if (!chunk.IsEmpty)
        {
            _buffer.AddRange(chunk.ToArray());
            _lastSeenAt = timestampUtc;
        }

        return output;
    }

    public IReadOnlyList<DecodedFrame> Flush(DateTimeOffset timestampUtc)
    {
        if (_buffer.Count == 0)
        {
            return Array.Empty<DecodedFrame>();
        }

        var frame = new DecodedFrame(_buffer.ToArray(), _lastSeenAt ?? timestampUtc);
        _buffer.Clear();
        _lastSeenAt = null;
        return new[] { frame };
    }
}
