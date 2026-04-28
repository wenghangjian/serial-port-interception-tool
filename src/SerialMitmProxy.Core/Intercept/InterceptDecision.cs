namespace SerialMitmProxy.Core.Intercept;

public sealed class InterceptDecision
{
    public InterceptCommand Command { get; init; } = InterceptCommand.Forward;

    public byte[]? EditedPayload { get; init; }

    public byte[]? InjectPayload { get; init; }

    public int RepeatCount { get; init; } = 1;
}
