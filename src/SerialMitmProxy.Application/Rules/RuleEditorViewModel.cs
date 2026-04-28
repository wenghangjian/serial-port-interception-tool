using System.Collections.ObjectModel;
using SerialMitmProxy.Application.Common;
using SerialMitmProxy.Core.Models;
using SerialMitmProxy.Core.Rules;

namespace SerialMitmProxy.Application.Rules;

public sealed class RuleEditorViewModel : ViewModelBase
{
    public ObservableCollection<Rule> Rules { get; } = new();

    public void AddDefaultInterceptRule(Direction direction)
    {
        Rules.Add(new Rule
        {
            Name = $"Intercept-{direction}",
            Matchers = new IRuleMatcher[]
            {
                new DirectionMatcher(direction),
            },
            Actions = new[]
            {
                new RuleAction { Type = RuleActionType.Intercept },
            },
        });
    }

    public void AddMonitorHexRule(string name, Direction? direction, string hexPattern)
    {
        Rules.Add(new Rule
        {
            Name = name,
            Scope = RuleScope.Monitor,
            Matchers = BuildMonitorMatchers(direction, new HexPatternMatcher(hexPattern)),
        });
    }

    public void AddMonitorAsciiRule(string name, Direction? direction, string regexPattern)
    {
        Rules.Add(new Rule
        {
            Name = name,
            Scope = RuleScope.Monitor,
            Matchers = BuildMonitorMatchers(direction, new RegexMatcher(regexPattern)),
        });
    }

    public void ReplaceRules(IEnumerable<Rule> rules)
    {
        Rules.Clear();
        foreach (var rule in rules)
        {
            Rules.Add(rule);
        }
    }

    public bool RemoveRule(Rule? rule)
    {
        return rule is not null && Rules.Remove(rule);
    }

    private static IRuleMatcher[] BuildMonitorMatchers(Direction? direction, IRuleMatcher patternMatcher)
    {
        var matchers = new List<IRuleMatcher>();
        if (direction.HasValue)
        {
            matchers.Add(new DirectionMatcher(direction.Value));
        }

        matchers.Add(patternMatcher);
        return matchers.ToArray();
    }
}
