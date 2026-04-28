namespace SerialMitmProxy.Core.Rules;

public sealed class RuleEvaluationResult
{
    public RuleEvaluationResult(byte[] payload)
    {
        Payload = payload;
    }

    public byte[] Payload { get; set; }

    public bool Drop { get; set; }

    public bool Intercept { get; set; }

    public TimeSpan Delay { get; set; }

    public int DuplicateCount { get; set; } = 1;

    public List<byte[]> Injections { get; } = new();
}
