using SerialMitmProxy.Core.Models;
using SerialMitmProxy.Core.Rules;

namespace SerialMitmProxy.Core.Tests.Rules;

public sealed class RuleEngineTests
{
    [Fact]
    public void RuleEngine_AppliesModifyDelayDuplicateAndInject()
    {
        var rule = new Rule
        {
            Name = "modify",
            Matchers = new IRuleMatcher[]
            {
                new DirectionMatcher(Direction.AtoB),
                new LengthMatcher(minLength: 4),
                new HexPatternMatcher("AA ?? CC"),
            },
            Actions = new[]
            {
                new RuleAction
                {
                    Type = RuleActionType.Modify,
                    Transformers = new IPayloadTransformer[]
                    {
                        new PatchOffsetTransformer(1, new byte[] { 0x10 }),
                        new ChecksumFixTransformer(0, 2, 3),
                    },
                },
                new RuleAction
                {
                    Type = RuleActionType.Delay,
                    Delay = TimeSpan.FromMilliseconds(20),
                },
                new RuleAction
                {
                    Type = RuleActionType.Inject,
                    Payload = new byte[] { 0x99, 0x88 },
                },
                new RuleAction
                {
                    Type = RuleActionType.Duplicate,
                    RepeatCount = 3,
                },
            },
        };

        var engine = new RuleEngine(new[] { rule });
        var result = engine.Evaluate(Direction.AtoB, new byte[] { 0xAA, 0xBB, 0xCC, 0x00 });

        Assert.False(result.Drop);
        Assert.False(result.Intercept);
        Assert.Equal(3, result.DuplicateCount);
        Assert.Equal(TimeSpan.FromMilliseconds(20), result.Delay);
        Assert.Single(result.Injections);
        Assert.Equal(new byte[] { 0x99, 0x88 }, result.Injections[0]);
        Assert.Equal(new byte[] { 0xAA, 0x10, 0xCC, 0x86 }, result.Payload);
    }

    [Fact]
    public void RuleEngine_DropsWhenRegexMatches()
    {
        var rule = new Rule
        {
            Name = "drop",
            Matchers = new IRuleMatcher[]
            {
                new RegexMatcher("PING"),
            },
            Actions = new[]
            {
                new RuleAction { Type = RuleActionType.Drop },
            },
        };

        var engine = new RuleEngine(new[] { rule });
        var result = engine.Evaluate(Direction.BtoA, System.Text.Encoding.ASCII.GetBytes("PING?"));

        Assert.True(result.Drop);
    }

    [Fact]
    public void RuleEngine_StopsApplyingRule_WhenEnabledIsTurnedOffAtRuntime()
    {
        var rule = new Rule
        {
            Name = "toggle",
            Matchers = new IRuleMatcher[]
            {
                new RegexMatcher("PING"),
            },
            Actions = new[]
            {
                new RuleAction { Type = RuleActionType.Drop },
            },
        };

        var engine = new RuleEngine(new[] { rule });
        rule.Enabled = false;

        var result = engine.Evaluate(Direction.BtoA, System.Text.Encoding.ASCII.GetBytes("PING?"));

        Assert.False(result.Drop);
    }

    [Fact]
    public void RuleEngine_IgnoresMonitorScopedRules()
    {
        var rule = new Rule
        {
            Name = "monitor-only",
            Scope = RuleScope.Monitor,
            Matchers = new IRuleMatcher[]
            {
                new RegexMatcher("PING"),
            },
            Actions = new[]
            {
                new RuleAction { Type = RuleActionType.Drop },
            },
        };

        var engine = new RuleEngine(new[] { rule });
        var result = engine.Evaluate(Direction.BtoA, System.Text.Encoding.ASCII.GetBytes("PING?"));

        Assert.False(result.Drop);
    }
}
