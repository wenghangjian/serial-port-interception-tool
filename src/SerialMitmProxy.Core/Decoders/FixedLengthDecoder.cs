using SerialMitmProxy.Core.Abstractions;
using SerialMitmProxy.Core.Models;

namespace SerialMitmProxy.Core.Decoders;

public sealed class FixedLengthDecoder : IFrameDecoder
{
    private readonly int _length;
    private readonly List<byte> _buffer = new();

    public FixedLengthDecoder(int length)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        _length = length;
    }

    public IReadOnlyList<DecodedFrame> Decode(ReadOnlyMemory<byte> chunk, DateTimeOffset timestampUtc)
    {
        var output = new List<DecodedFrame>();

        if (!chunk.IsEmpty)
        {
            _buffer.AddRange(chunk.ToArray());
        }

        while (_buffer.Count >= _length)
        {
            var frame = _buffer.Take(_length).ToArray();
            _buffer.RemoveRange(0, _length);
            output.Add(new DecodedFrame(frame, timestampUtc));
        }

        return output;
    }

    public IReadOnlyList<DecodedFrame> Flush(DateTimeOffset timestampUtc)
    {
        if (_buffer.Count == 0)
        {
            return Array.Empty<DecodedFrame>();
        }

        var frame = new DecodedFrame(_buffer.ToArray(), timestampUtc);
        _buffer.Clear();
        return new[] { frame };
    }
}
