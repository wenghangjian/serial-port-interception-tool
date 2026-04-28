using SerialMitmProxy.Core.Models;

namespace SerialMitmProxy.Core.Rules;

public interface IRuleMatcher
{
    bool IsMatch(Direction direction, byte[] payload);
}

public interface IPayloadTransformer
{
    byte[] Transform(byte[] payload);
}

public enum RuleScope
{
    Proxy = 0,
    Monitor = 1,
}

public sealed class RuleAction
{
    public RuleActionType Type { get; init; } = RuleActionType.Pass;

    public TimeSpan Delay { get; init; } = TimeSpan.Zero;

    public byte[]? Payload { get; init; }

    public int RepeatCount { get; init; } = 2;

    public IReadOnlyList<IPayloadTransformer> Transformers { get; init; } = Array.Empty<IPayloadTransformer>();
}

public sealed class Rule
{
    public string Name { get; init; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public RuleScope Scope { get; init; } = RuleScope.Proxy;

    public IReadOnlyList<IRuleMatcher> Matchers { get; init; } = Array.Empty<IRuleMatcher>();

    public IReadOnlyList<RuleAction> Actions { get; init; } = Array.Empty<RuleAction>();
}
