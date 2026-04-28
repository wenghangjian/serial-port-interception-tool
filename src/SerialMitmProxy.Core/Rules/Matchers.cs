using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SerialMitmProxy.Core.Models;

namespace SerialMitmProxy.Core.Rules;

public sealed class DirectionMatcher : IRuleMatcher
{
    public DirectionMatcher(Direction expectedDirection)
    {
        ExpectedDirection = expectedDirection;
    }

    public Direction ExpectedDirection { get; }

    public bool IsMatch(Direction direction, byte[] payload)
    {
        return direction == ExpectedDirection;
    }
}

public sealed class LengthMatcher : IRuleMatcher
{
    public LengthMatcher(int? minLength = null, int? maxLength = null)
    {
        if (minLength.HasValue && minLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minLength));
        }

        if (maxLength.HasValue && maxLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength));
        }

        if (minLength.HasValue && maxLength.HasValue && minLength > maxLength)
        {
            throw new ArgumentException("minLength must be <= maxLength.");
        }

        MinLength = minLength;
        MaxLength = maxLength;
    }

    public int? MinLength { get; }

    public int? MaxLength { get; }

    public bool IsMatch(Direction direction, byte[] payload)
    {
        if (MinLength.HasValue && payload.Length < MinLength)
        {
            return false;
        }

        if (MaxLength.HasValue && payload.Length > MaxLength)
        {
            return false;
        }

        return true;
    }
}

public sealed class HexPatternMatcher : IRuleMatcher
{
    private readonly byte?[] _pattern;

    public HexPatternMatcher(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new ArgumentException("Pattern is required.", nameof(pattern));
        }

        _pattern = ParsePattern(pattern);
    }

    public bool IsMatch(Direction direction, byte[] payload)
    {
        if (_pattern.Length == 0 || payload.Length < _pattern.Length)
        {
            return false;
        }

        for (var start = 0; start <= payload.Length - _pattern.Length; start++)
        {
            var matched = true;
            for (var i = 0; i < _pattern.Length; i++)
            {
                var expected = _pattern[i];
                if (expected.HasValue && payload[start + i] != expected.Value)
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return true;
            }
        }

        return false;
    }

    private static byte?[] ParsePattern(string pattern)
    {
        var tokens = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var parsed = new List<byte?>();

        foreach (var token in tokens)
        {
            if (token == "??")
            {
                parsed.Add(null);
                continue;
            }

            parsed.Add(byte.Parse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }

        return parsed.ToArray();
    }
}

public sealed class RegexMatcher : IRuleMatcher
{
    private readonly Regex _regex;

    public RegexMatcher(string pattern)
    {
        _regex = new Regex(pattern, RegexOptions.Compiled);
    }

    public bool IsMatch(Direction direction, byte[] payload)
    {
        var ascii = Encoding.ASCII.GetString(payload);
        return _regex.IsMatch(ascii);
    }
}
