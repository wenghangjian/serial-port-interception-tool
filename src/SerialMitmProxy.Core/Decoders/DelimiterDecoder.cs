using SerialMitmProxy.Core.Abstractions;
using SerialMitmProxy.Core.Models;

namespace SerialMitmProxy.Core.Decoders;

public sealed class DelimiterDecoder : IFrameDecoder
{
    private readonly byte[] _delimiter;
    private readonly List<byte> _buffer = new();

    public DelimiterDecoder(byte[] delimiter)
    {
        if (delimiter.Length == 0)
        {
            throw new ArgumentException("Delimiter cannot be empty.", nameof(delimiter));
        }

        _delimiter = delimiter;
    }

    public IReadOnlyList<DecodedFrame> Decode(ReadOnlyMemory<byte> chunk, DateTimeOffset timestampUtc)
    {
        var output = new List<DecodedFrame>();
        if (!chunk.IsEmpty)
        {
            _buffer.AddRange(chunk.ToArray());
        }

        while (true)
        {
            var matchIndex = IndexOf(_buffer, _delimiter);
            if (matchIndex < 0)
            {
                break;
            }

            var frameLength = matchIndex + _delimiter.Length;
            var frame = _buffer.Take(frameLength).ToArray();
            _buffer.RemoveRange(0, frameLength);
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

    private static int IndexOf(List<byte> source, byte[] pattern)
    {
        for (var i = 0; i <= source.Count - pattern.Length; i++)
        {
            var matched = true;
            for (var j = 0; j < pattern.Length; j++)
            {
                if (source[i + j] == pattern[j])
                {
                    continue;
                }

                matched = false;
                break;
            }

            if (matched)
            {
                return i;
            }
        }

        return -1;
    }
}
