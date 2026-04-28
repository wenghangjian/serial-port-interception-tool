namespace SerialMitmProxy.Core.Rules;

public sealed class ReplaceBytesTransformer : IPayloadTransformer
{
    private readonly byte[] _search;
    private readonly byte[] _replace;

    public ReplaceBytesTransformer(byte[] search, byte[] replace)
    {
        if (search.Length == 0)
        {
            throw new ArgumentException("search cannot be empty", nameof(search));
        }

        _search = search;
        _replace = replace;
    }

    public byte[] Transform(byte[] payload)
    {
        if (payload.Length == 0)
        {
            return Array.Empty<byte>();
        }

        var result = new List<byte>(payload.Length);
        var index = 0;

        while (index < payload.Length)
        {
            if (MatchesAt(payload, index, _search))
            {
                result.AddRange(_replace);
                index += _search.Length;
                continue;
            }

            result.Add(payload[index]);
            index++;
        }

        return result.ToArray();
    }

    private static bool MatchesAt(byte[] source, int start, byte[] pattern)
    {
        if (start + pattern.Length > source.Length)
        {
            return false;
        }

        for (var i = 0; i < pattern.Length; i++)
        {
            if (source[start + i] != pattern[i])
            {
                return false;
            }
        }

        return true;
    }
}

public sealed class PatchOffsetTransformer : IPayloadTransformer
{
    private readonly int _offset;
    private readonly byte[] _patch;

    public PatchOffsetTransformer(int offset, byte[] patch)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        _offset = offset;
        _patch = patch;
    }

    public byte[] Transform(byte[] payload)
    {
        var output = payload.ToArray();
        if (_offset >= output.Length)
        {
            return output;
        }

        for (var i = 0; i < _patch.Length; i++)
        {
            var index = _offset + i;
            if (index >= output.Length)
            {
                break;
            }

            output[index] = _patch[i];
        }

        return output;
    }
}

public sealed class ChecksumFixTransformer : IPayloadTransformer
{
    private readonly int _startOffset;
    private readonly int _endOffset;
    private readonly int _checksumOffset;

    public ChecksumFixTransformer(int startOffset, int endOffset, int checksumOffset)
    {
        if (startOffset < 0 || endOffset < startOffset)
        {
            throw new ArgumentOutOfRangeException(nameof(startOffset));
        }

        if (checksumOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(checksumOffset));
        }

        _startOffset = startOffset;
        _endOffset = endOffset;
        _checksumOffset = checksumOffset;
    }

    public byte[] Transform(byte[] payload)
    {
        var output = payload.ToArray();
        if (_checksumOffset >= output.Length)
        {
            return output;
        }

        var sum = 0;
        var upper = Math.Min(_endOffset, output.Length - 1);
        for (var i = _startOffset; i <= upper; i++)
        {
            sum += output[i];
        }

        output[_checksumOffset] = (byte)(sum & 0xFF);
        return output;
    }
}
