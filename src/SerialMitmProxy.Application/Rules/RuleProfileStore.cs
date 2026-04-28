using System.Text.Json;
using SerialMitmProxy.Core.Models;
using SerialMitmProxy.Core.Rules;

namespace SerialMitmProxy.Application.Rules;

public sealed class RuleProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public async Task SaveAsync(string filePath, IEnumerable<RuleProfileEntry> entries, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await JsonSerializer.SerializeAsync(stream, entries.ToArray(), JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RuleProfileEntry>> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        var entries = await JsonSerializer.DeserializeAsync<RuleProfileEntry[]>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return entries ?? Array.Empty<RuleProfileEntry>();
    }

    public static Rule ToRuntimeRule(RuleProfileEntry entry)
    {
        var matchers = new List<IRuleMatcher>();

        if (entry.Direction.HasValue)
        {
            matchers.Add(new DirectionMatcher(entry.Direction.Value));
        }

        if (entry.MinLength.HasValue || entry.MaxLength.HasValue)
        {
            matchers.Add(new LengthMatcher(entry.MinLength, entry.MaxLength));
        }

        if (!string.IsNullOrWhiteSpace(entry.HexPattern))
        {
            matchers.Add(new HexPatternMatcher(entry.HexPattern));
        }

        if (!string.IsNullOrWhiteSpace(entry.RegexPattern))
        {
            matchers.Add(new RegexMatcher(entry.RegexPattern));
        }

        var transformers = new List<IPayloadTransformer>();
        if (!string.IsNullOrWhiteSpace(entry.ReplaceFromHex) && !string.IsNullOrWhiteSpace(entry.ReplaceToHex))
        {
            transformers.Add(new ReplaceBytesTransformer(ParseHex(entry.ReplaceFromHex), ParseHex(entry.ReplaceToHex)));
        }

        if (entry.PatchOffset.HasValue && !string.IsNullOrWhiteSpace(entry.PatchHex))
        {
            transformers.Add(new PatchOffsetTransformer(entry.PatchOffset.Value, ParseHex(entry.PatchHex)));
        }

        if (entry.ChecksumStart.HasValue && entry.ChecksumEnd.HasValue && entry.ChecksumOffset.HasValue)
        {
            transformers.Add(new ChecksumFixTransformer(entry.ChecksumStart.Value, entry.ChecksumEnd.Value, entry.ChecksumOffset.Value));
        }

        var action = new RuleAction
        {
            Type = entry.Action,
            Delay = TimeSpan.FromMilliseconds(entry.DelayMs ?? 0),
            Payload = string.IsNullOrWhiteSpace(entry.InjectHex) ? null : ParseHex(entry.InjectHex),
            RepeatCount = entry.RepeatCount ?? 2,
            Transformers = transformers,
        };

        return new Rule
        {
            Name = entry.Name,
            Enabled = entry.Enabled,
            Matchers = matchers,
            Actions = new[] { action },
        };
    }

    private static byte[] ParseHex(string hex)
    {
        var tokens = hex.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Select(token => Convert.ToByte(token, 16)).ToArray();
    }
}

public sealed class RuleProfileEntry
{
    public string Name { get; init; } = string.Empty;

    public bool Enabled { get; init; } = true;

    public Direction? Direction { get; init; }

    public int? MinLength { get; init; }

    public int? MaxLength { get; init; }

    public string? HexPattern { get; init; }

    public string? RegexPattern { get; init; }

    public RuleActionType Action { get; init; } = RuleActionType.Pass;

    public int? DelayMs { get; init; }

    public int? RepeatCount { get; init; }

    public string? InjectHex { get; init; }

    public string? ReplaceFromHex { get; init; }

    public string? ReplaceToHex { get; init; }

    public int? PatchOffset { get; init; }

    public string? PatchHex { get; init; }

    public int? ChecksumStart { get; init; }

    public int? ChecksumEnd { get; init; }

    public int? ChecksumOffset { get; init; }
}
