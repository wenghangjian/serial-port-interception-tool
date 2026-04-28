namespace SerialMitmProxy.Core.Rules;

public enum RuleActionType
{
    Pass = 0,
    Drop = 1,
    Modify = 2,
    Intercept = 3,
    Delay = 4,
    Inject = 5,
    Duplicate = 6,
}
