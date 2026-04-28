using SerialMitmProxy.Core.Models;

namespace SerialMitmProxy.Core.Rules;

public sealed class RuleEngine
{
    private readonly List<Rule> _rules = new();

    public RuleEngine(IEnumerable<Rule>? rules = null)
    {
        if (rules is not null)
        {
            _rules.AddRange(rules);
        }
    }

    public IReadOnlyList<Rule> Rules => _rules;

    public void ReplaceAll(IEnumerable<Rule> rules)
    {
        _rules.Clear();
        _rules.AddRange(rules);
    }

    public RuleEvaluationResult Evaluate(Direction direction, byte[] payload)
    {
        var result = new RuleEvaluationResult(payload.ToArray());

        foreach (var rule in _rules)
        {
            if (!rule.Enabled)
            {
                continue;
            }

            if (rule.Scope != RuleScope.Proxy)
            {
                continue;
            }

            if (!rule.Matchers.All(matcher => matcher.IsMatch(direction, result.Payload)))
            {
                continue;
            }

            foreach (var action in rule.Actions)
            {
                ApplyAction(result, action);
            }
        }

        return result;
    }

    private static void ApplyAction(RuleEvaluationResult result, RuleAction action)
    {
        var transformed = result.Payload;
        foreach (var transformer in action.Transformers)
        {
            transformed = transformer.Transform(transformed);
        }

        result.Payload = transformed;

        switch (action.Type)
        {
            case RuleActionType.Pass:
                return;
            case RuleActionType.Drop:
                result.Drop = true;
                return;
            case RuleActionType.Modify:
                return;
            case RuleActionType.Intercept:
                result.Intercept = true;
                return;
            case RuleActionType.Delay:
                if (action.Delay > result.Delay)
                {
                    result.Delay = action.Delay;
                }

                return;
            case RuleActionType.Inject:
                if (action.Payload is not null && action.Payload.Length > 0)
                {
                    result.Injections.Add(action.Payload.ToArray());
                }

                return;
            case RuleActionType.Duplicate:
                result.DuplicateCount = Math.Max(result.DuplicateCount, Math.Max(action.RepeatCount, 2));
                return;
            default:
                throw new InvalidOperationException($"Unsupported action type: {action.Type}");
        }
    }
}
